using System;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Drops;
using MistWorms.Localization;
using MistWorms.Render;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// Network state of a writhing mass (spec §4.1, §4.2, §11.3, plan 4B-1). The vanilla health of the Character is the truth of the
    /// owner: after every <c>Character.ApplyDamage</c> the number of worms is derived from it (<see cref="MassHealth"/>) and written to the ZDO
    /// (<c>mw_n</c>, <c>mw_partial</c>), where every peer reads it for the name, the hit sphere, the render and the loss effects.
    /// A mass never heals and has no level: its health is exactly n x Worm.Health (decisions.md 85).
    /// </summary>
    internal sealed partial class WritheMassState : MonoBehaviour
    {
        /// <summary>The mass disappears this long after it dissolved into fleeing worms (spec §4.4: the dome shrinks in 0.4 s).</summary>
        public const float DissolveDestroyDelay = 0.4f;

        private const float ReconcilePeriod = 0.5f;
        private const string HitVolumeName = "HitVolume";

        private static readonly int HashN = MWNames.ZdoMassN.GetStableHashCode();
        private static readonly int HashPartial = MWNames.ZdoMassPartial.GetStableHashCode();
        private static readonly int HashState = MWNames.ZdoMassState.GetStableHashCode();
        private static readonly int HashBorn = MWNames.ZdoMassBorn.GetStableHashCode();
        private static readonly int HashStreamWidth = MWNames.ZdoMassStreamWidth.GetStableHashCode();

        private static readonly Dictionary<Character, WritheMassState> s_byCharacter = new Dictionary<Character, WritheMassState>();
        private static readonly List<WritheMassState> s_all = new List<WritheMassState>();

        /// <summary>The masses loaded on this peer. Do not add or remove while iterating.</summary>
        public static List<WritheMassState> All => s_all;

        /// <summary>The mass that owns <paramref name="c"/>, if it is one. Used by every patch to stay inert on other creatures.</summary>
        public static bool TryGet(Character c, out WritheMassState state)
        {
            state = null;
            return c != null && s_byCharacter.TryGetValue(c, out state) && state != null;
        }

        private Character _character;
        private ZNetView _nview;
        private Rigidbody _body;
        private WritheMassAI _ai;
        private CapsuleCollider _capsule;
        private readonly GameObject[] _hitGos = new GameObject[MassStream.MaxChainSpheres];
        private readonly SphereCollider[] _hits = new SphereCollider[MassStream.MaxChainSpheres];
        private int _chainCount;
        private int _hitN = -1;
        private bool _hitStream;
        private float _hitWidth = -1f;
        private bool _dissolving;
        private bool _imposed;
        private float _nextReconcile;
        private string _hoverName;
        private int _hoverN = -1;
        private string _hoverLanguage;

        /// <summary>Dev switch (plan 4B-1 point 8): the AI of this mass does nothing. Honoured by <see cref="WritheMassAI"/>.</summary>
        public bool FreezeAI { get; set; }

        public Character Character => _character;
        public WritheMassAI Ai => _ai;
        public bool Valid => _nview != null && _nview.IsValid();
        public bool IsOwner => Valid && _nview.IsOwner();
        public bool Dissolving => _dissolving;

        /// <summary>Why this mass is being destroyed (set by whoever destroys it); logged in dev builds when it goes.</summary>
        public string DestroyReason { get; set; }
#if MW_DEV
        /// <summary>Prefab name of the last creature that hit this mass on this peer (dev journal of its disappearance).</summary>
        public string LastAttacker { get; private set; }
        private string _idText = "?", _lastState = "";
        private int _lastN;
        private float _lastHealth;

        public void NoteHit(HitData hit)
        {
            Character a = hit != null ? hit.GetAttacker() : null;
            LastAttacker = a != null ? Utils.GetPrefabName(a.gameObject) : (hit != null && hit.HaveAttacker() ? "an attacker not found" : "no attacker");
        }
#endif

#if MW_DEV
        /// <summary>Calls of Character.ApplyDamage seen on this mass (hits and burning ticks): a single melee swing must count once.</summary>
        public int DamageCalls { get; private set; }
#endif
        public ZDOID ZdoId => Valid ? _nview.GetZDO().m_uid : ZDOID.None;

        // ------------------------------------------------------------------ ZDO state

        /// <summary>Worms in the mass (spec §4.2).</summary>
        public int N => Valid ? _nview.GetZDO().GetInt(HashN, 0) : 0;

        /// <summary>Damage dealt toward the next worm, in [0, Worm.Health).</summary>
        public float Partial => Valid ? _nview.GetZDO().GetFloat(HashPartial, 0f) : 0f;

        /// <summary>Raw <c>mw_state</c> (see <see cref="MassStateCodes"/>).</summary>
        public byte StateCode => Valid ? (byte)_nview.GetZDO().GetInt(HashState, 0) : (byte)0;

        /// <summary>Network time of the birth, in ticks (growth animation, phase 4B-4); 0 = unknown.</summary>
        public long BornTicks => Valid ? _nview.GetZDO().GetLong(HashBorn, 0L) : 0L;

        public ZDOID ColonyId => Valid ? _nview.GetZDO().GetZDOID(MWNames.ZdoMassColony) : ZDOID.None;

        /// <summary>The Stream bit of <c>mw_state</c>: the mass flows through a narrow passage as a ribbon (spec 4.7).</summary>
        public bool IsStream => MassStateCodes.IsStream(StateCode);

        /// <summary>Ribbon width while streaming (mw_sw), 0 otherwise.</summary>
        public float StreamWidth => Valid ? _nview.GetZDO().GetFloat(HashStreamWidth, 0f) : 0f;

        /// <summary>Ribbon length for the current count (config dependent, same on every peer).</summary>
        public float RibbonLength => MassStream.RibbonLength(N, MWConfig.Mass.StreamLengthPerWorm.Value, MWConfig.Mass.StreamMaxLength.Value, StreamWidth);

        /// <summary>The creature the mass hunts (<c>mw_target</c>), or ZDOID.None.</summary>
        public ZDOID TargetId => Valid ? _nview.GetZDO().GetZDOID(MWNames.ZdoMassTarget) : ZDOID.None;

        /// <summary>Radius the dome should have for the current count (the displayed radius smooths toward it).</summary>
        public float TargetRadius => MassMath.Radius(Math.Max(1, N));

        public float Health => _character != null && Valid ? _character.GetHealth() : 0f;

        // ------------------------------------------------------------------ creation

        /// <summary>
        /// Instantiates a mass of <paramref name="n"/> worms standing at <paramref name="position"/> (the floor). The calling peer owns it.
        /// Returns null when the prefab is not registered. Used by the colony (<paramref name="newborn"/>: a formation, the heap grows from nothing in 0.5 s) and by the dev tools.
        /// </summary>
        public static WritheMassState Create(Vector3 position, int n, ZDOID colony, bool newborn = false)
        {
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(MWNames.MassPrefab) : null;
            if (prefab == null)
            {
                Plugin.Log.LogError($"[Mass] prefab {MWNames.MassPrefab} is not registered in the ZNetScene");
                return null;
            }

            GameObject go = Instantiate(prefab, SafeSpawnPosition(position), Quaternion.identity);
            var state = go.GetComponent<WritheMassState>();
            if (state == null || !state.Valid)
            {
                Plugin.Log.LogError("[Mass] the instantiated mass has no valid network view");
                if (ZNetScene.instance != null) ZNetScene.instance.Destroy(go);
                else Destroy(go);
                return null;
            }
            state.Initialize(n, colony, newborn);
            return state;
        }

        private const float SpawnProbeUp = 1.5f;
        private const float SpawnProbeDown = 5f;
        private const float SpawnLift = 0.1f;

        /// <summary>
        /// Where a mass really appears: 0.1 m above the floor found by a ray down from 1.5 m above <paramref name="position"/> (lowered under a
        /// ceiling), never inside the geometry: a body created in the thin floor of a dungeon falls through it. Applies to every creation.
        /// </summary>
        public static Vector3 SafeSpawnPosition(Vector3 position)
        {
            int mask = WormPhysics.CharacterSolidMask;
            float up = SpawnProbeUp;
            if (Physics.Raycast(position + Vector3.up * SpawnLift, Vector3.up, out RaycastHit ceiling, SpawnProbeUp, mask, QueryTriggerInteraction.Ignore))
                up = Mathf.Max(SpawnLift, ceiling.distance - 0.1f);
            Vector3 from = position + Vector3.up * up;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit floor, up + SpawnProbeDown, mask, QueryTriggerInteraction.Ignore))
                return floor.point + Vector3.up * SpawnLift;
            return position + Vector3.up * SpawnLift;
        }

        /// <summary>Fresh mass: writes the ZDO fields and imposes the exact health n x Worm.Health. Owner only.</summary>
        public void Initialize(int n, ZDOID colony, bool newborn = false)
        {
            if (!IsOwner) return;
            n = Math.Max(1, Math.Min(n, MWConfig.Mass.MaxCount.Value));
            ZDO zdo = _nview.GetZDO();
            zdo.Set(HashN, n);
            zdo.Set(HashPartial, 0f);
            zdo.Set(HashState, (int)MassStateCodes.Idle);
            zdo.Set(MWNames.ZdoMassColony, colony);
            // The growth animation (spec §12.4) is for a mass born of a formation; a dev mass appears at its size (mw_born 0 = unknown).
            zdo.Set(HashBorn, newborn && ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L);
            zdo.Set(MWNames.ZdoMassPool, Array.Empty<byte>());
            ImposeHealth(n, 0f);
            _imposed = true;
        }

        // ------------------------------------------------------------------ life cycle

        private void Awake()
        {
            _character = GetComponent<Character>();
            _nview = GetComponent<ZNetView>();
            _ai = GetComponent<WritheMassAI>();
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();

            s_all.Add(this);
            if (_character != null) s_byCharacter[_character] = this;

            if (_nview != null) _nview.Register<int, ZPackage>(MWNames.RpcMassAdd, RPC_MassAdd);

            ApplyConfig();
            CreateHitVolume();
        }

        private void Start()
        {
            if (!Valid) return;
            if (IsOwner && !_imposed)
            {
                int n = N;
                if (n > 0)
                {
                    // Character.Awake relaunched SetupMaxHealth for an owner at full health (decisions.md 1.3), which also lowers a stored
                    // full health to the prefab value: the ZDO fields are the truth at (re)load, so health is set, not only capped.
                    ImposeHealth(n, Partial);
                    _imposed = true;
                }
                else if (_character != null && !_character.IsDead())
                {
                    Plugin.Log.LogWarning($"[Mass] {_nview.GetZDO().m_uid} has no worm count (mw_n = 0): destroyed");
                    DestroyReason = "no worm count (mw_n = 0) at load";
                    DestroyMass();
                }
            }
            UpdateHitVolume(N, IsStream, StreamWidth);
        }

        private void OnDestroy()
        {
            if (_ignoredColliders.Count > 0) RestoreCollisions();
            if (_ai != null) _ai.ReleaseOnDestroy();
#if MW_DEV
            // The journal of the disappearance of a mass (a mass that vanishes in a scenario is otherwise a mystery): why, the last creature that hit it, its last state.
            Plugin.Log.LogInfo($"[Mass] {_idText} (n={_lastN}) removed: {DestroyReason ?? "unknown (the scene, a zone unload or a destroy that did not say why)"}; last hit by {LastAttacker ?? "nobody"}; health {_lastHealth:0.#}; state {_lastState}");
#endif
            s_all.Remove(this);
            if (_character != null) s_byCharacter.Remove(_character);
        }

        private void Update()
        {
            if (!Valid) return;

            int n = N;
#if MW_DEV
            _lastN = n; _lastHealth = Health; _lastState = MassStateCodes.Describe(StateCode); _idText = ZdoId.ToString();
#endif
            bool stream = IsStream;
            float width = stream ? StreamWidth : 0f;
            if (n != _hitN || stream != _hitStream || Math.Abs(width - _hitWidth) > 0.01f)
                UpdateHitVolume(n, stream, stream ? Math.Max(width, MassStream.MinRibbonWidth) : 0f);

            if (IsOwner && !_dissolving && Time.time >= _nextReconcile)
            {
                _nextReconcile = Time.time + ReconcilePeriod;
                Reconcile();
            }
            PoolTick();
            MergeTick(Time.time);
            EngulfCollisionTick(Time.time);
        }

        // ------------------------------------------------------------------ fall safety net (owner)

        private const float GroundStableSeconds = 0.5f;
        private const float RescueLookDepth = 1f;         // below the last stable ground, from which the rule is looked at (the rule itself: MassRescue)
        private const float RescueLookSpeed = 10f;        // downward, from which the rule is looked at

        private Vector3 _lastGround;
        private bool _haveGround;
        private float _groundSince = -1f;

        /// <summary>Times the mass was put back on its last stable ground after falling out of the world (a floor that was not there).</summary>
        public int FallRescues { get; private set; }

        /// <summary>
        /// The body was moved on purpose to <paramref name="position"/> (a dev placement): the fall rescue forgets the ground it remembered, which belongs to the old place.
        /// Without it a mass put on a floor more than 3 m lower looked like a fall and was put back where it came from.
        /// </summary>
        public void TeleportedTo(Vector3 position)
        {
            _lastGround = position;
            _haveGround = false;
            _groundSince = -1f;
        }

        /// <summary>Dev: sinks the body below where it stands, to test the rescue.</summary>
        public void DevSink(float depth)
        {
            if (_body != null) _body.position = _body.position - Vector3.up * depth;
        }

        private const float GroundProbeAbove = 0.2f;
        private const float GroundProbeReach = 0.5f;      // a floor must lie within 0.3 m under the mass
        private const float OutdoorsHeight = 3000f;       // interiors sit far above the world (y > 3000)
        private const float TerrainMargin = 0.5f;

        /// <summary>
        /// A position is only remembered as ground when a ray down finds a floor within 0.3 m under it and, outdoors, it is not under the
        /// terrain height by more than 0.5 m (a body that already fell through the heightmap still reports a ground contact for a moment).
        /// </summary>
        private static bool GroundConfirmed(Vector3 pos)
        {
            if (!Physics.Raycast(pos + Vector3.up * GroundProbeAbove, Vector3.down, GroundProbeReach, WormPhysics.CharacterSolidMask, QueryTriggerInteraction.Ignore)) return false;
            if (pos.y < OutdoorsHeight && ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out float terrain) && pos.y < terrain - TerrainMargin) return false;
            return true;
        }

        private void FixedUpdate()
        {
            if (!IsOwner || _character == null || _body == null) return;
            float now = Time.time;
            if (_merging) StepMerge(now);

            if (_character.IsOnGround() && GroundConfirmed(transform.position))
            {
                if (_groundSince < 0f) _groundSince = now;
                if (now - _groundSince >= GroundStableSeconds)
                {
                    _lastGround = transform.position;
                    _haveGround = true;
                }
            }
            else
            {
                _groundSince = -1f;
            }
            if (!_haveGround) return;

            Vector3 pos = transform.position;
            float vy = _body.linearVelocity.y;
            // Cheap filter first: only a mass that is clearly lower than its ground or falling fast is looked at (a ray every physics step per mass is not needed).
            if (pos.y >= _lastGround.y - RescueLookDepth && vy >= -RescueLookSpeed) return;
            bool floorBelow = Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, MassRescue.FloorProbe, WormPhysics.CharacterSolidMask, QueryTriggerInteraction.Ignore);
            bool outdoors = pos.y < OutdoorsHeight;
            float terrain = 0f;
            bool hasTerrain = outdoors && ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(pos, out terrain);
            if (!MassRescue.Needed(floorBelow, outdoors, hasTerrain, pos.y, terrain, vy)) return;

            FallRescues++;
            Plugin.Log.LogWarning($"[Mass] {ZdoId} left the world (y {pos.y:0.0}, last ground {_lastGround}, speed {vy:0.0} m/s, floor below {floorBelow}, terrain {(hasTerrain ? terrain.ToString("0.0") : "n/a")}): put back, rescue {FallRescues}");
            _body.position = _lastGround + Vector3.up * SpawnLift;
            transform.position = _body.position;
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
            _groundSince = -1f;
        }

        /// <summary>
        /// Config dependent values, applied on every peer when the instance wakes up (the prefab is built before the synced config arrives):
        /// speeds, the resistance table of the worms (spec §3.2), the collider bound (spec §4.5) and the nominal health.
        /// </summary>
        private void ApplyConfig()
        {
            if (_character == null) return;
            float speed = MWConfig.Mass.MoveSpeed.Value;
            _character.m_speed = speed;
            _character.m_walkSpeed = speed;
            _character.m_runSpeed = speed;
            _character.m_health = MWConfig.Worm.Health.Value;
            _character.m_damageModifiers = MWConfig.BuildDamageModifiers();

            if (_capsule != null)
            {
                float bound = Mathf.Max(0.05f, MWConfig.Mass.MaxColliderRadius.Value);
                if (_capsule.radius > bound)
                {
                    Vector3 c = _capsule.center;
                    c.y -= _capsule.radius - bound;       // the sphere keeps resting on the floor
                    _capsule.center = c;
                    _capsule.radius = bound;
                }
            }
        }

        // ------------------------------------------------------------------ hit volume (spec §4.5, decisions.md 78)

        /// <summary>
        /// A child sphere on the proxy layer (hitbox) with no IDestructible of its own: vanilla melee, projectiles and Aoe find the Character
        /// through GetComponentInParent&lt;IDestructible&gt; (Projectile.FindHitObject) and count one hit per root. It never collides physically.
        /// </summary>
        private void CreateHitVolume()
        {
            // Hits come from the local client; a dedicated server has nobody to hit anything.
            if (Application.isBatchMode) return;
            ProxyLayer.Resolve();

            // The first sphere is the dome sphere; the others only exist (enabled) along the ribbon of a stream (spec 4.7). All preallocated.
            for (int i = 0; i < _hits.Length; i++)
            {
                var go = new GameObject(i == 0 ? HitVolumeName : HitVolumeName + i);
                go.transform.SetParent(transform, false);
                go.layer = ProxyLayer.Layer;
                var sphere = go.AddComponent<SphereCollider>();
                sphere.isTrigger = false;
                if (_capsule != null) Physics.IgnoreCollision(sphere, _capsule, true);
                if (i > 0) sphere.enabled = false;
                _hitGos[i] = go;
                _hits[i] = sphere;
            }
            _chainCount = 1;
            if (ProxyLayer.NeedsIgnoreCollision) IgnoreLocalPlayer();

            // A big child sphere would pull the automatic centre of mass of the body up: keep it in the capsule.
            if (_body != null && _capsule != null) _body.centerOfMass = _capsule.center;
        }

        private void IgnoreLocalPlayer()
        {
            Player player = Player.m_localPlayer;
            if (player == null || _hits[0] == null) return;
            foreach (Collider c in player.GetComponentsInChildren<Collider>(true))
            {
                if (c == null) continue;
                for (int i = 0; i < _hits.Length; i++) Physics.IgnoreCollision(_hits[i], c, true);
            }
        }

        /// <summary>
        /// Dome: one sphere of 0.9 r(n) at 0.4 r(n). Stream: a chain of up to 6 spheres along the ribbon (the local z axis, which is the
        /// forward of the mass) at the height of the flattened body (spec 4.5, 4.7).
        /// </summary>
        private void UpdateHitVolume(int n, bool stream, float width)
        {
            _hitN = n;
            _hitStream = stream;
            _hitWidth = width;
            if (_hits[0] == null) return;
            float r = MassMath.Radius(Math.Max(1, n));

            if (!stream)
            {
                _chainCount = 1;
                _hits[0].radius = MassMath.HitRadius(r);
                _hitGos[0].transform.localPosition = new Vector3(0f, MassMath.HitCenterHeight(r), 0f);
            }
            else
            {
                float length = MassStream.RibbonLength(n, MWConfig.Mass.StreamLengthPerWorm.Value, MWConfig.Mass.StreamMaxLength.Value, width);
                float sphere = MassStream.SphereRadius(width);
                float height = MassMath.DomeHeightFactor * r * MWConfig.Mass.StreamHeightFactor.Value;
                float y = Math.Max(0.25f, 0.45f * height);
                _chainCount = MassStream.ChainCount(length, sphere);
                for (int i = 0; i < _chainCount; i++)
                {
                    _hits[i].radius = sphere;
                    _hitGos[i].transform.localPosition = new Vector3(0f, y, MassStream.ChainOffset(i, _chainCount, length));
                }
            }
            for (int i = 0; i < _hits.Length; i++) _hits[i].enabled = i < _chainCount;
        }

        /// <summary>Number of active hit spheres (1 in the dome, 1 to 6 along a stream), 0 on a server.</summary>
        public int HitSphereCount => _hits[0] != null ? _chainCount : 0;

        /// <summary>Hit sphere <paramref name="index"/> (world centre and radius), for the dev tools and scenarios; false on a server or past the chain.</summary>
        public bool TryGetHitSphere(int index, out Vector3 center, out float radius)
        {
            center = default;
            radius = 0f;
            if (index < 0 || index >= _chainCount || _hits[index] == null) return false;
            center = _hitGos[index].transform.position;
            radius = _hits[index].radius;
            return true;
        }

        public bool TryGetHitSphere(out Vector3 center, out float radius) => TryGetHitSphere(0, out center, out radius);

        public SphereCollider HitCollider => _hits[0];

        public SphereCollider HitColliderAt(int index) => index >= 0 && index < _chainCount ? _hits[index] : null;

        // ------------------------------------------------------------------ health model (owner)

        /// <summary>Postfix of <c>Character.ApplyDamage</c> (see MassPatches): derives n and partial from the new health.</summary>
        public void OnDamageApplied()
        {
#if MW_DEV
            DamageCalls++;
#endif
            Reconcile();
        }

        /// <summary>
        /// Brings n and partial in line with the vanilla health (spec §4.2): n = ceil(health / H - 1e-4), partial = n x H - health. D is
        /// already reduced by resistances, blocking and difficulty by the vanilla path, so this is the algorithm of §4.2 on the final damage.
        /// A health above what n and partial allow (a heal) is brought back down: a mass has no regeneration. Idempotent.
        /// </summary>
        private void Reconcile()
        {
            if (!IsOwner || _dissolving || _character == null) return;
            float perWorm = MWConfig.Worm.Health.Value;
            float health = _character.GetHealth();
            int n = N;
            if (n <= 0) return;

            if (health <= 0f)
            {
                DestroyReason = $"killed: health {health:0.#} (vanilla death), last hit by {LastAttackerText()}";
                // Vanilla CheckDeath follows; clients see the last worms go.
                WriteCount(0, 0f);
                PoolOnDeath();   // the last worm hands out everything left (spec Â§7.3)
                return;
            }

            float expected = MassHealth.HealthFor(n, Partial, perWorm);
            if (health > expected + MassHealth.HealthTolerance)
            {
                _character.SetHealth(expected);
                return;
            }

            int newN = Math.Max(1, Math.Min(MassHealth.CountFor(health, perWorm), n));
            float newPartial = MassHealth.PartialFor(newN, health, perWorm);
            if (newN == n && Math.Abs(newPartial - Partial) <= MassHealth.HealthTolerance) return;

            if (newN != n) _character.SetMaxHealth(MassHealth.MaxHealthFor(newN, perWorm));
            WriteCount(newN, newPartial);

            int kills = n - newN;
            if (kills > 0) PoolOnLoss(kills, n);   // the dead worms' share of the pool, before a dissolution hands the rest over
            if (kills > 0 && newN < MWConfig.Mass.DissolveThreshold.Value) BeginDissolve(newN);
        }

        /// <summary>
        /// Adds worms (absorption, fusion). The owner caps at Mass.MaxCount and returns the number accepted; any other peer forwards the
        /// request to the owner and returns -1 (the owner decides).
        /// </summary>
        public int AddWorms(int k, MassPool donor = null)
        {
            if (k <= 0 || !Valid) return 0;
            if (!_nview.IsOwner())
            {
                _nview.InvokeRPC(MWNames.RpcMassAdd, k, DonorPackage(donor));
                if (donor != null) donor.Clear();   // the pool left with the worms
                return -1;
            }
            if (_dissolving || _merging || _character == null || _character.IsDead()) return 0;

            int n = N;
            int accepted = Math.Max(0, Math.Min(k, MWConfig.Mass.MaxCount.Value - n));
            if (accepted == 0) return 0;

            float partial = Partial;
            ImposeHealth(n + accepted, partial);
            WriteCount(n + accepted, partial);
            TakeDonorPool(donor, accepted, k);   // the pool comes with the worms (fraction accepted/k when the cap cut the group)
            return accepted;
        }

        private void RPC_MassAdd(long sender, int k, ZPackage pool)
        {
            try
            {
                if (!s_poolRpcIn.Read(pool))
                {
                    Plugin.Log.LogWarning($"[Pool] mass {ZdoId}: unreadable pool in MW_MassAdd, ignored");
                    s_poolRpcIn.Clear();
                }
                // Validation: no peer adds more worms than a mass can hold. Refused worms are gone, their pool is not: it is dropped (same result whoever owns the mass).
                if (k > 0 && k <= MWConfig.Mass.MaxCount.Value && IsOwner) AddWorms(k, s_poolRpcIn);
                DropLeftover(s_poolRpcIn);
            }
            catch (Exception e)
            {
                Plugin.LogException("WritheMassState.RPC_MassAdd", e);
            }
        }

        /// <summary>max = n x H first, then health = n x H - partial (SetHealth does not cap from above, SetMaxHealth only lowers it).</summary>
        private void ImposeHealth(int n, float partial)
        {
            if (_character == null) return;
            float perWorm = MWConfig.Worm.Health.Value;
            _character.SetMaxHealth(MassHealth.MaxHealthFor(n, perWorm));
            _character.SetHealth(MassHealth.HealthFor(n, partial, perWorm));
        }

        private void WriteCount(int n, float partial)
        {
            ZDO zdo = _nview.GetZDO();
            if (zdo.GetInt(HashN, 0) != n) zdo.Set(HashN, n);
            if (Math.Abs(zdo.GetFloat(HashPartial, 0f) - partial) > 1e-4f) zdo.Set(HashPartial, partial);
        }

        // ------------------------------------------------------------------ mw_state

        /// <summary>Sets the mode (Idle, Hunt, Engulf, Dormant) keeping the transition and Stream bits. Owner only.</summary>
        public void SetMode(byte mode) => WriteState(MassStateCodes.WithMode(StateCode, mode));

        public void SetFlag(byte flag, bool on) => WriteState(MassStateCodes.With(StateCode, flag, on));

        /// <summary>Enters or leaves the stream form: the Stream bit and the ribbon width (mw_sw). Owner only.</summary>
        public void SetStream(bool on, float width)
        {
            if (!IsOwner) return;
            ZDO zdo = _nview.GetZDO();
            if (on && Math.Abs(zdo.GetFloat(HashStreamWidth, 0f) - width) > 0.02f) zdo.Set(HashStreamWidth, width);
            SetFlag(MassStateCodes.Stream, on);
            if (!on && zdo.GetFloat(HashStreamWidth, 0f) != 0f) zdo.Set(HashStreamWidth, 0f);
        }

        /// <summary>Sets <c>mw_target</c> (owner only); ZDOID.None clears it.</summary>
        public void SetTarget(ZDOID id)
        {
            if (!IsOwner) return;
            if (TargetId != id) _nview.GetZDO().Set(MWNames.ZdoMassTarget, id);
        }

        private void WriteState(byte code)
        {
            if (!IsOwner) return;
            if (StateCode != code) _nview.GetZDO().Set(HashState, (int)code);
        }

        // ------------------------------------------------------------------ dissolution (spec §4.4)

        /// <summary>
        /// A loss took the mass under Mass.DissolveThreshold: the colony recreates the worms (fleeing) where the mass stands, and the
        /// mass is destroyed after the dome shrank. Without a colony the worms are lost (logged).
        /// </summary>
        private void BeginDissolve(int n)
        {
            if (_dissolving) return;
            if (_merging) CancelMerge();
            _dissolving = true;
            DestroyReason = $"dissolution (n={n} left, under Mass.DissolveThreshold)";
            WriteState(MassStateCodes.With(StateCode, MassStateCodes.Dissolving, true));

            Vector3 position = transform.position;
            ZDOID colonyId = ColonyId;
            WormColony colony = colonyId != ZDOID.None ? ColonyRegistry.FindById(colonyId) : null;
            if (colony == null)
            {
                Plugin.Log.LogWarning($"[Mass] {ZdoId} dissolved with {n} worms but colony {colonyId} is not loaded here: the worms are lost");
                PoolOnDeath();   // nobody to hand the pool to: it is dropped (nothing is lost silently)
            }
            else
            {
                // The loot pool of the mass travels in this package and is shared among the worms released (spec Â§7.1).
                colony.RequestDissolve(ZdoId, n, position, TakePoolPackage());
            }
            Invoke(nameof(DestroyMass), DissolveDestroyDelay);
        }

        private string LastAttackerText()
        {
#if MW_DEV
            return LastAttacker ?? "nobody";
#else
            return "n/a";
#endif
        }

        private void DestroyMass()
        {
            if (!Valid || !_nview.IsOwner()) return;
            if (ZNetScene.instance != null) ZNetScene.instance.Destroy(gameObject);
            else Destroy(gameObject);
        }

        // ------------------------------------------------------------------ name (spec §4.3)

        /// <summary>"Writhing Mass (n)" in the current language, rebuilt only when n or the language changes (EnemyHud reads it every frame).</summary>
        public string HoverName()
        {
            int n = N;
            string language = global::Localization.instance != null ? global::Localization.instance.GetSelectedLanguage() : string.Empty;
            if (_hoverName == null || n != _hoverN || language != _hoverLanguage)
            {
                _hoverN = n;
                _hoverLanguage = language;
                _hoverName = MWLocalization.Format(MWNames.MassCountToken, MWNames.MassNameToken, n);
            }
            return _hoverName;
        }
    }
}
