using System;

namespace MistWorms.Colony
{
    /// <summary>
    /// How many worms of a mass come from each nest (spec §8.2, decision 198): one count per nest index plus a count of worms with no nest (lone worms,
    /// dissolved worms of unknown origin, dev masses). The owner of the mass keeps it in the ZDO (<c>mw_nests</c>) and keeps the sum equal to the number of
    /// worms of the mass: every operation here conserves or changes the sum by exactly the number asked. Pure data, no game access: unit tested.
    /// </summary>
    internal sealed class NestCounts
    {
        public const int Slots = 64;                 // WormColony.MaxNests
        public const int None = Slots;               // index of the "no nest" count
        private const byte Version = 1;

        private readonly int[] _c = new int[Slots + 1];

        /// <summary>Sum of all counts.</summary>
        public int Total { get; private set; }

        /// <summary>Count of nest <paramref name="nest"/>; any index outside 0..63 (-1 included) is the "no nest" count.</summary>
        public int this[int nest] => _c[Slot(nest)];

        private static int Slot(int nest) => nest >= 0 && nest < Slots ? nest : None;

        public void Clear()
        {
            Array.Clear(_c, 0, _c.Length);
            Total = 0;
        }

        public void CopyFrom(NestCounts other)
        {
            Array.Copy(other._c, _c, _c.Length);
            Total = other.Total;
        }

        public void Add(int nest, int k)
        {
            if (k <= 0) return;
            _c[Slot(nest)] += k;
            Total += k;
        }

        public void AddAll(NestCounts other)
        {
            for (int i = 0; i < _c.Length; i++) _c[i] += other._c[i];
            Total += other.Total;
        }

        /// <summary>
        /// Writes into <paramref name="into"/> (cleared first) the share of <paramref name="k"/> worms of this set, proportional to the counts, with the largest
        /// remainders rounded up (ties to the lowest index), so the shares always add up to min(k, Total). Does not change this set.
        /// </summary>
        public void CopyProrata(int k, NestCounts into)
        {
            into.Clear();
            if (k <= 0 || Total <= 0) return;
            if (k >= Total)
            {
                into.CopyFrom(this);
                return;
            }
            long total = Total;
            int given = 0;
            for (int i = 0; i < _c.Length; i++)
            {
                int share = (int)((long)_c[i] * k / total);
                into._c[i] = share;
                given += share;
            }
            Array.Clear(_boosted, 0, _boosted.Length);
            for (int left = k - given; left > 0; left--)
            {
                int best = -1;
                long bestRemainder = -1;
                for (int i = 0; i < _c.Length; i++)
                {
                    if (_boosted[i] || into._c[i] >= _c[i]) continue;
                    long remainder = (long)_c[i] * k % total;
                    if (remainder > bestRemainder) { bestRemainder = remainder; best = i; }
                }
                if (best < 0) break;
                into._c[best]++;
                _boosted[best] = true;      // one extra unit per slot at most
                given++;
            }
            into.Total = given;
        }

        private readonly bool[] _boosted = new bool[Slots + 1];

        /// <summary>Removes <paramref name="k"/> worms prorata (see <see cref="CopyProrata"/>), puts them in <paramref name="removed"/> when it is not null, and returns how many.</summary>
        public int TakeProrata(int k, NestCounts removed)
        {
            var share = removed ?? new NestCounts();
            CopyProrata(k, share);
            for (int i = 0; i < _c.Length; i++) _c[i] -= share._c[i];
            Total -= share.Total;
            return share.Total;
        }

        /// <summary>Takes one worm from the lowest non empty slot (the "no nest" slot last) and returns that slot, or -1 when the set is empty.</summary>
        public int TakeOne()
        {
            for (int i = 0; i < _c.Length; i++)
            {
                if (_c[i] <= 0) continue;
                _c[i]--;
                Total--;
                return i;
            }
            return -1;
        }

        /// <summary>
        /// The nest that most of the worms come from (the lowest index on a tie), or -1 when no worm has a nest or the worms with no nest are more numerous than
        /// the biggest nest share: the group of a mass (spec §5.1, plan 7-2).
        /// </summary>
        public int Majority()
        {
            int best = -1, bestCount = 0;
            for (int i = 0; i < Slots; i++)
            {
                if (_c[i] <= bestCount) continue;
                bestCount = _c[i];
                best = i;
            }
            return best >= 0 && bestCount >= _c[None] ? best : -1;
        }

        /// <summary>
        /// Takes <paramref name="n"/> worms from the set one by one (the lowest nest first, the "no nest" worms last) and writes the nest index of each into
        /// <paramref name="into"/>, -1 for a worm with no nest or once the set is empty: the nest of origin of the sleeping worms a mass is dissolved into. Exact,
        /// not prorata: each nest gets back exactly its count.
        /// </summary>
        public void DrainTo(short[] into, int n)
        {
            for (int k = 0; k < n; k++)
            {
                int origin = TakeOne();
                into[k] = origin >= 0 && origin < None ? (short)origin : (short)-1;
            }
        }

        /// <summary>Removes <paramref name="other"/> from this set, slot by slot, never below zero.</summary>
        public void Subtract(NestCounts other)
        {
            for (int i = 0; i < _c.Length; i++)
            {
                int d = Math.Min(_c[i], other._c[i]);
                _c[i] -= d;
                Total -= d;
            }
        }

        /// <summary>Makes the sum exactly <paramref name="n"/>: the missing worms are counted as having no nest, the surplus is removed prorata.</summary>
        public void FitTo(int n)
        {
            if (n < 0) n = 0;
            if (Total < n) Add(None, n - Total);
            else if (Total > n) TakeProrata(Total - n, null);
        }

        // ------------------------------------------------------------------ serialization

        /// <summary>version, number of non empty counts, then (slot byte, count ushort) each.</summary>
        public byte[] ToBytes()
        {
            int used = 0;
            for (int i = 0; i < _c.Length; i++) if (_c[i] > 0) used++;
            var data = new byte[2 + used * 3];
            data[0] = Version;
            data[1] = (byte)used;
            int at = 2;
            for (int i = 0; i < _c.Length; i++)
            {
                if (_c[i] <= 0) continue;
                int v = Math.Min(_c[i], ushort.MaxValue);
                data[at++] = (byte)i;
                data[at++] = (byte)(v & 0xff);
                data[at++] = (byte)(v >> 8);
            }
            return data;
        }

        /// <summary>Appends this set to a package that already holds a pool (<c>MW_MassAdd</c>, <c>MW_Dissolve</c>).</summary>
        public void AppendTo(ZPackage pkg) => pkg.Write(ToBytes());

        /// <summary>
        /// Reads the set that follows the pool in <paramref name="pkg"/> (the pool must have been read first: the cursor stands at its end). A package with no tail
        /// is an empty set (false), as is an unreadable one.
        /// </summary>
        public bool ReadTail(ZPackage pkg)
        {
            Clear();
            try
            {
                if (pkg == null || pkg.GetPos() >= pkg.Size()) return false;
                return ReadBytes(pkg.ReadByteArray());
            }
            catch (Exception)
            {
                Clear();
                return false;
            }
        }

        /// <summary>Reads <see cref="ToBytes"/> output. Null or empty is an empty set (true); an unknown version or a truncated array gives an empty set and false.</summary>
        public bool ReadBytes(byte[] data)
        {
            Clear();
            if (data == null || data.Length == 0) return true;
            if (data.Length < 2 || data[0] != Version || data.Length != 2 + data[1] * 3) return false;
            int at = 2;
            for (int n = 0; n < data[1]; n++)
            {
                int slot = data[at++];
                int v = data[at] | (data[at + 1] << 8);
                at += 2;
                if (slot > None) { Clear(); return false; }
                Add(slot, v);
            }
            return true;
        }
    }
}
