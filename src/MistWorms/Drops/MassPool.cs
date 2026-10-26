using System;

namespace MistWorms.Drops
{
    /// <summary>Whole items a pool hands out: up to <see cref="MassPool.MaxEntries"/> prefabs (hash) with an integer count. Fixed arrays, reused by the caller.</summary>
    internal sealed class PoolDrops
    {
        public readonly int[] Hash = new int[MassPool.MaxEntries];
        public readonly int[] Count = new int[MassPool.MaxEntries];
        public int N;

        public void Clear() => N = 0;

        /// <summary>Adds <paramref name="count"/> of <paramref name="hash"/> (merged into the existing entry); false when the table is full.</summary>
        public bool Add(int hash, int count)
        {
            if (count <= 0) return true;
            for (int i = 0; i < N; i++)
            {
                if (Hash[i] != hash) continue;
                Count[i] += count;
                return true;
            }
            if (N >= Hash.Length) return false;
            Hash[N] = hash;
            Count[N] = count;
            N++;
            return true;
        }
    }

    /// <summary>
    /// The loot pool of a mass or of a free worm (spec §7.1, §7.3, decisions.md 68, 79): <c>prefab hash -> quantity</c> plus a fractional remainder
    /// <c>carry</c> per resource, in fixed arrays (no allocation after construction). Pure logic, no game access, so it is unit tested.
    /// <para>The share of the worms that die is computed on the quantities as <c>pool x k / n</c> and moved to <c>carry</c>; the whole items of
    /// <c>carry</c> are dropped. The total handed out is therefore the floor of the cumulative share, whatever the split of the hits (one area blow
    /// or worm by worm). The last worm hands out the floor of everything left (<see cref="HarvestAll"/>); the fraction is lost, never rounded up.</para>
    /// </summary>
    internal sealed class MassPool
    {
        /// <summary>Distinct resources a pool can hold (plan 6, A10).</summary>
        public const int MaxEntries = 16;

        /// <summary>First byte of the serialized form.</summary>
        public const byte FormatVersion = 1;

        /// <summary>Float noise tolerated when a quantity is floored (a sum of shares that should be exactly 4 must give 4, not 3).</summary>
        public const float FloorTolerance = 1e-3f;

        /// <summary>Entries holding less than this are dust and are removed.</summary>
        private const float Dust = 1e-4f;

        private readonly int[] _hash = new int[MaxEntries];
        private readonly float[] _qty = new float[MaxEntries];
        private readonly float[] _carry = new float[MaxEntries];
        private int _count;

        public int Count => _count;
        public bool IsEmpty => _count == 0;
        public int HashAt(int i) => _hash[i];
        public float QtyAt(int i) => _qty[i];
        public float CarryAt(int i) => _carry[i];

        public void Clear() => _count = 0;

        public void CopyFrom(MassPool other)
        {
            _count = other._count;
            Array.Copy(other._hash, _hash, _count);
            Array.Copy(other._qty, _qty, _count);
            Array.Copy(other._carry, _carry, _count);
        }

        /// <summary>Quantity plus carry of one resource (0 when absent).</summary>
        public float Total(int hash)
        {
            int i = IndexOf(hash);
            return i < 0 ? 0f : _qty[i] + _carry[i];
        }

        /// <summary>Sum of quantity and carry over every resource.</summary>
        public float TotalAll()
        {
            float sum = 0f;
            for (int i = 0; i < _count; i++) sum += _qty[i] + _carry[i];
            return sum;
        }

        private int IndexOf(int hash)
        {
            for (int i = 0; i < _count; i++)
                if (_hash[i] == hash) return i;
            return -1;
        }

        private int Slot(int hash)
        {
            int i = IndexOf(hash);
            if (i >= 0) return i;
            if (_count >= MaxEntries) return -1;
            _hash[_count] = hash;
            _qty[_count] = 0f;
            _carry[_count] = 0f;
            return _count++;
        }

        // ------------------------------------------------------------------ adding

        /// <summary>pool[hash] += qty. False (nothing added) when the quantity is not positive and finite, or when the table is full.</summary>
        public bool Add(int hash, float qty)
        {
            if (!(qty > 0f) || float.IsInfinity(qty)) return false;
            int i = Slot(hash);
            if (i < 0) return false;
            _qty[i] += qty;
            return true;
        }

        /// <summary>Adds everything of <paramref name="other"/> (quantities and carries): absorption, fusion. Returns the number of entries that did not fit.</summary>
        public int Merge(MassPool other)
        {
            int lost = 0;
            for (int k = 0; k < other._count; k++)
            {
                int i = Slot(other._hash[k]);
                if (i < 0) { lost++; continue; }
                _qty[i] += other._qty[k];
                _carry[i] += other._carry[k];
            }
            return lost;
        }

        /// <summary>
        /// Moves the fraction <paramref name="k"/>/<paramref name="n"/> of this pool (quantities and carries) to <paramref name="dest"/> (partial
        /// merge, decisions.md 79). <paramref name="k"/> &gt;= <paramref name="n"/> moves everything. Returns the entries that did not fit in dest.
        /// </summary>
        public int MoveFraction(MassPool dest, int k, int n)
        {
            if (k <= 0 || n <= 0 || _count == 0) return 0;
            if (k >= n)
            {
                int lostAll = dest.Merge(this);
                _count = 0;
                return lostAll;
            }
            float f = (float)k / n;
            int lost = 0;
            for (int e = 0; e < _count; e++)
            {
                float q = _qty[e] * f, c = _carry[e] * f;
                int i = dest.Slot(_hash[e]);
                if (i < 0) { lost++; }
                else
                {
                    dest._qty[i] += q;
                    dest._carry[i] += c;
                }
                _qty[e] -= q;
                _carry[e] -= c;
            }
            Compact();
            return lost;
        }

        /// <summary>
        /// <paramref name="part"/> becomes the <c>1/parts</c> of this pool (dissolution: equal shares for the worms released, fractions kept). This pool is not changed.
        /// </summary>
        public void DivideInto(int parts, MassPool part)
        {
            if (parts < 1) parts = 1;
            part._count = _count;
            float f = 1f / parts;
            for (int e = 0; e < _count; e++)
            {
                part._hash[e] = _hash[e];
                part._qty[e] = _qty[e] * f;
                part._carry[e] = _carry[e] * f;
            }
        }

        // ------------------------------------------------------------------ the share of the worms that die

        /// <summary>
        /// <c>k</c> of the <c>n</c> worms die: share = pool x k / n moves from the quantity to the carry. <paramref name="k"/> &gt;= <paramref name="n"/>
        /// moves everything. Does not drop anything: <see cref="Harvest"/> does.
        /// </summary>
        public void Share(int k, int n)
        {
            if (k <= 0 || n <= 0) return;
            if (k >= n)
            {
                for (int e = 0; e < _count; e++) { _carry[e] += _qty[e]; _qty[e] = 0f; }
                return;
            }
            float f = (float)k / n;
            for (int e = 0; e < _count; e++)
            {
                float share = _qty[e] * f;
                _qty[e] -= share;
                _carry[e] += share;
            }
        }

        /// <summary>The whole items of every carry go to <paramref name="drops"/> (floor with the float tolerance) and leave the carry.</summary>
        public void Harvest(PoolDrops drops)
        {
            for (int e = 0; e < _count; e++)
            {
                int part = FloorTol(_carry[e]);
                if (part <= 0) continue;
                if (!drops.Add(_hash[e], part)) continue;   // table full: stays in the carry for the next time
                _carry[e] = Math.Max(0f, _carry[e] - part);
            }
            Compact();
        }

        /// <summary>
        /// Last worm: floor(quantity + carry) of every resource goes to <paramref name="drops"/>; the pool is emptied, the fraction is lost.
        /// </summary>
        public void HarvestAll(PoolDrops drops)
        {
            for (int e = 0; e < _count; e++) drops.Add(_hash[e], FloorTol(_qty[e] + _carry[e]));
            _count = 0;
        }

        /// <summary>Puts back <paramref name="count"/> items that could not be dropped now (drop limit): they wait in the carry for the next worm.</summary>
        public void Return(int hash, int count)
        {
            if (count <= 0) return;
            int i = Slot(hash);
            if (i < 0) return;
            _carry[i] += count;
        }

        /// <summary>floor(x) that forgives float noise: 3.9995 gives 4.</summary>
        public static int FloorTol(float x)
        {
            if (!(x > 0f)) return 0;
            return (int)Math.Floor(x + FloorTolerance);
        }

        private void Compact()
        {
            int w = 0;
            for (int r = 0; r < _count; r++)
            {
                if (_qty[r] + _carry[r] <= Dust) continue;
                if (w != r)
                {
                    _hash[w] = _hash[r];
                    _qty[w] = _qty[r];
                    _carry[w] = _carry[r];
                }
                w++;
            }
            _count = w;
        }

        // ------------------------------------------------------------------ serialization (ZDO mw_pool, Dissolve and MassAdd packages)

        /// <summary>version byte, entry count byte, then (int hash, float quantity, float carry) per entry. An empty pool writes the two bytes.</summary>
        public void Write(ZPackage pkg)
        {
            pkg.Write(FormatVersion);
            pkg.Write((byte)_count);
            for (int i = 0; i < _count; i++)
            {
                pkg.Write(_hash[i]);
                pkg.Write(_qty[i]);
                pkg.Write(_carry[i]);
            }
        }

        /// <summary>
        /// Reads a pool written by <see cref="Write"/> into this one (cleared first). An unknown version, a count over the limit, a truncated
        /// package or a non finite / negative value gives false and an empty pool; the caller logs. An empty package is an empty pool (true).
        /// </summary>
        public bool Read(ZPackage pkg)
        {
            _count = 0;
            try
            {
                if (pkg == null || pkg.Size() == 0) return true;
                pkg.SetPos(0);
                byte version = pkg.ReadByte();
                if (version != FormatVersion) return false;
                int count = pkg.ReadByte();
                if (count > MaxEntries) return false;
                for (int i = 0; i < count; i++)
                {
                    int hash = pkg.ReadInt();
                    float q = pkg.ReadSingle(), c = pkg.ReadSingle();
                    if (!IsGood(q) || !IsGood(c)) { _count = 0; return false; }
                    if (q + c <= 0f) continue;
                    int e = Slot(hash);
                    if (e < 0) { _count = 0; return false; }
                    _qty[e] += q;
                    _carry[e] += c;
                }
                return true;
            }
            catch (Exception)
            {
                _count = 0;
                return false;
            }
        }

        /// <summary>The ZDO form: the bytes of a package written by <see cref="Write"/>.</summary>
        public byte[] ToBytes()
        {
            var pkg = new ZPackage();
            Write(pkg);
            return pkg.GetArray();
        }

        // The same bytes as Write / Read, into and out of a byte array at an offset (the colony snapshot embeds pools without allocating, phase 7-3).

        /// <summary>Bytes <see cref="WriteTo"/> writes.</summary>
        public int SerializedSize => 2 + _count * 12;

        /// <summary>Writes the form of <see cref="Write"/> at <paramref name="at"/> (room checked by the caller); returns the position after it.</summary>
        public int WriteTo(byte[] buf, int at)
        {
            buf[at++] = FormatVersion;
            buf[at++] = (byte)_count;
            for (int i = 0; i < _count; i++)
            {
                MistWorms.Colony.ByteIO.PutInt(buf, at, _hash[i]);
                MistWorms.Colony.ByteIO.PutFloat(buf, at + 4, _qty[i]);
                MistWorms.Colony.ByteIO.PutFloat(buf, at + 8, _carry[i]);
                at += 12;
            }
            return at;
        }

        /// <summary>
        /// Reads a pool written by <see cref="WriteTo"/> (or <see cref="Write"/>) at <paramref name="at"/>, which moves past it. Same refusals as <see cref="Read"/>,
        /// plus a truncated array; the pool is then empty.
        /// </summary>
        public bool ReadFrom(byte[] data, ref int at)
        {
            _count = 0;
            if (data == null || at < 0 || at + 2 > data.Length || data[at] != FormatVersion) return false;
            int count = data[at + 1];
            if (count > MaxEntries || at + 2 + count * 12 > data.Length) return false;
            at += 2;
            for (int i = 0; i < count; i++, at += 12)
            {
                int hash = MistWorms.Colony.ByteIO.GetInt(data, at);
                float q = MistWorms.Colony.ByteIO.GetFloat(data, at + 4), c = MistWorms.Colony.ByteIO.GetFloat(data, at + 8);
                if (!IsGood(q) || !IsGood(c)) { _count = 0; return false; }
                if (q + c <= 0f) continue;
                int e = Slot(hash);
                if (e < 0) { _count = 0; return false; }
                _qty[e] += q;
                _carry[e] += c;
            }
            return true;
        }

        public bool ReadBytes(byte[] data) => data == null || data.Length == 0 ? FinishEmpty() : Read(new ZPackage(data));

        private bool FinishEmpty()
        {
            _count = 0;
            return true;
        }

        private static bool IsGood(float v) => v >= 0f && !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
