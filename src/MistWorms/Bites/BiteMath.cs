using System;
using System.Collections.Generic;

namespace MistWorms.Bites
{
    /// <summary>An item that can lose durability, abstracted from the game's ItemData (the adapter fills the id).</summary>
    internal struct BiteItem
    {
        public int Id;
        public float Durability;
        public bool IsWisplight;
    }

    /// <summary>Durability taken from the item <see cref="Id"/> by one bite.</summary>
    internal struct BiteLoss
    {
        public int Id;
        public float Amount;
    }

    internal struct BiteResult
    {
        /// <summary>At least one item could be bitten (Wisplight rule applied). False = nothing to devour: the damage goes to health.</summary>
        public bool HadEligible;
        /// <summary>Part of P that no eligible item could absorb.</summary>
        public float Remaining;
        /// <summary>Durability actually removed.</summary>
        public float Eaten;
    }

    /// <summary>
    /// Which pieces a bite eats and how much (spec §6.3 points 1 to 4). Pure logic: the caller passes the items with durability left
    /// (already filtered on m_useDurability, m_durability &gt; 0 and the equipped-only option) and applies the returned losses.
    /// </summary>
    internal static class BiteMath
    {
        private const float Epsilon = 1e-4f;

        /// <summary>k = clamp(ceil(itemsPerTen x n / 10), 1, eligible), spec §6.3 point 2 (plan 5, A6).</summary>
        public static int PiecesPerBite(int wormCount, int itemsPerTen, int eligible)
        {
            long k = ((long)Math.Max(1, wormCount) * Math.Max(1, itemsPerTen) + 9L) / 10L;
            return (int)Math.Max(1L, Math.Min(k, Math.Max(1, eligible)));
        }

        /// <summary>
        /// Distributes <paramref name="p"/> durability points over random eligible items. <paramref name="losses"/> is cleared and filled
        /// (one entry per item that lost something). The Wisplight (when <paramref name="wisplightLast"/> is set) only takes what the other
        /// items could not absorb, in the same tick; what nothing could absorb is <see cref="BiteResult.Remaining"/> and goes to health
        /// (plan 5, A7).
        /// </summary>
        public static BiteResult Select(IList<BiteItem> items, float p, int wormCount, int itemsPerTen, bool wisplightLast, Random rng, List<BiteLoss> losses)
        {
            losses.Clear();
            var result = new BiteResult { Remaining = Math.Max(0f, p) };

            var normal = new List<int>(items.Count);
            var wisp = new List<int>(2);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Durability <= 0f) continue;
                if (wisplightLast && items[i].IsWisplight) wisp.Add(i);
                else normal.Add(i);
            }
            result.HadEligible = normal.Count > 0 || wisp.Count > 0;
            if (!result.HadEligible || result.Remaining <= 0f) return result;

            var taken = new float[items.Count];
            float remaining = result.Remaining;
            if (normal.Count > 0) remaining = Eat(items, normal, taken, remaining, wormCount, itemsPerTen, rng);
            if (remaining > Epsilon && wisp.Count > 0) remaining = Eat(items, wisp, taken, remaining, wormCount, itemsPerTen, rng);

            float eaten = 0f;
            for (int i = 0; i < taken.Length; i++)
            {
                if (taken[i] <= 0f) continue;
                losses.Add(new BiteLoss { Id = items[i].Id, Amount = taken[i] });
                eaten += taken[i];
            }
            result.Eaten = eaten;
            result.Remaining = Math.Max(0f, remaining);
            return result;
        }

        /// <summary>One distribution over <paramref name="pool"/> (indices into items): k items drawn, then the excess; returns what is left of <paramref name="p"/>.</summary>
        private static float Eat(IList<BiteItem> items, List<int> pool, float[] taken, float p, int wormCount, int itemsPerTen, Random rng)
        {
            int count = pool.Count;
            var left = new float[count];
            for (int e = 0; e < count; e++) left[e] = items[pool[e]].Durability - taken[pool[e]];

            // k items drawn without replacement (partial Fisher-Yates), each asked for P / k and capped by what it has left.
            int k = PiecesPerBite(wormCount, itemsPerTen, count);
            var order = new int[count];
            for (int e = 0; e < count; e++) order[e] = e;
            float remaining = p;
            float share = remaining / k;
            for (int c = 0; c < k; c++)
            {
                int j = c + rng.Next(count - c);
                int pick = order[j];
                order[j] = order[c];
                order[c] = pick;

                float take = Math.Min(share, left[pick]);
                left[pick] -= take;
                taken[pool[pick]] += take;
                remaining -= take;
            }

            // The excess goes to other random eligible items until P or the items run out.
            while (remaining > Epsilon)
            {
                int candidates = 0;
                for (int e = 0; e < count; e++)
                    if (left[e] > Epsilon) candidates++;
                if (candidates == 0) break;

                int nth = rng.Next(candidates);
                int target = -1;
                for (int e = 0; e < count; e++)
                {
                    if (left[e] <= Epsilon) continue;
                    if (nth-- == 0) { target = e; break; }
                }
                float take = Math.Min(remaining, left[target]);
                left[target] -= take;
                taken[pool[target]] += take;
                remaining -= take;
            }
            return remaining;
        }
    }
}
