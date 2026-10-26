using System;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Config;
using MistWorms.Drops;
using UnityEngine;

namespace MistWorms.Mass
{
    /// <summary>
    /// The loot pool of a mass (spec §7, decisions.md 68, 79, plan 6-1). The owner keeps it in a fixed <see cref="MassPool"/> and writes it to the ZDO
    /// <c>mw_pool</c> after every change (versioned bytes, see <see cref="MassPool.Write"/>), so a new owner, a save or a reload restores it. Non-owners never
    /// touch it. What the mass eats arrives through <c>MW_Consumed</c> (<see cref="AddConsumed"/>); worms that die take their share
    /// (<see cref="PoolOnLoss"/>), the last ones the rest; absorption, fusion and dissolution move it with the worms.
    /// </summary>
    internal sealed partial class WritheMassState
    {
        /// <summary>Where the items of the dead worms appear: above the middle of the dome.</summary>
        private const float PoolDropHeight = 0.5f;

        private static readonly MassPool s_poolRpcIn = new MassPool();      // the pool of a MW_MassAdd, while it is applied
        private static readonly MassPool s_poolPart = new MassPool();       // a fraction of a donor pool, while it is merged
        private static readonly MassPool s_poolTransfer = new MassPool();   // the fraction a partial merge hands over

        private MassPool _pool;
        private bool _poolLoaded;

        /// <summary>The pool of this mass, loaded from the ZDO the first time the owner needs it. Owner only.</summary>
        private MassPool OwnerPool()
        {
            if (_pool == null) _pool = new MassPool();
            if (!_poolLoaded)
            {
                _poolLoaded = true;
                byte[] data = Valid ? _nview.GetZDO().GetByteArray(MWNames.ZdoMassPool, null) : null;
                if (!_pool.ReadBytes(data))
                    Plugin.Log.LogWarning($"[Pool] mass {ZdoId}: unreadable mw_pool ({(data != null ? data.Length : 0)} bytes), pool reset");
            }
            return _pool;
        }

        private void SavePool()
        {
            if (!Valid || !_nview.IsOwner() || _pool == null) return;
            _nview.GetZDO().Set(MWNames.ZdoMassPool, _pool.ToBytes());
        }

        /// <summary>A peer that is not the owner forgets its copy: the next owner reads the ZDO again.</summary>
        private void PoolTick()
        {
            if (_poolLoaded && !IsOwner) _poolLoaded = false;
        }

        private Vector3 PoolDropPoint() => transform.position + Vector3.up * PoolDropHeight;

        /// <summary>What a bite ate (<c>MW_Consumed</c>, already validated) enters the pool. Owner only; a dissolving mass has already handed its pool over.</summary>
        public void AddConsumed(ConsumedList list)
        {
            if (list == null || list.Count == 0 || !IsOwner || _dissolving) return;
            MassPool pool = OwnerPool();
            int refused = 0;
            for (int i = 0; i < list.Count; i++)
                if (!pool.Add(list.Hash[i], list.Qty[i])) refused++;
            if (refused > 0) Plugin.Log.LogWarning($"[Pool] mass {ZdoId}: {refused} entries refused (pool holds {pool.Count} of {MassPool.MaxEntries} resources)");
            SavePool();
        }

        /// <summary><paramref name="kills"/> of the <paramref name="nBefore"/> worms died: their share of the pool is dropped (spec §7.3). Owner only.</summary>
        private void PoolOnLoss(int kills, int nBefore)
        {
            MassPool pool = OwnerPool();
            if (pool.IsEmpty) return;
            WormDrops.DropPoolShare(pool, kills, nBefore, PoolDropPoint());
            SavePool();
        }

        /// <summary>The last worm died: floor of everything left. Owner only.</summary>
        private void PoolOnDeath()
        {
            MassPool pool = OwnerPool();
            if (pool.IsEmpty) return;
            WormDrops.DropPoolRest(pool, PoolDropPoint());
            SavePool();
        }

        /// <summary>The pool as a package for <c>MW_Dissolve</c>; the mass keeps nothing. Owner only.</summary>
        private ZPackage TakePoolPackage()
        {
            var pkg = new ZPackage();
            MassPool pool = OwnerPool();
            pool.Write(pkg);
            pool.Clear();
            SavePool();
            TakeNestsInto(pkg);   // the nests of origin of the worms travel with the pool, after it
            return pkg;
        }

        /// <summary>
        /// Takes the pool of arriving worms (owner side of <see cref="AddWorms"/>): all of <paramref name="donor"/> when the <paramref name="accepted"/> worms are the
        /// <paramref name="k"/> offered, the fraction accepted/k otherwise (what stays in the donor goes with the worms that stay). Quantities and carries.
        /// </summary>
        private void TakeDonorPool(MassPool donor, int accepted, int k)
        {
            if (donor == null || donor.IsEmpty) return;
            MassPool pool = OwnerPool();
            int lost;
            if (accepted >= k)
            {
                lost = pool.Merge(donor);
                donor.Clear();
            }
            else
            {
                s_poolPart.Clear();
                lost = donor.MoveFraction(s_poolPart, accepted, k);
                lost += pool.Merge(s_poolPart);
            }
            if (lost > 0) Plugin.Log.LogWarning($"[Pool] mass {ZdoId}: {lost} resources did not fit when a pool was added (limit {MassPool.MaxEntries})");
            SavePool();
        }

        /// <summary>What a donor pool still holds after the worms were refused is dropped where this mass stands. The pool is emptied.</summary>
        private void DropLeftover(MassPool leftover)
        {
            if (leftover.IsEmpty) return;
#if MW_DEV
            Plugin.DevInfo($"[Pool] mass {ZdoId}: pool of refused worms dropped ({leftover.Count} resources)");
#endif
            WormDrops.DropPoolRest(leftover, PoolDropPoint());
            leftover.Clear();
        }

        /// <summary>The pools of the worms that formed this mass (owner, at creation).</summary>
        public void GiveInitialPool(MassPool pools)
        {
            if (pools == null || pools.IsEmpty || !IsOwner) return;
            TakeDonorPool(pools, 1, 1);
        }

        private ZPackage DonorPackage(MassPool donor, NestCounts nests = null)
        {
            var pkg = new ZPackage();
            if (donor != null) donor.Write(pkg);
            else new MassPool().Write(pkg);    // the set always follows a pool, empty or not
            if (nests != null) nests.AppendTo(pkg);
            return pkg;
        }

#if MW_DEV
        /// <summary>The pool as the bridge shows it: resource, quantity, carry (any peer: a non-owner reads the ZDO).</summary>
        public List<object> PoolJson()
        {
            var list = new List<object>();
            if (!Valid) return list;
            var view = new MassPool();
            if (IsOwner) view.CopyFrom(OwnerPool());
            else view.ReadBytes(_nview.GetZDO().GetByteArray(MWNames.ZdoMassPool, null));
            for (int i = 0; i < view.Count; i++)
            {
                GameObject prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(view.HashAt(i)) : null;
                list.Add(new Dictionary<string, object>
                {
                    { "prefab", prefab != null ? prefab.name : view.HashAt(i).ToString() },
                    { "hash", view.HashAt(i) },
                    { "qty", view.QtyAt(i) },
                    { "carry", view.CarryAt(i) },
                });
            }
            return list;
        }

        /// <summary>Dev: the owner's pool (a copy into <paramref name="into"/>).</summary>
        public void DevPoolCopy(MassPool into)
        {
            if (IsOwner) into.CopyFrom(OwnerPool());
            else into.ReadBytes(Valid ? _nview.GetZDO().GetByteArray(MWNames.ZdoMassPool, null) : null);
        }

        /// <summary>Dev: puts <paramref name="qty"/> of a prefab into the pool as a bite would (S05 unit setup). Owner only.</summary>
        public void DevAddToPool(ConsumedList list) => AddConsumed(list);
#endif
    }
}
