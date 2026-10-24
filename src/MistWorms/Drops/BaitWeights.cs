using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace MistWorms.Drops
{
    /// <summary>A fishing bait found in the ObjectDB and the number of trophy ingredients of its recipe.</summary>
    internal readonly struct BaitCandidate
    {
        public readonly string Prefab;
        public readonly int TrophyIngredients;

        public BaitCandidate(string prefab, int trophyIngredients)
        {
            Prefab = prefab;
            TrophyIngredients = trophyIngredients;
        }
    }

    /// <summary>Weighted list of bait prefabs (Drops.BaitWeights). Immutable; picking is O(n) over a handful of entries.</summary>
    internal sealed class BaitTable
    {
        public readonly string[] Names;
        public readonly float[] Weights;
        public readonly float Total;

        public BaitTable(string[] names, float[] weights)
        {
            Names = names;
            Weights = weights;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) total += weights[i];
            Total = total;
        }

        public int Count => Names.Length;

        /// <summary>Entry for a uniform <paramref name="unit"/> in [0, 1): the name whose cumulative weight range contains it; null if empty.</summary>
        public string Pick(double unit)
        {
            if (Names.Length == 0 || Total <= 0f) return null;
            double target = unit * Total;
            double acc = 0.0;
            for (int i = 0; i < Names.Length; i++)
            {
                acc += Weights[i];
                if (target < acc) return Names[i];
            }
            return Names[Names.Length - 1];
        }
    }

    /// <summary>Generation and parsing of the Drops.BaitWeights list (spec §7.5, decisions.md 12). Pure: unit tested.</summary>
    internal static class BaitWeights
    {
        public const float BaseWeight = 40f;       // FishingBait, bought from Haldor
        public const float MistlandsWeight = 25f;
        public const float MinOtherWeight = 2f;
        public const float OtherNumerator = 10f;

        /// <summary>Weight of one bait: base 40, Mistlands 25, any other 10 / (1 + T) rounded, at least 2.</summary>
        public static float WeightOf(string prefab, int trophyIngredients, string basePrefab, string mistlandsPrefab)
        {
            if (string.Equals(prefab, basePrefab, StringComparison.Ordinal)) return BaseWeight;
            if (string.Equals(prefab, mistlandsPrefab, StringComparison.Ordinal)) return MistlandsWeight;
            double w = Math.Round(OtherNumerator / (1.0 + Math.Max(0, trophyIngredients)), MidpointRounding.AwayFromZero);
            return (float)Math.Max(MinOtherWeight, w);
        }

        /// <summary>The "prefab:weight,prefab:weight" text for the given baits, sorted by name so the output is stable.</summary>
        public static string Compute(IList<BaitCandidate> baits, string basePrefab, string mistlandsPrefab)
        {
            var sorted = new List<BaitCandidate>(baits);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Prefab, b.Prefab));
            var sb = new StringBuilder();
            foreach (BaitCandidate b in sorted)
            {
                if (string.IsNullOrEmpty(b.Prefab)) continue;
                if (sb.Length > 0) sb.Append(',');
                sb.Append(b.Prefab).Append(':').Append(WeightOf(b.Prefab, b.TrophyIngredients, basePrefab, mistlandsPrefab).ToString("0.##", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parses "prefab:weight,prefab:weight". A malformed or non positive entry is skipped and described in <paramref name="problems"/>
        /// (may be null). A duplicated prefab keeps its last weight.
        /// </summary>
        public static List<KeyValuePair<string, float>> Parse(string text, List<string> problems)
        {
            var result = new List<KeyValuePair<string, float>>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (string raw in text.Split(','))
            {
                string entry = raw.Trim();
                if (entry.Length == 0) continue;
                int colon = entry.LastIndexOf(':');
                if (colon <= 0 || colon == entry.Length - 1)
                {
                    problems?.Add($"'{entry}' is not prefab:weight");
                    continue;
                }
                string name = entry.Substring(0, colon).Trim();
                if (!float.TryParse(entry.Substring(colon + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float weight)
                    || float.IsNaN(weight) || float.IsInfinity(weight) || weight <= 0f)
                {
                    problems?.Add($"'{entry}' has no positive weight");
                    continue;
                }
                int existing = result.FindIndex(kv => kv.Key == name);
                if (existing >= 0) result[existing] = new KeyValuePair<string, float>(name, weight);
                else result.Add(new KeyValuePair<string, float>(name, weight));
            }
            return result;
        }

        /// <summary>Table from parsed entries, dropping the names <paramref name="isKnown"/> refuses (unknown prefab: warned by the caller).</summary>
        public static BaitTable Build(List<KeyValuePair<string, float>> entries, Func<string, bool> isKnown, List<string> unknown)
        {
            var names = new List<string>();
            var weights = new List<float>();
            foreach (KeyValuePair<string, float> kv in entries)
            {
                if (isKnown != null && !isKnown(kv.Key))
                {
                    unknown?.Add(kv.Key);
                    continue;
                }
                names.Add(kv.Key);
                weights.Add(kv.Value);
            }
            return new BaitTable(names.ToArray(), weights.ToArray());
        }
    }
}
