using System;
using MistWorms.Drops;

namespace MistWorms.Colony
{
    /// <summary>
    /// The loot pools of the free worms of a colony (spec §7.1, plan 6, A10, decision 141): the pool of a worm lives in the shared table of its store
    /// (<see cref="WormStore.Pools"/>, <see cref="WormStore.poolIndex"/>), created when the worm first eats something. What a strike ate for a worm that is
    /// gone goes to the <i>reserve</i> of the colony, which joins the next worm absorbed by a mass or the next dissolution.
    /// </summary>
    internal sealed partial class WormColony
    {
        private readonly MassPool _killPool = new MassPool();       // the pool of the worm being killed (its slot is freed before the drop)
        private readonly MassPool _dissolvePool = new MassPool();   // the pool of a dissolved mass, while it is read
        private readonly MassPool _dissolveShare = new MassPool();  // one worm's share of it
        private readonly MassPool _formPool = new MassPool();      // the pools of the worms that form a mass
        private MassPool _reserve;

        /// <summary>The pool of worm <paramref name="i"/>, created if it has none; null when the table is full.</summary>
        private MassPool EnsureWormPool(int i)
        {
            WormStore s = Store;
            if (s.poolIndex[i] < 0) s.poolIndex[i] = s.Pools.Acquire();
            return s.poolIndex[i] >= 0 ? s.Pools.Get(s.poolIndex[i]) : null;
        }

        /// <summary>The pool that goes into a mass with worm <paramref name="i"/>: its own plus the reserve of the colony; null when there is nothing.</summary>
        private MassPool DonorPool(int i)
        {
            bool reserve = _reserve != null && !_reserve.IsEmpty;
            if (!reserve) return Store.poolIndex[i] >= 0 ? Store.Pools.Get(Store.poolIndex[i]) : null;
            MassPool pool = EnsureWormPool(i);
            if (pool == null) return null;
            pool.Merge(_reserve);
            _reserve.Clear();
            return pool;
        }

        /// <summary>
        /// What a cobra strike ate for worm (<paramref name="slot"/>, <paramref name="generation"/>) enters its pool (<c>MW_WormConsumed</c>, already validated).
        /// A worm that died or was absorbed meanwhile leaves it to the reserve of the colony. Owner only.
        /// </summary>
        public void AddConsumedToWorm(int slot, int generation, ConsumedList list)
        {
            if (Store == null || list == null || list.Count == 0 || !IsOwner) return;
            MassPool pool = Store.IsAlive(slot, (ushort)generation) ? EnsureWormPool(slot) : null;
            bool toReserve = pool == null;
            if (toReserve)
            {
                if (_reserve == null) _reserve = new MassPool();
                pool = _reserve;
#if MW_DEV
                Plugin.Log.LogInfo($"[Pool] worm {slot} (generation {generation}) is gone or has no pool slot: {list.Count} entries go to the reserve of colony {ZdoId}");
#endif
            }
            int refused = 0;
            for (int i = 0; i < list.Count; i++)
                if (!pool.Add(list.Hash[i], list.Qty[i])) refused++;
            if (refused > 0) Plugin.Log.LogWarning($"[Pool] colony {ZdoId}: {refused} entries refused ({MassPool.MaxEntries} resources at most in a pool)");
        }

        /// <summary>Reads the pool of a dissolving mass (plus the reserve) and cuts one equal share per worm released.</summary>
        private void PrepareDissolvePool(ZPackage pool, int n)
        {
            _dissolveShare.Clear();
            if (!_dissolvePool.Read(pool))
            {
                Plugin.Log.LogWarning($"[Pool] colony {ZdoId}: unreadable pool in a dissolution, ignored");
                _dissolvePool.Clear();
            }
            if (_reserve != null && !_reserve.IsEmpty)
            {
                _dissolvePool.Merge(_reserve);
                _reserve.Clear();
            }
            if (!_dissolvePool.IsEmpty) _dissolvePool.DivideInto(Math.Max(1, n), _dissolveShare);
        }

        /// <summary>A dissolution that cannot happen (refused): the pool it carried is dropped where the mass stood; the reserve is not touched.</summary>
        private void DropRefusedDissolvePool(ZPackage pool, UnityEngine.Vector3 position)
        {
            if (!_dissolvePool.Read(pool)) _dissolvePool.Clear();
            if (_dissolvePool.IsEmpty) return;
#if MW_DEV
            Plugin.Log.LogInfo($"[Pool] colony {ZdoId}: dissolution refused, its pool is dropped at {position}");
#endif
            WormDrops.DropPoolRest(_dissolvePool, position + UnityEngine.Vector3.up * 0.3f);
        }

        /// <summary>One worm of a dissolution could not be created: its share goes to the reserve of the colony.</summary>
        private void ShareToReserve()
        {
            if (_dissolveShare.IsEmpty) return;
            if (_reserve == null) _reserve = new MassPool();
            _reserve.Merge(_dissolveShare);
        }

        /// <summary>The worm just created by a dissolution takes its equal share (quantities and carries, spec §7.1).</summary>
        private void GiveDissolvedShare(int slot)
        {
            if (_dissolveShare.IsEmpty) return;
            MassPool pool = EnsureWormPool(slot);
            if (pool != null) pool.CopyFrom(_dissolveShare);
        }
    }
}
