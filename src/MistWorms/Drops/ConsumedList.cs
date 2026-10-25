using System;

namespace MistWorms.Drops
{
    /// <summary>
    /// What one bite put into a pool, by resource (prefab hash, quantity): built on the client of the bitten player (spec §7.2) and sent to the
    /// owner of the mass or of the worm by <c>MW_Consumed</c> / <c>MW_WormConsumed</c> (plan 6, A9). Fixed arrays, no allocation after construction.
    /// The wire format is an int entry count, then (int prefab hash, float quantity) per entry; the receiver applies <see cref="TryRead"/>.
    /// </summary>
    internal sealed class ConsumedList
    {
        /// <summary>Distinct resources one bite can hold (a full set of gear is far under it; the surplus is dropped).</summary>
        public const int Capacity = 64;

        /// <summary>Entries per packet (plan 6, A9).</summary>
        public const int MaxEntriesPerPacket = 16;

        /// <summary>Largest quantity of one entry (plan 6, A9); the sender splits a bigger one into several entries.</summary>
        public const float MaxQtyPerEntry = 50f;

        public readonly int[] Hash = new int[Capacity];
        public readonly float[] Qty = new float[Capacity];
        public int Count;

        public void Clear() => Count = 0;

        /// <summary>Adds to the entry of <paramref name="hash"/> (created if needed). False when the quantity is not positive or the list is full.</summary>
        public bool Add(int hash, float qty)
        {
            if (!(qty > 0f) || float.IsInfinity(qty)) return false;
            for (int i = 0; i < Count; i++)
            {
                if (Hash[i] != hash) continue;
                Qty[i] += qty;
                return true;
            }
            if (Count >= Capacity) return false;
            Hash[Count] = hash;
            Qty[Count] = qty;
            Count++;
            return true;
        }

        public float Total
        {
            get
            {
                float sum = 0f;
                for (int i = 0; i < Count; i++) sum += Qty[i];
                return sum;
            }
        }

        /// <summary>
        /// Writes the next packet: at most <see cref="MaxEntriesPerPacket"/> entries of at most <see cref="MaxQtyPerEntry"/> each. <paramref name="entry"/>
        /// and <paramref name="done"/> are the cursor (entry index, quantity of it already written), start at 0. Returns false when everything was written.
        /// </summary>
        public bool WriteNextPacket(ZPackage pkg, ref int entry, ref float done)
        {
            if (entry >= Count) return false;
            int written = 0;
            var hashes = new int[MaxEntriesPerPacket];
            var qtys = new float[MaxEntriesPerPacket];
            while (entry < Count && written < MaxEntriesPerPacket)
            {
                float left = Qty[entry] - done;
                if (left <= 0f) { entry++; done = 0f; continue; }
                float piece = Math.Min(left, MaxQtyPerEntry);
                hashes[written] = Hash[entry];
                qtys[written] = piece;
                written++;
                done += piece;
                if (Qty[entry] - done <= 1e-6f) { entry++; done = 0f; }
            }
            pkg.Write(written);
            for (int i = 0; i < written; i++)
            {
                pkg.Write(hashes[i]);
                pkg.Write(qtys[i]);
            }
            return written > 0;
        }

        /// <summary>
        /// Reads a packet written by <see cref="WriteNextPacket"/> into <paramref name="into"/> (cleared first) with the validation of plan 6, A9: at most
        /// <see cref="MaxEntriesPerPacket"/> entries are read (the rest is ignored), an entry is kept only when its prefab is known (<paramref name="known"/>)
        /// and its quantity is finite, positive and at most <see cref="MaxQtyPerEntry"/>; a truncated package keeps what was read. Returns the number of
        /// entries ignored (0 = clean).
        /// </summary>
        public static int TryRead(ZPackage pkg, Func<int, bool> known, ConsumedList into)
        {
            into.Clear();
            int ignored = 0;
            try
            {
                pkg.SetPos(0);
                int count = pkg.ReadInt();
                if (count < 0) return 1;
                int read = Math.Min(count, MaxEntriesPerPacket);
                ignored = count - read;
                for (int i = 0; i < read; i++)
                {
                    int hash = pkg.ReadInt();
                    float qty = pkg.ReadSingle();
                    bool ok = qty > 0f && qty <= MaxQtyPerEntry && !float.IsNaN(qty) && known(hash);
                    if (!ok || !into.Add(hash, qty)) ignored++;
                }
            }
            catch (Exception)
            {
                ignored++;
            }
            return ignored;
        }
    }
}
