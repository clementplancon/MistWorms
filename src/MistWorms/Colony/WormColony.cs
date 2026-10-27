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
#if MW_DEV
        /// <summary>Dev freeze of every colony (mw_freeze): worms stay where they are.</summary>
        public static bool Frozen;
#endif

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

        /// <summary>
        /// Stable identity of this colony (<see cref="MineKey"/>), 0 until it is configured. Nests and masses refer to their colony by it, never by <see cref="ZdoId"/>, which Valheim
        /// changes at every load of the world (decision 211).
        /// </summary>
        public long Key { get; private set; }

        /// <summary>Network time (ticks) of the creation of this colony, 0 = unknown (written before the key existed).</summary>
        public long CreatedTicks { get; private set; }

        /// <summary>The data that decides the deduplication (<see cref="MineOwnership.CompareKeeper"/>), read from the ZDO.</summary>
        public KeeperCandidate Candidate
        {
            get
            {
                ZDOID id = ZdoId;
                ZDO zdo = _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
                return new KeeperCandidate
                {
                    Seeded = Seeded,
                    Worms = zdo != null ? SnapshotFormat.PeekCount(zdo.GetByteArray(HashSnapshot, null)) : 0,
                    CreatedTicks = CreatedTicks,
                    User = id.UserID,
                    Id = id.ID,
                };
            }
        }

        // ------------------------------------------------------------------ creation

        /// <summary>Instantiates a colony at <paramref name="position"/> with its box; null if the prefab is not registered.</summary>
        public static WormColony Create(Vector3 position, ColonyKind kind, Vector3 boxCenter, Vector3 boxHalfSize)
        {
            if (!ColonyRegistry.ServerMayHost) return null;   // a dedicated server creates no colony
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
            if (Key == 0L)
            {
                // A mine: the same key whoever creates it and in every session (the colony sits at the root of the dungeon). A test colony: any.
                Vector3 root = transform.position;
                Key = kind == ColonyKind.Mine ? MineKey.FromPosition(root.x, root.y, root.z) : MineKey.NewRandom();
                CreatedTicks = ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
            }
            _configured = true;

            ZDO zdo = _nview.GetZDO();
            if (zdo != null && zdo.IsOwner())
            {
                zdo.Set(MWNames.ZdoKind, (int)kind);
                zdo.Set(MWNames.ZdoBoxCenter, boxCenter);
                zdo.Set(MWNames.ZdoBoxExtents, boxHalfSize);
                zdo.Set(MWNames.ZdoColonyKey, Key);
                if (CreatedTicks != 0L) zdo.Set(MWNames.ZdoCreated, CreatedTicks);
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
            if (_initialized && !_snapClosed) WriteSnapshotOnDestroy();   // a zone unload keeps the ZDO: leave the worms in it
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
#if MW_DEV
            _rng = new System.Random(DevSeed.HasValue ? DevSeed.Value : Environment.TickCount ^ GetInstanceID());
#else
            _rng = new System.Random(Environment.TickCount ^ GetInstanceID());
#endif
            _dropRng = WormDrops.NewRng(GetInstanceID());
            _nview.Register<int, ZPackage>(MWNames.RpcWormHit, RPC_WormHit);
            InitNet(capacity);
            _nview.Register<ZDOID, int, Vector3, ZPackage, bool>(MWNames.RpcMassDissolve, RPC_MassDissolve);
            _nview.Register<int, Vector3, bool>(MWNames.RpcNestGone, RPC_NestGone);
            _nview.Register<ZPackage, Vector3>(MWNames.RpcSnapAbsorb, RPC_SnapAbsorb);
            InitNests();
            InitWake();
            InitPaths();
            InitTargetCache(capacity);
            InitMassLink(capacity);

            ZDO zdo = _nview.GetZDO();
            _zdo = zdo;
            Kind = (ColonyKind)zdo.GetInt(MWNames.ZdoKind, 0);
            Vector3 half = zdo.GetVec3(MWNames.ZdoBoxExtents, Vector3.zero);
            if (half != Vector3.zero)
            {
                // A reloaded colony: the box is persisted (spec §8.4) and its worms come back from mw_snap on the first owner tick (WormSnapshot.cs).
                BoxCenter = zdo.GetVec3(MWNames.ZdoBoxCenter, transform.position);
                BoxHalfSize = half;
                _boxBounds = new Bounds(BoxCenter, half * 2f);
                _configured = true;

                // The identity is in the ZDO. A colony saved before the key existed gets the one its position gives (a mine is created at the root of its dungeon), and the owner writes it.
                Key = zdo.GetLong(MWNames.ZdoColonyKey, 0L);
                CreatedTicks = zdo.GetLong(MWNames.ZdoCreated, 0L);
                if (Key == 0L)
                {
                    Vector3 root = zdo.GetPosition();
                    Key = MineKey.FromPosition(root.x, root.y, root.z);
                    if (zdo.IsOwner()) zdo.Set(MWNames.ZdoColonyKey, Key);
                    Plugin.Log.LogInfo($"[Colony] colony {ZdoId} (saved without a key): key {MineKey.Describe(Key)} from its position {root}");
                }
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
            if (!ColonyRegistry.ServerMayHost) return;   // a dedicated server hosts no colony: it routes (spec §10.2, decision 215)

            if (!_nview.IsOwner())
            {
                // Not the owner: the worms are not simulated here, they are the mirror of what the owner sends (spec §11.3, phase 8).
                _mirrorMode = true;
                _wakeAdopted = false;   // a later owner starts from the awake bits in the ZDO
                if (_snapSettled) { HandOffSnapshot(); ResetNetSend(); _needKeyframe = true; }   // its worms are the new owner's, restored from mw_snap
                if (_absorbQueue.Count > 0) ForwardAbsorbQueue();   // the snapshots of duplicate colonies it had not merged yet go to the new owner
                try
                {
                    TickMirror(Time.time);
                }
                catch (Exception e)
                {
                    Plugin.LogException("WormColony.TickMirror", e);
                }
                return;
            }
            if (_mirrorMode)
            {
                // Just became the owner: the copy of the owner before us goes, the snapshot restores the real worms below.
                _mirrorMode = false;
                DropMirrorForOwnership();
            }
#if MW_DEV
            if (Frozen)
            {
                try { TickNet(Time.time); }   // a frozen colony still shows its worms to the peers (and keeps them alive on their side)
                catch (Exception e) { Plugin.LogException("WormColony.TickNet", e); }
                return;
            }
#endif

            try
            {
                // The worms of a reloaded colony come back from mw_snap before anything else runs (spec §11.4).
                if (!_snapSettled && !SettleSnapshot(Time.time)) return;
#if MW_DEV
                long heap0 = DevMeasureAlloc ? UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() : 0L;
                long vanilla0 = DevVanillaBytes;
#endif
                Tick();
                UpdateIdleSounds(Time.time);
                TickSnapshot(Time.time);
                TickNet(Time.time);
#if MW_DEV
                if (DevMeasureAlloc)
                {
                    long rise = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - heap0;
                    rise -= DevVanillaBytes - vanilla0;   // the vanilla damage call is counted apart (DevVanillaBytes), not as the mod's
                    if (rise > 0) DevAllocBytes += rise;
                    DevAllocFrames++;
                }
#endif
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
            _snapClosed = true;   // the ZDO goes with the object: nothing to leave in it
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
                s.den[i] = hit.point;
                s.progressPos[i] = hit.point;
            }
            // Spread the first steps and de-synchronise the wandering and the crawl animation.
            s.nextSimTime[i] = now + SimPhase(i);
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
                if (!_mirrorMode) EmitNetEvent(DeltaEventType.Death, where + Vector3.up * 0.1f, SessionUid);   // the peers near play it too (this one just did)
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
        private float _frameVanillaMs;

        /// <summary>
        /// Worst simulation frame once the time spent INSIDE the vanilla damage call of a strike (Character.Damage and what it runs, game code) is taken out, and the first strike of the
        /// session as before. Decision 255: a strike on the player is 3 ms in one frame of 0.08 ms of simulation, a cost of the game that the mod does not control.
        /// </summary>
        public float PeakMsWithoutVanilla { get; private set; }

        /// <summary>Largest time (ms) one frame spent in the vanilla damage call of its strikes since the last reset, reported apart from the budget.</summary>
        public float WorstVanillaMs { get; private set; }
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
            if (!_frameFirstSessionStrike && ms - _frameVanillaMs > PeakMsWithoutVanilla) PeakMsWithoutVanilla = ms - _frameVanillaMs;
            if (_frameVanillaMs > WorstVanillaMs) WorstVanillaMs = _frameVanillaMs;
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
            _frameStrikeFxMs = _frameStrikeBiteMs = _frameVanillaMs = 0f;
        }

        /// <summary>Forgets the peak and the worst frames (the scenario calls it after its own spawn frame and the second after it).</summary>
        public void ResetSimPeaks()
        {
            MaxSimMs = 0f;
            PeakMsWithoutFirstStrike = 0f;
            PeakMsWithoutVanilla = 0f;
            WorstVanillaMs = 0f;
            for (int k = 0; k < 3; k++) { _worstMs[k] = 0f; WorstFrames[k] = null; }
        }

        /// <summary>Restarts the drop generator from <paramref name="seed"/> (WormDrops.SeedForTests).</summary>
        public void ReseedDrops(int seed) => _dropRng = new System.Random(seed);

        /// <summary>Seed of the simulation generator of the colonies created from now on (ScenarioReset: a scenario starts from the same sequence alone or in a series); null = time based.</summary>
        public static int? DevSeed;

        /// <summary>Restarts the simulation generator of this colony from <paramref name="seed"/>.</summary>
        public void ReseedRng(int seed)
        {
            _rng = new System.Random(seed);
            _seedStart = 0;   // where the cluster search of the formation starts: part of the same sequence
        }

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
            ResetWake();
#if MW_DEV
            _reserve?.Clear();   // mw_kill is a complete reset: the loot reserve (what strikes ate for worms that are gone) goes too
#endif
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
            if (_mirrorMode) return s.pos[i];   // the mirror already puts the slot where it is shown (WormMirror.Tick)
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
