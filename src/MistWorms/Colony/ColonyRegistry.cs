using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>The colonies currently loaded on this peer (added in Awake, removed in OnDestroy).</summary>
    internal static class ColonyRegistry
    {
        private static readonly List<WormColony> s_all = new List<WormColony>();

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
