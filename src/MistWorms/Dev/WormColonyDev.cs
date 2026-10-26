#if MW_DEV
using System.Collections.Generic;
using MistWorms.Drops;
using MistWorms.Mass;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>Debug-only handles on the waking, the calm and the pools of a colony (S14, S14b, state dumps). Not compiled into a Release build.</summary>
    internal sealed partial class WormColony
    {
        /// <summary>
        /// Dev (S11): while true the owner's update (simulation, sounds, snapshot, network send) is measured by the rise of the managed heap around it, summed in <see cref="DevAllocBytes"/> over
        /// <see cref="DevAllocFrames"/> frames, all colonies. The heap of the whole game is noisy (see P4_MassPerf), so this reads the rise inside the colony's own update only; a collection inside
        /// the update hides a part of it (the number can only be too low, and the scenario logs the collections).
        /// </summary>
        public static bool DevMeasureAlloc;
        public static long DevAllocBytes;
        public static int DevAllocFrames;

        /// <summary>
        /// Dev (S11): the part of the rise that falls inside the vanilla damage call of a strike (<c>WormBite.SendToPlayer</c> / <c>HitCreature</c> / <c>HitStructure</c>: the routed RPC, its parameter
        /// array, the <c>HitData</c>, <c>Character.Damage</c>), over <see cref="DevVanillaCalls"/> calls. It is taken out of <see cref="DevAllocBytes"/>, which is the mod's own code (decision 229).
        /// </summary>
        public static long DevVanillaBytes;
        public static int DevVanillaCalls;

        public static void DevResetAlloc() { DevAllocBytes = 0L; DevAllocFrames = 0; DevVanillaBytes = 0L; DevVanillaCalls = 0; }

        /// <summary>Wakes group <paramref name="k"/> as an alert would, without the chain (owner only). Returns the sleeping worms woken.</summary>
        public int DevWakeGroup(int k) => IsOwner && _gAwake != null ? WakeGroup(k, Time.time) : 0;

        /// <summary>Alerts group <paramref name="k"/> (owner only): it wakes and the alert spreads to the nests around it.</summary>
        public void DevAlert(int k)
        {
            if (IsOwner && _gAwake != null) Alert(k, Time.time);
        }

        /// <summary>Awake groups as a list of indices (state dumps).</summary>
        public List<object> DevAwakeGroups()
        {
            var list = new List<object>();
            for (int k = 0; k < MaxNests; k++)
                if (IsGroupAwake(k)) list.Add(k);
            return list;
        }

        /// <summary>
        /// Turns up to <paramref name="count"/> worms of group <paramref name="k"/> (not the lone ones) into one mass standing at their centroid, with their pool and
        /// their nests of origin, the way the colony forms a mass. Returns the mass, or null (not the owner, no worm, prefab missing).
        /// </summary>
        public WritheMassState DevFormMassFromGroup(int k, int count)
        {
            if (!IsOwner || Store == null || count < 1) return null;
            WormStore s = Store;
            int taken = 0;
            Vector3 sum = Vector3.zero;
            _formPool.Clear();
            _formNests.Clear();
            for (int i = 0; i < s.HighWater && taken < count; i++)
            {
                if (!s.alive[i] || s.lone[i] || s.nestIndex[i] != k) continue;
                byte st = s.state[i];
                if (st == (byte)WormState.Absorbed || st == (byte)WormState.Dead || st == (byte)WormState.Absorbing) continue;
                _cluster[taken++] = i;
                sum += s.pos[i];
            }
            if (taken == 0) return null;

            WritheMassState mass = WritheMassState.Create(sum / taken, taken, Key, newborn: false);
            if (mass == null) return null;
            for (int j = 0; j < taken; j++)
            {
                int wi = _cluster[j];
                _seedBuf[j] = s.pos[wi];
                if (s.poolIndex[wi] >= 0) _formPool.Merge(s.Pools.Get(s.poolIndex[wi]));
                _formNests.Add(MassNestOf(wi), 1);
                RemoveWorm(wi);
            }
            mass.GiveInitialPool(_formPool);
            mass.GiveInitialNests(_formNests);
            _formNests.Clear();
            _formPool.Clear();
            return mass;
        }

        /// <summary>Puts <paramref name="qty"/> of the item <paramref name="hash"/> in the pool of worm <paramref name="i"/> (S14 measures that none is lost).</summary>
        public bool DevGivePool(int i, int hash, float qty)
        {
            if (Store == null || (uint)i >= (uint)Store.Capacity || !Store.alive[i]) return false;
            MassPool pool = EnsureWormPool(i);
            return pool != null && pool.Add(hash, qty);
        }

        /// <summary>Everything this colony holds: worms of the store (free ones), worms inside its masses, and the loot pool in the worms, the masses and the reserve.</summary>
        public void DevTotals(out int worms, out float poolQty)
        {
            WormStore s = Store;
            worms = s != null ? s.AliveCount : 0;
            poolQty = 0f;
            if (s != null)
                for (int i = 0; i < s.HighWater; i++)
                    if (s.alive[i] && s.poolIndex[i] >= 0) poolQty += s.Pools.Get(s.poolIndex[i]).TotalAll();
            if (_reserve != null) poolQty += _reserve.TotalAll();

            var scratch = new MassPool();
            long me = Key;
            List<WritheMassState> all = WritheMassState.All;
            for (int i = 0; me != 0L && i < all.Count; i++)
            {
                WritheMassState m = all[i];
                if (m == null || !m.Valid || m.ColonyKey != me || m.Dissolving) continue;
                worms += m.N;
                if (scratch.ReadBytes(m.DevPoolBytes())) poolQty += scratch.TotalAll();
            }
        }

        /// <summary>Writes <c>mw_snap</c> now (S15 forces a snapshot instead of waiting Network.SnapshotInterval). Returns its size in bytes, or -1 when this peer cannot write it.</summary>
        public int DevWriteSnapshot()
        {
            if (!IsOwner || !_snapSettled) return -1;
            WriteSnapshot();
            return _snapLast != null ? _snapLast.Length : -1;
        }

        /// <summary>Size of the <c>mw_snap</c> in the ZDO (0 = none).</summary>
        public int DevSnapshotSize()
        {
            byte[] data = _zdo != null && _zdo.IsValid() ? _zdo.GetByteArray(HashSnapshot, null) : null;
            return data != null ? data.Length : 0;
        }

        /// <summary>Puts <paramref name="qty"/> of <paramref name="hash"/> in the reserve of the colony (S15 checks it comes back).</summary>
        public void DevAddReserve(int hash, float qty)
        {
            if (_reserve == null) _reserve = new MassPool();
            _reserve.Add(hash, qty);
        }

        public float DevReserveTotal() => _reserve != null ? _reserve.TotalAll() : 0f;

        /// <summary>Counts of the free worms by state, for the dumps.</summary>
        public int DevCountState(WormState state)
        {
            WormStore s = Store;
            int n = 0;
            if (s == null) return 0;
            for (int i = 0; i < s.HighWater; i++)
                if (s.alive[i] && s.state[i] == (byte)state) n++;
            return n;
        }
    }
}

namespace MistWorms.Mass
{
    internal sealed partial class WritheMassState
    {
        /// <summary>The raw <c>mw_pool</c> of the ZDO (dev totals).</summary>
        public byte[] DevPoolBytes() => Valid ? _nview.GetZDO().GetByteArray(MWNames.ZdoMassPool, null) : null;
    }
}
#endif
