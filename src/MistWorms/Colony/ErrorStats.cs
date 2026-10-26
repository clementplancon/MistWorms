using System;
using System.Collections.Generic;

namespace MistWorms.Colony
{
    /// <summary>
    /// Mean and percentile of a list of errors (metres between the mirror of a peer and the owner, S17 of the phase 8-2). Pure and unit tested: the scenario only feeds it. A percentile is
    /// the nearest rank (the smallest value such that at least <c>p</c> percent of the samples are at or below it), so p95 of 100 samples is the 95th smallest.
    /// </summary>
    internal static class ErrorStats
    {
        public static double Mean(IList<float> v)
        {
            if (v == null || v.Count == 0) return 0.0;
            double sum = 0.0;
            for (int i = 0; i < v.Count; i++) sum += v[i];
            return sum / v.Count;
        }

        /// <summary>Nearest rank percentile, <paramref name="percent"/> in (0, 100]; 0 for an empty list. The list is not modified.</summary>
        public static float Percentile(IList<float> v, double percent)
        {
            if (v == null || v.Count == 0) return 0f;
            var sorted = new float[v.Count];
            for (int i = 0; i < sorted.Length; i++) sorted[i] = v[i];
            Array.Sort(sorted);
            int rank = (int)Math.Ceiling(percent / 100.0 * sorted.Length);
            if (rank < 1) rank = 1;
            if (rank > sorted.Length) rank = sorted.Length;
            return sorted[rank - 1];
        }

        public static float Max(IList<float> v)
        {
            float m = 0f;
            if (v == null) return m;
            for (int i = 0; i < v.Count; i++) if (v[i] > m) m = v[i];
            return m;
        }
    }
}
