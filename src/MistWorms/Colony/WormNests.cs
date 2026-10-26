using System;
using System.Collections.Generic;
using MistWorms.Config;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// The nests of a colony (spec §8): registry by stable index, laying of the sleeping worms, the per nest count under the caps, release of
    /// the worms of a destroyed nest, and the seeded flag of the mine. Everything here runs on the owner of the colony (the nests may be owned by
    /// another peer: they are read, never written, except by their own owner).
    /// </summary>
    internal sealed partial class WormColony
    {
        /// <summary>Slots of the nest registry; <c>Nest.MaxNestsPerMine</c> is at most 64.</summary>
        public const int MaxNests = 64;

        private const float LayRingMin = 1f, LayRingMax = 2f;               // spec §8.1: an egg is laid 1 to 2 m from the nest
        private const float ReleaseRingMin = 0.8f, ReleaseRingMax = 1.6f;
        private const float CapCheckInterval = 0.5f;
        private const float LayRetrySeconds = 2f;
        private const int RingTries = 8;

        private WormNest[] _nests;
        private readonly NestCounts _oneNest = new NestCounts();     // scratch: the nest of the one worm that climbs onto a mass
        private readonly NestCounts _formNests = new NestCounts();   // scratch: the nests of the worms that form a mass
        private readonly NestCounts _dissolveNests = new NestCounts();
        private float _lastNestTick;

        /// <summary>Nests of this colony that are loaded on this peer.</summary>
        public int NestsLoaded { get; private set; }

        /// <summary>Eggs laid, nests destroyed by a hit and worms released by them since this colony was loaded (dev checks).</summary>
        public int EggsLaid { get; private set; }
        public int NestsDestroyed { get; private set; }
        public int WormsReleased { get; private set; }

        /// <summary>Report of the last seeding this peer ran on this colony, or null.</summary>
        public SeedReport LastSeed { get; set; }

#if MW_DEV
        /// <summary>Dev switch: when positive, replaces <c>Nest.SpawnInterval</c> (S09 forces 2 s).</summary>
        public static float DevSpawnInterval;
#endif

        /// <summary>
        /// Free worms plus the worms inside the masses of this colony: what <c>Colony.MaxAlivePerMine</c> limits (spec §8.2). Computed from the masses at the
        /// moment it is asked (merging ones included; a dissolving one already handed its worms to the store), so it is exact at every instant.
        /// </summary>
        public int TotalAlive => (Store != null ? Store.AliveCount : 0) + WormsInMasses;

        public int WormsInMasses
        {
            get
            {
                int n = 0;
                ZDOID me = ZdoId;
                List<WritheMassState> all = WritheMassState.All;
                for (int i = 0; i < all.Count; i++)
                {
                    WritheMassState m = all[i];
                    if (m != null && m.Valid && !m.Dissolving && m.ColonyId == me) n += m.N;
                }
                return n;
            }
        }

        /// <summary>True once the mine has been seeded (flag in the ZDO of the colony): a mine is never seeded twice.</summary>
        public bool Seeded => _initialized && _nview.IsValid() && _nview.GetZDO().GetInt(MWNames.ZdoSeeded, 0) != 0;

        /// <summary>Writes (or clears, dev) the seeded flag; the owner only.</summary>
        public void SetSeeded(bool value)
        {
            if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
            _nview.GetZDO().Set(MWNames.ZdoSeeded, value ? 1 : 0);
        }

        private void InitNests()
        {
            _nests = new WormNest[MaxNests];
        }

        public WormNest NestAt(int index) => _nests != null && (uint)index < (uint)_nests.Length && _nests[index] != null ? _nests[index] : null;

        /// <summary>The lowest free nest index, or -1 when all 64 are taken.</summary>
        public int NextFreeNestIndex()
        {
            if (_nests == null) return -1;
            for (int k = 0; k < _nests.Length; k++)
                if (_nests[k] == null) return k;
            return -1;
        }

        /// <summary>Called by a nest once it knows its colony (and by <see cref="SpawnNest"/>).</summary>
        public void RegisterNest(WormNest nest)
        {
            if (_nests == null || nest == null || (uint)nest.Index >= (uint)_nests.Length) return;
            if (_nests[nest.Index] == nest) return;
            if (_nests[nest.Index] == null) NestsLoaded++;
            _nests[nest.Index] = nest;
        }

        public void UnregisterNest(WormNest nest)
        {
            if (_nests == null || nest == null || (uint)nest.Index >= (uint)_nests.Length || _nests[nest.Index] != nest) return;
            _nests[nest.Index] = null;
            NestsLoaded--;
        }

        /// <summary>
        /// Creates a nest at <paramref name="position"/> (a floor point) as a world object owned by this peer. <paramref name="index"/> -1 takes the
        /// lowest free one. Returns null when the prefab is not registered or the registry is full.
        /// </summary>
        public WormNest SpawnNest(Vector3 position, float yawDegrees, int index = -1)
        {
            if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return null;
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(MWNames.NestPrefab) : null;
            if (prefab == null)
            {
                Plugin.Log.LogError($"[Colony] prefab {MWNames.NestPrefab} is not registered in the ZNetScene");
                return null;
            }
            if (index < 0) index = NextFreeNestIndex();
            if (index < 0 || index >= MaxNests) return null;

            GameObject go = Instantiate(prefab, position, Quaternion.Euler(0f, yawDegrees, 0f));
            var nest = go.GetComponent<WormNest>();
            if (nest == null)
            {
                Destroy(go);
                return null;
            }
            nest.Setup(this, index);
            RegisterNest(nest);
            return nest;
        }

        /// <summary>Destroys every nest of this colony that is loaded, with no effect and no release (dev reset, mw_kill).</summary>
        public int DestroyAllNests()
        {
            if (_nests == null) return 0;
            int n = 0;
            for (int k = 0; k < _nests.Length; k++)
            {
                WormNest nest = _nests[k];
                if (nest == null) continue;
                nest.DestroyQuiet();
                n++;
            }
            return n;
        }

        // ------------------------------------------------------------------ laying (spec §8.1, §8.2)

        private float LayInterval()
        {
#if MW_DEV
            if (DevSpawnInterval > 0f) return DevSpawnInterval;
#endif
            return MWConfig.Nest.SpawnInterval.Value;
        }

        private bool AnyPlayerInBox()
        {
            if (_players == null) return false;
            for (int k = 0; k < _players.Count; k++)
            {
                Player p = _players[k];
                if (p != null && InBox(p.transform.position)) return true;
            }
            return false;
        }

        /// <summary>
        /// Living worms of nest <paramref name="k"/>: the free ones that were laid or seeded at it plus the <c>nestCounts</c> of every mass of the colony
        /// (merging and dissolving ones included). Lone worms do not count.
        /// </summary>
        public int CountFromNest(int k)
        {
            WormStore s = Store;
            int n = 0;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && s.nestIndex[i] == k && !s.lone[i]) n++;
            ZDOID me = ZdoId;
            List<WritheMassState> all = WritheMassState.All;
            for (int i = 0; i < all.Count; i++)
            {
                WritheMassState m = all[i];
                if (m != null && m.Valid && m.ColonyId == me) n += m.NestCountOf(k);
            }
            return n;
        }

        private void TickNests(float now)
        {
            float dt = Mathf.Min(now - _lastNestTick, 0.5f);
            _lastNestTick = now;
            if (NestsLoaded <= 0 || !MWConfig.General.Enabled.Value) return;

            bool present = AnyPlayerInBox();                 // no laying while nobody is in the mine (no catching up either, spec §8.1)
            float interval = LayInterval();
            float pause = MWConfig.Nest.HitPause.Value;
            int maxNest = MWConfig.Nest.MaxAlivePerNest.Value;
            int maxMine = MWConfig.Colony.MaxAlivePerMine.Value;

            for (int k = 0; k < _nests.Length; k++)
            {
                WormNest nest = _nests[k];
                if (nest == null || !nest.IsValid) continue;

                bool paused = !present || nest.SecondsSinceHit < pause;
                bool ready = nest.LayTimer + dt >= interval;
                bool can = true;
                if (ready && !paused)
                {
                    can = false;
                    if (now >= nest.NextCapCheck)
                    {
                        can = NestMath.CanLay(CountFromNest(k), maxNest, TotalAlive, maxMine);
                        if (!can) nest.NextCapCheck = now + CapCheckInterval;
                    }
                }
                if (!NestMath.LayStep(ref nest.LayTimer, dt, interval, paused, can)) continue;

                if (LayEgg(nest, k)) EggsLaid++;
                else nest.LayTimer = Mathf.Max(0f, interval - LayRetrySeconds);   // no room around the nest right now: try again shortly
            }
        }

        private bool LayEgg(WormNest nest, int k)
        {
            Vector3 center = nest.transform.position;
            if (!FindRingPoint(center, LayRingMin, LayRingMax, out Vector3 at)) return false;
            Vector3 away = at - center;
            int slot = AddWorm(at, WormState.Sleeping, WormMath.HeadingOf(away.x, away.z));
            if (slot < 0) return false;
            Store.nestIndex[slot] = (short)k;
            nest.Laid++;
            return true;
        }

        /// <summary>A floor point between <paramref name="rMin"/> and <paramref name="rMax"/> from <paramref name="center"/>, in the box, no wall in the way, at most a step above or below.</summary>
        private bool FindRingPoint(Vector3 center, float rMin, float rMax, out Vector3 point)
        {
            for (int t = 0; t < RingTries; t++)
            {
                float a = (float)_rng.NextDouble() * Mathf.PI * 2f;
                float r = rMin + (float)_rng.NextDouble() * (rMax - rMin);
                Vector3 from = center + new Vector3(Mathf.Cos(a) * r, 1.2f, Mathf.Sin(a) * r);
                if (!Physics.Raycast(from, Vector3.down, out RaycastHit hit, 3f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > WormPhysics.MaxSlopeDeg) continue;
                if (Mathf.Abs(hit.point.y - center.y) > 1f || !InBox(hit.point)) continue;
                if (Physics.Linecast(center + Vector3.up * 0.6f, hit.point + Vector3.up * 0.3f, WormPhysics.SolidMask, QueryTriggerInteraction.Ignore)) continue;
                point = hit.point;
                return true;
            }
            point = center;
            return false;
        }

        // ------------------------------------------------------------------ worms inside masses

        /// <summary>The nest a worm counts for inside a mass: its own, or none for a lone worm or one of unknown origin.</summary>
        private int MassNestOf(int i) => Store.lone[i] ? NestCounts.None : Store.nestIndex[i];

        // ------------------------------------------------------------------ destruction (spec §8.1)

        /// <summary>The nest owner tells the colony owner that nest <paramref name="index"/> was destroyed by a hit: its worms are released.</summary>
        public void RequestNestGone(int index, Vector3 position)
        {
            if (!_initialized || !_nview.IsValid()) return;
            if (_nview.IsOwner())
            {
                ReceiveNestGone(index, position);
                return;
            }
            _nview.InvokeRPC(MWNames.RpcNestGone, index, position);
        }

        private void RPC_NestGone(long sender, int index, Vector3 position)
        {
            try
            {
                if (!_initialized || !_nview.IsValid() || !_nview.IsOwner()) return;
                ReceiveNestGone(index, position);
            }
            catch (Exception e)
            {
                Plugin.LogException("WormColony.RPC_NestGone", e);
            }
        }

        private void ReceiveNestGone(int index, Vector3 position)
        {
            NestsDestroyed++;
            if (!MWConfig.General.Enabled.Value || Disabled) return;

            // Active worms, no drop (nothing falls out of a nest). The cap applies: a full mine releases nothing (spec §8.2).
            int wanted = MWConfig.Nest.ReleaseOnDestroy.Value;
            int created = 0;
            for (int j = 0; j < wanted; j++)
            {
                FindRingPoint(position, ReleaseRingMin, ReleaseRingMax, out Vector3 at);   // the nest position itself when no ring point is free
                int slot = AddWorm(at, WormState.Idle, (float)_rng.NextDouble() * Mathf.PI * 2f - Mathf.PI);
                if (slot < 0) break;
                created++;
            }
            WormsReleased += created;
            Plugin.Log.LogInfo($"[Colony] nest {index} destroyed at {position}: {created}/{wanted} active worms released");
        }
    }
}
