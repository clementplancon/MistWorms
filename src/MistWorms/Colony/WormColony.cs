using System;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Assets;
using MistWorms.Config;
using MistWorms.Drops;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// One colony = one network object that owns and simulates every virtual worm of a mine (spec §10.1, §11.1).
    /// This file holds the life cycle, the ZDO state, the registry and the render source; the simulation is in
    /// WormSim.cs (states, movement) and WormSense.cs (perception, fire, paths).
    /// </summary>
    internal sealed partial class WormColony : MonoBehaviour, IWormRenderSource
    {
        public enum ColonyKind
        {
            Test = 0,   // dev colony around the player, outside any dungeon
            Mine = 1,   // colony at the root of an infested mine's dungeon
        }

        /// <summary>A colony owned by this peer with no worm for this long destroys itself (spec §8.4).</summary>
        public const float EmptyDestroyDelay = 10f;

        // Debug builds keep spare slots so scenarios can exceed the cap with force (spec §11.1).
#if MW_DEV
        private const int DebugSpareSlots = 50;
#else
        private const int DebugSpareSlots = 0;
#endif

        /// <summary>Dev switch (mw_freeze): every colony stops simulating, worms stay where they are.</summary>
        public static bool Frozen;

        private ZNetView _nview;
        private bool _initialized;
        private bool _configured;
        private SpatialGrid _grid;
        private System.Random _rng;
        private System.Random _dropRng;      // native drops (spec §7.5): own generator so tests can seed it without touching the movement
        private WormEffects.Limiter _deathFx;
        private WormEffects.Limiter _hitFx;
        private WormEffects.Limiter _strikeFx;
        private WormEffects.Limiter _idleFx;
        private float _emptySince = -1f;
        private float _lastTickTime;
        private bool _registeredWithRenderer;

        public WormStore Store { get; private set; }
        public ColonyKind Kind { get; private set; }
        public Vector3 BoxCenter { get; private set; }
        public Vector3 BoxHalfSize { get; private set; }
        public bool IsConfigured => _configured;
        public bool Disabled { get; private set; }
        public string DisabledReason { get; private set; }

        /// <summary>Wall clock cost of the last simulation pass of this colony, and its running average / maximum.</summary>
        public float LastSimMs { get; private set; }
        public float AvgSimMs { get; private set; }
        public float MaxSimMs { get; private set; }
        public int LastSimSteps { get; private set; }

        public ZNetView NView => _nview;
        public bool IsOwner => _nview != null && _nview.IsValid() && _nview.IsOwner();
        public long OwnerUid => _nview != null && _nview.IsValid() ? _nview.GetZDO().GetOwner() : 0L;
        public ZDOID ZdoId => _nview != null && _nview.IsValid() ? _nview.GetZDO().m_uid : ZDOID.None;

        // ------------------------------------------------------------------ creation

        /// <summary>Instantiates a colony at <paramref name="position"/> with its box; null if the prefab is not registered.</summary>
        public static WormColony Create(Vector3 position, ColonyKind kind, Vector3 boxCenter, Vector3 boxHalfSize)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(MWNames.ColonyPrefab) : null;
            if (prefab == null)
            {
                Plugin.Log.LogError($"[Colony] prefab {MWNames.ColonyPrefab} is not registered in the ZNetScene");
                return null;
            }

            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            var colony = go.GetComponent<WormColony>();
            if (colony == null)
            {
                Destroy(go);
                return null;
            }
            colony.Configure(kind, boxCenter, boxHalfSize);
            return colony;
        }

        /// <summary>Sets the kind and the box, and writes them to the ZDO when this peer owns it.</summary>
        public void Configure(ColonyKind kind, Vector3 boxCenter, Vector3 boxHalfSize)
        {
            TryInit();
            if (!_initialized) return;
            Kind = kind;
            BoxCenter = boxCenter;
            BoxHalfSize = boxHalfSize;
            _boxBounds = new Bounds(boxCenter, boxHalfSize * 2f);
            _configured = true;

            ZDO zdo = _nview.GetZDO();
            if (zdo != null && zdo.IsOwner())
            {
                zdo.Set(MWNames.ZdoKind, (int)kind);
                zdo.Set(MWNames.ZdoBoxCenter, boxCenter);
                zdo.Set(MWNames.ZdoBoxExtents, boxHalfSize);
            }
        }

        // ------------------------------------------------------------------ life cycle

        private void Awake()
        {
            ColonyRegistry.Add(this);
            TryInit();
        }

        private void Start()
        {
            RegisterWithRenderer();
        }

        private void OnDestroy()
        {
            ColonyRegistry.Remove(this);
            if (_registeredWithRenderer && WormRenderer.Existing != null) WormRenderer.Existing.Unregister(this);
            _registeredWithRenderer = false;
        }

        private void TryInit()
        {
            if (_initialized) return;
            if (_nview == null) _nview = GetComponent<ZNetView>();
            if (_nview == null)
            {
                // ZNetView removed itself (no ZDOMan: a prefab copy, not a world object).
                enabled = false;
                return;
            }
            if (!_nview.IsValid()) return;

            int capacity = MWConfig.Colony.MaxAlivePerMine.Value + DebugSpareSlots;
            Store = new WormStore(capacity);
            _grid = new SpatialGrid(capacity);
            _rng = new System.Random(Environment.TickCount ^ GetInstanceID());
            _dropRng = WormDrops.NewRng(GetInstanceID());
            _nview.Register<int, int, ZPackage>(MWNames.RpcWormHit, RPC_WormHit);
            _nview.Register<ZDOID, int, Vector3, ZPackage>(MWNames.RpcMassDissolve, RPC_MassDissolve);
            _nview.Register<int, Vector3>(MWNames.RpcNestGone, RPC_NestGone);
            InitNests();
            InitPaths();
            InitTargetCache(capacity);
            InitMassLink(capacity);

            ZDO zdo = _nview.GetZDO();
            Kind = (ColonyKind)zdo.GetInt(MWNames.ZdoKind, 0);
            Vector3 half = zdo.GetVec3(MWNames.ZdoBoxExtents, Vector3.zero);
            if (half != Vector3.zero)
            {
                // A reloaded colony: the box is persisted (spec §8.4); worms are not before phase 7, so it will empty out and vanish.
                BoxCenter = zdo.GetVec3(MWNames.ZdoBoxCenter, transform.position);
                BoxHalfSize = half;
                _boxBounds = new Bounds(BoxCenter, half * 2f);
                _configured = true;
            }
            _lastTickTime = Time.time;
            _initialized = true;
        }

        private void RegisterWithRenderer()
        {
            // Rendering is client only; a dedicated server has no renderer and no assets.
            if (_registeredWithRenderer || Application.isBatchMode || !AssetCatalog.IsReady) return;
            WormRenderer.Instance.Register(this);
            _registeredWithRenderer = true;
        }

        private void Update()
        {
            if (!_initialized)
            {
                TryInit();
                if (!_initialized) return;
            }
            if (Disabled || !_configured || !_nview.IsValid()) return;

            if (!_nview.IsOwner())
            {
                // Phase 8: deltas + dead reckoning. Until then only the owner simulates and non owners show nothing.
                return;
            }
            if (Frozen) return;

            try
            {
                Tick();
                UpdateIdleSounds(Time.time);
            }
            catch (Exception e)
            {
                // Spec §9.3: a simulation error freezes this colony (no drops) instead of reaching the game.
                Disabled = true;
                DisabledReason = e.GetType().Name + ": " + e.Message;
                Plugin.LogException("WormColony.Tick (colony disabled)", e);
            }
        }

        /// <summary>Removes the colony object and its ZDO; the owner only.</summary>
        public void DestroyColony()
        {
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(gameObject);
            else Destroy(gameObject);
        }

        // ------------------------------------------------------------------ worms

        /// <summary>Creates a worm standing on the floor under <paramref name="position"/>. Returns the slot or -1 (full or cap reached).</summary>
        public int AddWorm(Vector3 position, WormState state, float headingRad, bool force = false)
        {
            if (!_initialized || Store == null) return -1;
            if (!force && TotalAlive >= MWConfig.Colony.MaxAlivePerMine.Value) return -1;   // free worms and the worms inside masses (spec §8.2)

            float now = Time.time;
            int i = Store.Alloc(position, headingRad, state, MWConfig.Worm.Health.Value, now);
            if (i < 0) return -1;
            _blacklist.Clear(i);
            _watch[i].Reset();
            _obstacleKey[i] = 0L;
            _livingSeen[i] = -1000f;
#if MW_DEV
            if (_histCount != null) { _histCount[i] = 0; RecordHistory(i, state, now); }
#endif

            WormStore s = Store;
            if (WormPhysics.ProbeGround(position, out RaycastHit hit))
            {
                s.pos[i] = hit.point;
                s.prevPos[i] = hit.point;
                s.groundNormal[i] = hit.normal;
                s.home[i] = hit.point;
                s.progressPos[i] = hit.point;
            }
            // Spread the first steps and de-synchronise the wandering and the crawl animation.
            s.nextSimTime[i] = now + (i % 10) * 0.005f;
            s.pauseUntil[i] = now + (float)_rng.NextDouble() * 2f;
            s.animTime[i] = (float)_rng.NextDouble() * 10f;
            _emptySince = -1f;
            return i;
        }

        /// <summary>Removes a worm. <paramref name="withDrops"/> false = no effect and no drop (out of the box, no floor, dev kill).</summary>
        public void KillWorm(int i, bool withDrops)
        {
            if (Store == null || (uint)i >= (uint)Store.Capacity || !Store.alive[i]) return;
            Vector3 where = Store.pos[i];
            _killPool.Clear();
            if (Store.poolIndex[i] >= 0 && withDrops) _killPool.CopyFrom(Store.Pools.Get(Store.poolIndex[i]));   // Free releases the slot
            ReleasePath(i);
            ResetIdleSound(i);
#if MW_DEV
            RecordDeath(i, withDrops);
#endif
            Store.Free(i);

            // A disabled colony (simulation error) gives nothing (spec §9.3).
            if (!withDrops || Disabled) return;
            try
            {
                WormEffects.PlayDeath(ref _deathFx, where + Vector3.up * 0.1f, Time.time);
                WormDrops.DropFor(_dropRng, where);
                WormDrops.DropPoolRest(_killPool, where + Vector3.up * 0.3f);   // a free worm hands out its own pool, floor of quantity + carry, the fraction is lost: the loot pool (spec §7.3).
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.KillWorm (effect/drops)", e);
            }
        }

#if MW_DEV
        // Frame detail of the simulation (plan 4A-3 fix 3): counters of the frame in progress and the three worst frames since the reset.
        private int _framePaths, _frameApproach, _frameQueries, _frameStrikes;
        private float _frameStrikeFxMs, _frameStrikeBiteMs;
        private int _strikesSoFar;
        private bool _frameFirstSessionStrike;

        /// <summary>Strikes since the game was loaded, all colonies (the first one pays for the first vanilla Damage() and the JIT).</summary>
        private static int s_sessionStrikes;

        /// <summary>Cost of the frame holding the first strike of the session (-1 = not seen by this colony), left out of <see cref="PeakMsWithoutFirstStrike"/>.</summary>
        public float FirstSessionStrikeMs { get; private set; } = -1f;

        /// <summary>Worst simulation frame since the last reset, not counting the frame of the session's first strike.</summary>
        public float PeakMsWithoutFirstStrike { get; private set; }

        /// <summary>Why the worms died since the last <see cref="ResetCounters"/> (at most 20 lines): the hit that killed, or the reason of a removal without drops.</summary>
        public readonly List<string> DeathCauses = new List<string>();
        private string _lastKillCause;

        private void RecordDeath(int i, bool withDrops)
        {
            if (DeathCauses.Count >= 20) { _lastKillCause = null; return; }
            Vector3 p = Store.pos[i];
            DeathCauses.Add(withDrops
                ? $"worm {i} killed by {_lastKillCause ?? "an unknown hit"}"
                : $"worm {i} removed without drops at {p} (in box {InBox(p)}, state {(WormState)Store.state[i]})");
            _lastKillCause = null;
        }
        public readonly string[] WorstFrames = new string[3];
        private readonly float[] _worstMs = new float[3];

        private void RecordFrame(float now, int steps, bool gridRebuilt)
        {
            float ms = LastSimMs;
            if (_frameFirstSessionStrike) FirstSessionStrikeMs = ms;
            else if (ms > PeakMsWithoutFirstStrike) PeakMsWithoutFirstStrike = ms;
            _frameFirstSessionStrike = false;
            int slot = -1;
            for (int k = 0; k < 3; k++)
                if (ms > _worstMs[k]) { slot = k; break; }
            if (slot >= 0)
            {
                for (int k = 2; k > slot; k--) { _worstMs[k] = _worstMs[k - 1]; WorstFrames[k] = WorstFrames[k - 1]; }
                _worstMs[slot] = ms;
                WorstFrames[slot] = $"{ms:0.00} ms at t={now:0.00}: {steps} steps, {_framePaths} path requests, {_frameApproach} approach evaluations, " +
                                    $"{_frameQueries} grid queries, {_frameStrikes} strikes (sound/effect {_frameStrikeFxMs:0.00} ms, bite {_frameStrikeBiteMs:0.00} ms, " +
                                    $"strike number {_strikesSoFar + 1} of the run), grid rebuilt={gridRebuilt}, alive {Store.AliveCount}";
            }
            _strikesSoFar += _frameStrikes;
            _framePaths = _frameApproach = _frameQueries = _frameStrikes = 0;
            _frameStrikeFxMs = _frameStrikeBiteMs = 0f;
        }

        /// <summary>Forgets the peak and the worst frames (the scenario calls it after its own spawn frame and the second after it).</summary>
        public void ResetSimPeaks()
        {
            MaxSimMs = 0f;
            PeakMsWithoutFirstStrike = 0f;
            for (int k = 0; k < 3; k++) { _worstMs[k] = 0f; WorstFrames[k] = null; }
        }

        /// <summary>Restarts the drop generator from <paramref name="seed"/> (WormDrops.SeedForTests).</summary>
        public void ReseedDrops(int seed) => _dropRng = new System.Random(seed);
#endif

        /// <summary>Kills every worm without drops (mw_kill).</summary>
        public int KillAll()
        {
            if (Store == null) return 0;
            int n = 0;
            for (int i = 0; i < Store.HighWater; i++)
            {
                if (!Store.alive[i]) continue;
                KillWorm(i, false);
                n++;
            }
            return n;
        }

        private bool InBox(Vector3 p) => WormMath.InsideBox(p, BoxCenter, BoxHalfSize);

        // ------------------------------------------------------------------ rendering (spec §12.1)

        private const float StrikeDuration = 0.25f;   // spec §3.1
        private const float CrawlPhaseStep = 6.1f;     // frames between two worms of the 16 frame crawl (about the golden fraction of the cycle)
        private const float IdlePhaseStep = 0.9f;      // frames between two worms of the 8 frame idle
        private const float SleepPhaseStep = 0.7f;

        /// <summary>Position of worm <paramref name="i"/> interpolated between its last two simulation steps (render and hit proxy).</summary>
        public Vector3 InterpolatedPos(int i, float now)
        {
            WormStore s = Store;
            float interval = s.simInterval[i];
            float t = interval > 1e-4f ? Mathf.Clamp01((now - s.lastSimTime[i]) / interval) : 1f;
            return Vector3.Lerp(s.prevPos[i], s.pos[i], t);
        }

        public void CollectInstances(WormRenderer.Sink sink)
        {
            WormStore s = Store;
            if (!_initialized || s == null || s.AliveCount == 0) return;

            float now = Time.time;
            float windup = MWConfig.Worm.CobraWindup.Value;
            Vector3 cam = sink.CameraPosition;
            float cullDist = sink.RenderDistance + 0.5f;
            float cullSqr = cullDist * cullDist;
            float lodDist = Mathf.Max(12f, sink.ShadowDistance);   // beyond it crawl and idle use the 240 triangle LOD1 (and cast no shadow)
            float lodSqr = lodDist * lodDist;

            for (int i = 0; i < s.HighWater; i++)
            {
                if (!s.alive[i]) continue;
                var state = (WormState)s.state[i];
                if (state == WormState.Absorbed || state == WormState.Dead) continue;

                float since = now - s.lastSimTime[i];
                float interval = s.simInterval[i];
                Vector3 p = InterpolatedPos(i, now);

                float dx = p.x - cam.x, dy = p.y - cam.y, dz = p.z - cam.z;
                float dSqr = dx * dx + dy * dy + dz * dz;
                if (dSqr > cullSqr) continue;   // the renderer would cull it anyway; skip the matrix

                // Look variation, stable for a given worm: scale and a roll around the body axis (render only, the proxy keeps its size).
                WormMath.Variation(i, s.generation[i], out float scale, out float rollDeg);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, s.groundNormal[i])
                                 * Quaternion.Euler(0f, s.heading[i] * Mathf.Rad2Deg, 0f)
                                 * Quaternion.AngleAxis(rollDeg, Vector3.forward);

                WormAnim anim = WormAnim.Crawl;
                int frame;
                switch (state)
                {
                    case WormState.Sleeping:
                        anim = WormAnim.Sleep;
                        frame = sink.FrameAt(anim, now, i * SleepPhaseStep);
                        break;
                    case WormState.Striking:
                    {
                        float st = s.stateTime[i] + Mathf.Min(since, interval);
                        if (st < windup)
                        {
                            // Rear up over the windup, once.
                            anim = WormAnim.Rear;
                            int frames = sink.FrameCount(anim);
                            frame = windup > 1e-3f ? Mathf.Min(frames - 1, (int)(st / windup * frames)) : frames - 1;
                        }
                        else if (st < windup + StrikeDuration)
                        {
                            anim = WormAnim.Strike;
                            frame = sink.FrameAt(anim, st - windup);
                        }
                        else
                        {
                            frame = sink.FrameAt(anim, s.animTime[i], i * CrawlPhaseStep); // recovery: crawl pose, frozen
                        }
                        break;
                    }
                    default:
                        if (WormMath.UseIdlePose(now, s.stillSince[i]))
                        {
                            anim = WormAnim.Idle;
                            frame = sink.FrameAt(anim, now, i * IdlePhaseStep);
                        }
                        else
                        {
                            // animTime only advances while the worm really moves.
                            frame = sink.FrameAt(anim, s.animTime[i], i * CrawlPhaseStep);
                        }
                        break;
                }

                Matrix4x4 m = Matrix4x4.TRS(p, rot, new Vector3(scale, scale, scale));
                if (dSqr > lodSqr && (anim == WormAnim.Crawl || anim == WormAnim.Idle))
                    sink.AddWorm(anim, WormLod.Lod1, frame >> 1, m);   // LOD1 frame j is pose 2j of LOD0: the phase stays continuous
                else
                    sink.AddWorm(anim, frame, m);
            }
        }
    }
}
