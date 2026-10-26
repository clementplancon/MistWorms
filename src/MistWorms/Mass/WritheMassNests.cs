using MistWorms.Colony;

namespace MistWorms.Mass
{
    /// <summary>
    /// The <c>nestCounts</c> of a mass (spec §8.2, decision 198): how many of its worms come from each nest, plus the worms with no nest. The owner keeps
    /// the set in memory and writes it to <c>mw_nests</c> after every change; every change goes through <see cref="NestCounts"/>, so the sum stays n:
    /// absorption adds one worm to its nest, a merge moves a prorata share, a loss removes a prorata share, a dissolution hands the set to the colony.
    /// </summary>
    internal sealed partial class WritheMassState
    {
        private static readonly NestCounts s_nestIn = new NestCounts();       // what AddWorms receives
        private static readonly NestCounts s_nestCut = new NestCounts();      // the part of it a full mass accepts
        private static readonly NestCounts s_nestRpc = new NestCounts();      // the set of a MW_MassAdd, while it is applied
        private static readonly NestCounts s_nestOffer = new NestCounts();    // a partial merge: the share offered
        private static readonly NestCounts s_nestShare = new NestCounts();    // a partial merge: the share that really left
        private static readonly NestCounts s_nestRead = new NestCounts();     // a non owner reading the ZDO

        private NestCounts _nests;

        private NestCounts LoadNests()
        {
            if (_nests == null)
            {
                _nests = new NestCounts();
                _nests.ReadBytes(Valid ? _nview.GetZDO().GetByteArray(MWNames.ZdoMassNests, null) : null);
            }
            return _nests;
        }

        /// <summary>The set of the owner, made to add up to n first (a mass made by a dev tool, an old save: the missing worms have no nest).</summary>
        private NestCounts OwnerNests()
        {
            NestCounts c = LoadNests();
            if (!_dissolving) c.FitTo(N);
            return c;
        }

        private void SaveNests()
        {
            if (Valid && IsOwner && _nests != null) _nview.GetZDO().Set(MWNames.ZdoMassNests, _nests.ToBytes());
        }

        /// <summary>Worms of this mass that come from nest <paramref name="nest"/> (any peer: a non owner reads the ZDO).</summary>
        public int NestCountOf(int nest)
        {
            if (!Valid) return 0;
            if (IsOwner) return OwnerNests()[nest];
            s_nestRead.ReadBytes(_nview.GetZDO().GetByteArray(MWNames.ZdoMassNests, null));
            return s_nestRead[nest];
        }

        /// <summary>The worms that formed this mass, by nest (the colony, at creation). Owner only.</summary>
        public void GiveInitialNests(NestCounts formed)
        {
            if (!IsOwner || formed == null) return;
            NestCounts c = LoadNests();
            c.CopyFrom(formed);
            c.FitTo(N);
            SaveNests();
        }

        /// <summary>Counts for a fresh mass: every worm without a nest.</summary>
        private void ResetNests(int n)
        {
            NestCounts c = LoadNests();
            c.Clear();
            c.FitTo(n);
            SaveNests();
        }

        /// <summary>The owner adds <paramref name="accepted"/> of the <paramref name="k"/> worms offered: their set prorata when the cap cut the group. Call before the count changes.</summary>
        private void AddNests(NestCounts offered, int accepted, int k)
        {
            NestCounts c = OwnerNests();
            s_nestIn.Clear();
            if (offered != null) s_nestIn.CopyFrom(offered);
            s_nestIn.FitTo(k);
            if (accepted < k)
            {
                s_nestIn.CopyProrata(accepted, s_nestCut);
                c.AddAll(s_nestCut);
            }
            else
            {
                c.AddAll(s_nestIn);
            }
            SaveNests();
        }

        /// <summary>Worms died (a loss): the same number leaves the set, prorata. Call with the count before the loss.</summary>
        private void LoseNests(int kills, int countBefore)
        {
            NestCounts c = LoadNests();
            c.FitTo(countBefore);
            c.TakeProrata(kills, null);
            SaveNests();
        }

        /// <summary>The mass is gone: nothing counts any more.</summary>
        private void ClearNests()
        {
            LoadNests().Clear();
            SaveNests();
        }

        /// <summary>The set leaves with the pool for the colony (dissolution): appended to the package, and the mass keeps nothing.</summary>
        private void TakeNestsInto(ZPackage pkg)
        {
            NestCounts c = LoadNests();
            c.AppendTo(pkg);
            c.Clear();
            SaveNests();
        }
    }
}
