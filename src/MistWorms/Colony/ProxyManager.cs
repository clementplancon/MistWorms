using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Client side pool of <see cref="WormProxy"/> objects and their attachment to the virtual worms near the viewer (spec §10.1).
    /// A worm alive and within 40 m of the camera or of the local player gets a proxy; it is released beyond 44 m, when the worm dies
    /// or when its slot is reused. The per-frame path allocates nothing: arrays and index stacks are fixed at start.
    /// </summary>
    [DefaultExecutionOrder(32001)]
    internal sealed class ProxyManager : MonoBehaviour
    {
        public const int PoolSize = 420;                 // spec §10.1: 400 worms + the Debug margin
        public const float AttachRange = 40f;
        public const float ReleaseRange = 44f;

        // Geometry of the hit body. The vanilla sword sweeps a 0.5 m sphere at hip height (dump section 7), so a 0.18 m ball resting
        // on the floor is inside its reach (decisions.md 49).
        public const float ColliderRadius = 0.18f;
        public const float CenterHeight = 0.12f;

        private const float MoveEpsilonSqr = 1e-6f;

        private static ProxyManager s_instance;

        /// <summary>The manager if it already exists; never creates one.</summary>
        public static ProxyManager Existing => s_instance;

        public static ProxyManager Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("MistWorms.ProxyManager") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(go);
                    s_instance = go.AddComponent<ProxyManager>();
                }
                return s_instance;
            }
        }

        private WormProxy[] _pool;
        private SphereCollider[] _colliders;
        private int[] _free;
        private int _freeCount;
        private int[] _active;
        private int _activeCount;
        private int[] _activeSlot;        // position of a pool entry inside _active, for O(1) removal
        private Vector3[] _lastPos;

        // Fallback layer only: the player instance each proxy already ignores.
        private int[] _ignoredFor;
        private readonly List<Collider> _playerColliders = new List<Collider>(8);

        public int ActiveCount => _activeCount;

        /// <summary>The proxy attached to worm <paramref name="i"/> of <paramref name="colony"/>, or null.</summary>
        public WormProxy ProxyFor(WormColony colony, int i)
        {
            if (_pool == null || colony?.Store == null || (uint)i >= (uint)colony.Store.Capacity) return null;
            int p = colony.Store.proxyIndex[i];
            return p >= 0 && p < _pool.Length && _pool[p].colony == colony && _pool[p].index == i ? _pool[p] : null;
        }

        private void Awake()
        {
            ProxyLayer.Resolve();
            _pool = new WormProxy[PoolSize];
            _colliders = new SphereCollider[PoolSize];
            _free = new int[PoolSize];
            _active = new int[PoolSize];
            _activeSlot = new int[PoolSize];
            _lastPos = new Vector3[PoolSize];
            _ignoredFor = new int[PoolSize];

            for (int k = 0; k < PoolSize; k++)
            {
                var go = new GameObject("MW_WormProxy");
                go.transform.SetParent(transform, false);
                go.layer = ProxyLayer.Layer;

                var sphere = go.AddComponent<SphereCollider>();
                sphere.radius = ColliderRadius;
                sphere.isTrigger = false;

                // Kinematic: moving it never wakes the physics solver, and nothing pushes it.
                var body = go.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;

                var proxy = go.AddComponent<WormProxy>();
                proxy.poolIndex = k;
                _pool[k] = proxy;
                _colliders[k] = sphere;
                go.SetActive(false);
                _free[k] = PoolSize - 1 - k;
            }
            _freeCount = PoolSize;
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private void LateUpdate()
        {
            if (_pool == null) return;
            float now = Time.time;

            Vector3 camPos = default, playerPos = default;
            bool haveCam = false, havePlayer = false;
            Camera cam = Utils.GetMainCamera();
            if (cam != null) { camPos = cam.transform.position; haveCam = true; }
            Player player = Player.m_localPlayer;
            if (player != null) { playerPos = player.transform.position; havePlayer = true; }
            if (!haveCam && !havePlayer) return;

            float attachSqr = AttachRange * AttachRange;
            float releaseSqr = ReleaseRange * ReleaseRange;

            // 1. release: dead or reused worm, colony gone, out of range.
            for (int a = _activeCount - 1; a >= 0; a--)
            {
                WormProxy p = _pool[_active[a]];
                WormColony c = p.colony;
                bool keep = false;
                if (c != null && c.Store != null && c.Store.IsAlive(p.index, p.generation)
                    && c.Store.state[p.index] != (byte)WormState.Absorbed)
                {
                    keep = NearestSqr(c.Store.pos[p.index], haveCam, camPos, havePlayer, playerPos) <= releaseSqr;
                }
                if (!keep) Release(p.poolIndex);
            }

            // 2. attach to the worms that came within range.
            List<WormColony> colonies = ColonyRegistry.All;
            for (int ci = 0; ci < colonies.Count; ci++)
            {
                WormColony c = colonies[ci];
                if (c == null || c.Store == null || c.Store.AliveCount == 0) continue;
                WormStore s = c.Store;
                for (int i = 0; i < s.HighWater; i++)
                {
                    if (!s.alive[i] || s.proxyIndex[i] >= 0 || s.state[i] == (byte)WormState.Absorbed) continue;
                    if (NearestSqr(s.pos[i], haveCam, camPos, havePlayer, playerPos) > attachSqr) continue;
                    if (_freeCount == 0) return; // pool exhausted (cannot happen below the capacity of one colony)
                    Attach(c, i, now);
                }
            }

            // 3. follow the interpolated position.
            for (int a = 0; a < _activeCount; a++)
            {
                int k = _active[a];
                WormProxy p = _pool[k];
                Vector3 pos = p.colony.InterpolatedPos(p.index, now);
                pos.y += CenterHeight;
                if ((pos - _lastPos[k]).sqrMagnitude > MoveEpsilonSqr)
                {
                    p.transform.position = pos;
                    _lastPos[k] = pos;
                }
            }
        }

        private static float NearestSqr(Vector3 p, bool haveCam, Vector3 cam, bool havePlayer, Vector3 player)
        {
            float best = float.MaxValue;
            if (haveCam) best = (p - cam).sqrMagnitude;
            if (havePlayer)
            {
                float d = (p - player).sqrMagnitude;
                if (d < best) best = d;
            }
            return best;
        }

        private void Attach(WormColony colony, int i, float now)
        {
            int k = _free[--_freeCount];
            WormProxy p = _pool[k];
            p.colony = colony;
            p.index = i;
            p.generation = colony.Store.generation[i];
            colony.Store.proxyIndex[i] = k;

            Vector3 pos = colony.InterpolatedPos(i, now);
            pos.y += CenterHeight;
            p.transform.position = pos;
            _lastPos[k] = pos;
            p.gameObject.SetActive(true);

            _activeSlot[k] = _activeCount;
            _active[_activeCount++] = k;

            if (ProxyLayer.NeedsIgnoreCollision) IgnoreLocalPlayer(k);
        }

        private void Release(int k)
        {
            WormProxy p = _pool[k];
            WormColony c = p.colony;
            if (c != null && c.Store != null && (uint)p.index < (uint)c.Store.Capacity && c.Store.proxyIndex[p.index] == k)
                c.Store.proxyIndex[p.index] = -1;

            p.colony = null;
            p.gameObject.SetActive(false);

            int slot = _activeSlot[k];
            int last = _active[--_activeCount];
            _active[slot] = last;
            _activeSlot[last] = slot;
            _free[_freeCount++] = k;
            _ignoredFor[k] = 0;
        }

        /// <summary>Fallback when no layer is collision free: ignore every collider of the local player for this proxy.</summary>
        private void IgnoreLocalPlayer(int k)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            int id = player.GetInstanceID();
            if (_ignoredFor[k] == id) return;
            _ignoredFor[k] = id;
            player.GetComponentsInChildren(true, _playerColliders);
            for (int c = 0; c < _playerColliders.Count; c++)
                if (_playerColliders[c] != null) Physics.IgnoreCollision(_colliders[k], _playerColliders[c], true);
        }
    }
}
