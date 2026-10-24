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

        /// <summary>k = clamp(ceil(n / 10) * itemsPerTen, 1, eligible), spec §6.3 point 2.</summary>
        public static int PiecesPerBite(int wormCount, int itemsPerTen, int eligible)
        {
            int k = ((Math.Max(1, wormCount) + 9) / 10) * Math.Max(1, itemsPerTen);
            return Math.Max(1, Math.Min(k, eligible));
        }

        /// <summary>
        /// Distributes <paramref name="p"/> durability points over random eligible items. <paramref name="losses"/> is cleared and filled
        /// (one entry per item that lost something). The Wisplight is eligible only when it is the only item left, when
        /// <paramref name="wisplightLast"/> is set.
        /// </summary>
        public static BiteResult Select(IList<BiteItem> items, float p, int wormCount, int itemsPerTen, bool wisplightLast, Random rng, List<BiteLoss> losses)
        {
            losses.Clear();
            var result = new BiteResult { Remaining = Math.Max(0f, p) };

            // Eligible set: every item with durability left; the Wisplight only if nothing else remains.
            var eligible = new List<int>(items.Count);
            int wisplights = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Durability <= 0f) continue;
                if (wisplightLast && items[i].IsWisplight) { wisplights++; continue; }
                eligible.Add(i);
            }
            if (eligible.Count == 0 && wisplights > 0)
            {
                for (int i = 0; i < items.Count; i++)
                    if (items[i].Durability > 0f && items[i].IsWisplight) eligible.Add(i);
            }
            if (eligible.Count == 0 || result.Remaining <= 0f)
            {
                result.HadEligible = eligible.Count > 0;
                return result;
            }
            result.HadEligible = true;

            int count = eligible.Count;
            var left = new float[count];
            var taken = new float[count];
            for (int e = 0; e < count; e++) left[e] = items[eligible[e]].Durability;

            // k items drawn without replacement (partial Fisher-Yates), each asked for P / k and capped by what it has left.
            int k = PiecesPerBite(wormCount, itemsPerTen, count);
            var order = new int[count];
            for (int e = 0; e < count; e++) order[e] = e;
            float remaining = result.Remaining;
            float share = remaining / k;
            for (int c = 0; c < k; c++)
            {
                int j = c + rng.Next(count - c);
                int pick = order[j];
                order[j] = order[c];
                order[c] = pick;

                float take = Math.Min(share, left[pick]);
                left[pick] -= take;
                taken[pick] += take;
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
                taken[target] += take;
                remaining -= take;
            }

            float eaten = 0f;
            for (int e = 0; e < count; e++)
            {
                if (taken[e] <= 0f) continue;
                losses.Add(new BiteLoss { Id = items[eligible[e]].Id, Amount = taken[e] });
                eaten += taken[e];
            }
            result.Eaten = eaten;
            result.Remaining = Math.Max(0f, remaining);
            return result;
        }
    }
}
