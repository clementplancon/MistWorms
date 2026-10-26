using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>The colonies currently loaded on this peer (added in Awake, removed in OnDestroy).</summary>
    internal static class ColonyRegistry
    {
        private static readonly List<WormColony> s_all = new List<WormColony>();

#if MW_DEV
        /// <summary>
        /// Dev: a dedicated server hosts colonies after all (the <c>serverown</c> mode of the pass 8-2: the server claims the colony of the dev site and simulates it, which makes the
        /// client a real non owner). The extension point of <see cref="ServerMayHost"/>, set by <c>Dev.ServerOwnDev</c> (the <c>own</c> request of the pass 8-2, decisions 220 and 221) and cleared by <c>release</c>.
        /// </summary>
        public static bool DevServerOwn;
#else
        public const bool DevServerOwn = false;
#endif

        /// <summary>
        /// False on a dedicated server (spec §10.2, decision 215): it creates, seeds, claims and simulates no colony, it only routes the network objects, as vanilla does for creatures.
        /// Checked by <see cref="WormColony.Create"/>, <see cref="MineSeeder.Pump"/> and <c>WormColony.Update</c>.
        /// </summary>
        public static bool ServerMayHost => DevServerOwn || ZNet.instance == null || !ZNet.instance.IsDedicated();

        /// <summary>Do not add or remove while iterating; copy first if a loop may destroy a colony.</summary>
        public static List<WormColony> All => s_all;

        public static void Add(WormColony colony)
        {
            if (colony != null && !s_all.Contains(colony)) s_all.Add(colony);
        }

        public static void Remove(WormColony colony) => s_all.Remove(colony);

        /// <summary>The loaded colony whose network object has this id, or null (a mass finds its colony through it).</summary>
        public static WormColony FindById(ZDOID id)
        {
            if (id == ZDOID.None) return null;
            for (int i = 0; i < s_all.Count; i++)
            {
                WormColony c = s_all[i];
                if (c != null && c.ZdoId == id) return c;
            }
            return null;
        }

        /// <summary>
        /// The loaded colony with this stable key (see <see cref="MineKey"/>), or null; what a mass or a nest uses to find its colony, in every session. When two colonies of one mine
        /// are loaded (a duplicate that the deduplication has not removed yet) it is the one that will stay, so every caller agrees on it.
        /// </summary>
        public static WormColony FindByKey(long key)
        {
            if (key == 0L) return null;
            WormColony best = null;
            for (int i = 0; i < s_all.Count; i++)
            {
                WormColony c = s_all[i];
                if (c == null || c.Key != key) continue;
                if (best == null || MineOwnership.CompareKeeper(c.Candidate, best.Candidate) < 0) best = c;
            }
            return best;
        }

        /// <summary>The colony whose box contains <paramref name="pos"/>, or null (spec §8.4).</summary>
        public static WormColony FindFor(Vector3 pos)
        {
            for (int i = 0; i < s_all.Count; i++)
            {
                WormColony c = s_all[i];
                if (c != null && c.IsConfigured && WormMath.InsideBox(pos, c.BoxCenter, c.BoxHalfSize)) return c;
            }
            return null;
        }
    }
}
