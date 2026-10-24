using System;
using UnityEngine;

namespace MistWorms.Colony
{
    /// <summary>
    /// Uniform 2 m grid over the xz plane (spec §11.1) with an open-addressing hash table and one intrusive linked
    /// list per cell. All arrays are sized at construction; <see cref="Clear"/>, <see cref="Insert"/> and
    /// <see cref="QueryNearest"/> never allocate. Pure, so it is unit tested.
    /// </summary>
    internal sealed class SpatialGrid
    {
        public const float CellSize = 2f;

        private readonly int _mask;
        private readonly bool[] _used;
        private readonly int[] _keyX;
        private readonly int[] _keyZ;
        private readonly int[] _head;
        private readonly int[] _next;

        public int Count { get; private set; }

        /// <param name="capacity">Highest item index + 1 that will ever be inserted.</param>
        public SpatialGrid(int capacity)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            int size = 16;
            while (size < capacity * 2) size <<= 1; // at most one cell per item, so the table is never more than half full
            _mask = size - 1;
            _used = new bool[size];
            _keyX = new int[size];
            _keyZ = new int[size];
            _head = new int[size];
            _next = new int[capacity];
        }

        public void Clear()
        {
            Array.Clear(_used, 0, _used.Length);
            Count = 0;
        }

        public static int CellOf(float v) => (int)Math.Floor(v / CellSize);

        public void Insert(int item, float x, float z)
        {
            if ((uint)item >= (uint)_next.Length) return;
            int slot = FindSlot(CellOf(x), CellOf(z), create: true);
            _next[item] = _head[slot];
            _head[slot] = item;
            Count++;
        }

        /// <summary>
        /// Finds up to <paramref name="k"/> items closest to (x, y, z) within <paramref name="radius"/> in the xz plane and
        /// <paramref name="maxDy"/> in height, excluding <paramref name="exclude"/>. Results are sorted by distance
        /// in <paramref name="outIdx"/> / <paramref name="outSqrDist"/>, both at least k long. Returns the count.
        /// </summary>
        public int QueryNearest(float x, float y, float z, float radius, float maxDy, int exclude, Vector3[] positions,
            int k, int[] outIdx, float[] outSqrDist)
        {
            if (k <= 0) return 0;
            float r2 = radius * radius;
            int cx0 = CellOf(x - radius), cx1 = CellOf(x + radius);
            int cz0 = CellOf(z - radius), cz1 = CellOf(z + radius);
            int found = 0;

            for (int cx = cx0; cx <= cx1; cx++)
            {
                for (int cz = cz0; cz <= cz1; cz++)
                {
                    int slot = FindSlot(cx, cz, create: false);
                    if (slot < 0) continue;
                    for (int item = _head[slot]; item >= 0; item = _next[item])
                    {
                        if (item == exclude) continue;
                        Vector3 p = positions[item];
                        float dx = p.x - x, dz = p.z - z;
                        float d2 = dx * dx + dz * dz;
                        if (d2 > r2 || Math.Abs(p.y - y) > maxDy) continue;

                        // Insertion into the sorted result list; drops the farthest when full.
                        int at = found < k ? found : k - 1;
                        if (found == k && d2 >= outSqrDist[k - 1]) continue;
                        while (at > 0 && outSqrDist[at - 1] > d2)
                        {
                            outIdx[at] = outIdx[at - 1];
                            outSqrDist[at] = outSqrDist[at - 1];
                            at--;
                        }
                        outIdx[at] = item;
                        outSqrDist[at] = d2;
                        if (found < k) found++;
                    }
                }
            }
            return found;
        }

        private int FindSlot(int cx, int cz, bool create)
        {
            int h = unchecked(cx * 73856093 ^ cz * 19349663) & _mask;
            while (true)
            {
                if (!_used[h])
                {
                    if (!create) return -1;
                    _used[h] = true;
                    _keyX[h] = cx;
                    _keyZ[h] = cz;
                    _head[h] = -1;
                    return h;
                }
                if (_keyX[h] == cx && _keyZ[h] == cz) return h;
                h = (h + 1) & _mask;
            }
        }
    }
}
