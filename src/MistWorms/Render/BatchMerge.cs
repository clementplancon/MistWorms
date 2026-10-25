using System;
using UnityEngine;

namespace MistWorms.Render
{
    /// <summary>Matrices queued for one batch (one mesh, one material, one shadow mode) plus their world bounds.</summary>
    internal sealed class InstanceList
    {
        public const int MaxInstancesPerCall = 1023;
        public const int MaxInstancesPerList = 16384;

        public Matrix4x4[] Matrices = new Matrix4x4[64];
        public int Count;
        private Vector3 _min, _max;

        public void Reset()
        {
            Count = 0;
            _min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        }

        public bool Add(in Matrix4x4 m, Vector3 center, float radius)
        {
            if (Count == Matrices.Length)
            {
                if (Count >= MaxInstancesPerList) return false;
                Array.Resize(ref Matrices, Count * 2); // amortised: only while the working set grows
            }
            Matrices[Count++] = m;
            if (center.x - radius < _min.x) _min.x = center.x - radius;
            if (center.y - radius < _min.y) _min.y = center.y - radius;
            if (center.z - radius < _min.z) _min.z = center.z - radius;
            if (center.x + radius > _max.x) _max.x = center.x + radius;
            if (center.y + radius > _max.y) _max.y = center.y + radius;
            if (center.z + radius > _max.z) _max.z = center.z + radius;
            return true;
        }

        /// <summary>Appends every instance of <paramref name="other"/> and empties it. Returns the number refused by the list cap.</summary>
        public int TakeFrom(InstanceList other)
        {
            int n = other.Count;
            if (n == 0) return 0;
            int room = MaxInstancesPerList - Count;
            int take = n < room ? n : room;
            if (take > 0)
            {
                int need = Count + take;
                if (need > Matrices.Length)
                {
                    int cap = Matrices.Length;
                    while (cap < need) cap *= 2;
                    Array.Resize(ref Matrices, cap); // amortised, like Add
                }
                Array.Copy(other.Matrices, 0, Matrices, Count, take);
                Count += take;
                if (other._min.x < _min.x) _min.x = other._min.x;
                if (other._min.y < _min.y) _min.y = other._min.y;
                if (other._min.z < _min.z) _min.z = other._min.z;
                if (other._max.x > _max.x) _max.x = other._max.x;
                if (other._max.y > _max.y) _max.y = other._max.y;
                if (other._max.z > _max.z) _max.z = other._max.z;
            }
            other.Reset();
            return n - take;
        }

        /// <summary>
        /// Appends <paramref name="count"/> matrices of <paramref name="src"/> in one block copy with their precomputed world bounds. Returns the number
        /// refused by the list cap.
        /// </summary>
        public int AddRange(Matrix4x4[] src, int count, Vector3 min, Vector3 max)
        {
            if (count <= 0) return 0;
            int room = MaxInstancesPerList - Count;
            int take = count < room ? count : room;
            if (take > 0)
            {
                int need = Count + take;
                if (need > Matrices.Length)
                {
                    int cap = Matrices.Length;
                    while (cap < need) cap *= 2;
                    Array.Resize(ref Matrices, cap); // amortised, like Add
                }
                Array.Copy(src, 0, Matrices, Count, take);
                Count += take;
                if (min.x < _min.x) _min.x = min.x;
                if (min.y < _min.y) _min.y = min.y;
                if (min.z < _min.z) _min.z = min.z;
                if (max.x > _max.x) _max.x = max.x;
                if (max.y > _max.y) _max.y = max.y;
                if (max.z > _max.z) _max.z = max.z;
            }
            return count - take;
        }

        public Bounds Bounds()
        {
            var b = new Bounds();
            b.SetMinMax(_min, _max);
            return b;
        }

        /// <summary>RenderMeshInstanced calls needed for this list.</summary>
        public int Calls => (Count + MaxInstancesPerCall - 1) / MaxInstancesPerCall;
    }

    /// <summary>
    /// Draw call guard (phase 4B-3): when the non-empty lists would need more than the budget, the odd frames of
    /// flipbook groups are folded into the even ones, group by group in the given priority order. Pure and allocation-free.
    /// </summary>
    internal static class BatchMerge
    {
        /// <summary>
        /// <paramref name="groups"/> are the mergeable flipbooks (one array of frames each), cheapest to degrade first.
        /// <paramref name="fixedLists"/> only count towards the budget. Returns the number of lists folded away.
        /// <paramref name="dropped"/> receives the instances refused by the per-list cap (normally 0).
        /// </summary>
        public static int Apply(InstanceList[][] groups, InstanceList[] fixedLists, int budget, out int dropped)
        {
            dropped = 0;
            int used = 0;
            for (int i = 0; i < fixedLists.Length; i++) used += fixedLists[i].Calls;
            for (int g = 0; g < groups.Length; g++)
            {
                InstanceList[] frames = groups[g];
                for (int f = 0; f < frames.Length; f++) used += frames[f].Calls;
            }

            int merged = 0;
            for (int g = 0; g < groups.Length && used > budget; g++)
            {
                InstanceList[] frames = groups[g];
                for (int f = 1; f < frames.Length && used > budget; f += 2)
                {
                    InstanceList even = frames[f - 1], odd = frames[f];
                    // With an empty side the call count cannot drop: keep the exact pose.
                    if (even.Count == 0 || odd.Count == 0) continue;
                    int before = even.Calls + odd.Calls;
                    dropped += even.TakeFrom(odd);
                    used += even.Calls - before;
                    merged++;
                }
            }
            return merged;
        }
    }
}
