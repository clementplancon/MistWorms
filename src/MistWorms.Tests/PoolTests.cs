using System;
using System.Collections.Generic;
using MistWorms.Colony;
using MistWorms.Drops;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Pool maths (spec §7.1 to §7.3, decisions 68 and 79), vanilla cost (plan 6, A1), serialization and RPC validation (A9, A10).</summary>
    public class PoolTests
    {
        private const int Iron = 111, Thread = 222, Wood = 333;

        private static int Total(PoolDrops d, int hash)
        {
            int sum = 0;
            for (int i = 0; i < d.N; i++) if (d.Hash[i] == hash) sum += d.Count[i];
            return sum;
        }

        /// <summary>Applies the hits one after the other (k worms each, the hit that empties the mass is the death); adds what comes out to <paramref name="got"/>.</summary>
        private static void Kill(MassPool pool, int n, int[] hits, int[] hash, int[] got)
        {
            var drops = new PoolDrops();
            foreach (int k in hits)
            {
                drops.Clear();
                if (k >= n) pool.HarvestAll(drops);
                else { pool.Share(k, n); pool.Harvest(drops); }
                n -= k;
                for (int e = 0; e < hash.Length; e++) got[e] += Total(drops, hash[e]);
            }
        }

        [Fact]
        public void FortyWormsAndAFractionalRemainder()
        {
            var pool = new MassPool();
            pool.Add(Iron, 4.37f);
            var drops = new PoolDrops();
            int total = 0, firstDrop = 0;
            for (int worm = 1; worm <= 39; worm++)
            {
                drops.Clear();
                pool.Share(1, 41 - worm);
                pool.Harvest(drops);
                int got = Total(drops, Iron);
                if (got > 0 && firstDrop == 0) firstDrop = worm;
                total += got;
            }
            Assert.Equal(10, firstDrop);          // 4.37 / 40 = 0.109 per worm: nothing before the 10th
            drops.Clear();
            pool.HarvestAll(drops);               // the last worm
            total += Total(drops, Iron);
            Assert.Equal(4, total);               // floor(4.37), the 0.37 is lost
            Assert.True(pool.IsEmpty);
        }

        [Theory]
        [InlineData(40, 1)]
        [InlineData(40, 7)]
        [InlineData(200, 3)]
        [InlineData(13, 5)]
        public void ChunkedHitsGiveTheSameTotalAsOneBlow(int n, int seed)
        {
            var rng = new System.Random(seed);
            int[] hash = { Iron, Thread, Wood };
            float[] amounts = { 4.37f + seed, 8.74f * seed, 0.5f + n / 7f };

            var whole = new MassPool();
            for (int e = 0; e < 3; e++) whole.Add(hash[e], amounts[e]);
            var expected = new int[3];
            Kill(whole, n, new[] { n }, hash, expected);

            var single = new MassPool();
            for (int e = 0; e < 3; e++) single.Add(hash[e], amounts[e]);
            var hitsOne = new int[n];
            for (int i = 0; i < n; i++) hitsOne[i] = 1;
            var gotOne = new int[3];
            Kill(single, n, hitsOne, hash, gotOne);

            var random = new MassPool();
            for (int e = 0; e < 3; e++) random.Add(hash[e], amounts[e]);
            var hits = new List<int>();
            for (int left = n; left > 0;) { int k = Math.Min(left, 1 + rng.Next(9)); hits.Add(k); left -= k; }
            var gotRandom = new int[3];
            Kill(random, n, hits.ToArray(), hash, gotRandom);

            for (int e = 0; e < 3; e++)
            {
                Assert.Equal((int)Math.Floor(amounts[e] + 1e-3f), expected[e]);
                Assert.Equal(expected[e], gotOne[e]);
                Assert.Equal(expected[e], gotRandom[e]);
            }
            Assert.True(single.IsEmpty && random.IsEmpty);   // no positive remainder at the end
        }

        [Fact]
        public void SurplusReturnedToTheCarryComesOutLater()
        {
            var pool = new MassPool();
            pool.Add(Iron, 10f);
            var drops = new PoolDrops();
            pool.Share(5, 10);
            pool.Harvest(drops);
            Assert.Equal(5, Total(drops, Iron));
            pool.Return(Iron, 3);                   // the event limit kept 3 back
            drops.Clear();
            pool.Share(5, 5);
            pool.Harvest(drops);
            Assert.Equal(8, Total(drops, Iron));    // 3 returned + the 5 left
        }

        [Fact]
        public void PartialMergeMovesTheProrataAndConserves()
        {
            var big = new MassPool();
            var small = new MassPool();
            small.Add(Iron, 10f);
            small.Add(Thread, 3f);
            small.Share(1, 4);                       // 2.5 iron and 0.75 thread in the carry
            var moved = new MassPool();
            small.MoveFraction(moved, 5, 20);        // a quarter goes
            big.Merge(moved);
            Assert.Equal(2.5f, big.Total(Iron), 3);
            Assert.Equal(7.5f, small.Total(Iron), 3);
            Assert.Equal(0.75f, big.Total(Thread), 3);
            Assert.Equal(13f, big.TotalAll() + small.TotalAll(), 3);
            Assert.Equal(0.625f, big.CarryAt(0), 3); // the carry travels too: 2.5 x 1/4
        }

        [Fact]
        public void MovingEverythingEmptiesTheSource()
        {
            var a = new MassPool();
            a.Add(Iron, 2f);
            var b = new MassPool();
            a.MoveFraction(b, 5, 5);
            Assert.True(a.IsEmpty);
            Assert.Equal(2f, b.Total(Iron), 3);
        }

        [Fact]
        public void DissolutionDividesEquallyAndKeepsTheFractions()
        {
            var pool = new MassPool();
            pool.Add(Iron, 7f);
            pool.Share(1, 7);
            var part = new MassPool();
            pool.DivideInto(4, part);
            var sum = new MassPool();
            for (int i = 0; i < 4; i++) sum.Merge(part);
            Assert.Equal(7f, sum.Total(Iron), 3);
            Assert.Equal(pool.CarryAt(0), sum.CarryAt(0), 3);
            Assert.Equal(7f, pool.Total(Iron), 3);   // the source is unchanged
        }

        [Fact]
        public void FullTableRefusesAndKeepsWhatItHas()
        {
            var pool = new MassPool();
            for (int i = 0; i < MassPool.MaxEntries; i++) Assert.True(pool.Add(1000 + i, 1f));
            Assert.False(pool.Add(5, 1f));
            Assert.True(pool.Add(1000, 1f));          // an existing resource still grows
            Assert.Equal(2f, pool.Total(1000), 3);
            Assert.False(pool.Add(1000, -1f));
            Assert.False(pool.Add(1000, float.NaN));
        }

        [Fact]
        public void CappedAddKeepsTheRefusedShareInTheDonor()
        {
            var donor = new MassPool();
            donor.Add(Iron, 9f);
            donor.Add(Thread, 4f);
            var big = new MassPool();
            var part = new MassPool();
            donor.MoveFraction(part, 2, 3);          // 2 of the 3 offered worms were accepted
            big.Merge(part);
            Assert.Equal(13f, big.TotalAll() + donor.TotalAll(), 3);   // what was refused stays in the donor (to be dropped), nothing vanishes
            Assert.Equal(3f, donor.Total(Iron), 3);
        }

        [Fact]
        public void DissolutionWithFailedWormsConservesTheTotal()
        {
            var pool = new MassPool();
            pool.Add(Iron, 10f);
            pool.Add(Thread, 5f);
            var share = new MassPool();
            pool.DivideInto(5, share);
            var worms = new MassPool();
            var reserve = new MassPool();
            for (int i = 0; i < 5; i++)
            {
                if (i == 1 || i == 3) reserve.Merge(share);   // two worms could not be created
                else worms.Merge(share);
            }
            Assert.Equal(15f, worms.TotalAll() + reserve.TotalAll(), 3);
        }

        [Fact]
        public void FormationAddsTheWormPoolsTogether()
        {
            var formed = new MassPool();
            for (int i = 0; i < 6; i++)
            {
                var w = new MassPool();
                w.Add(Iron, 0.5f * i);
                if (i % 2 == 0) w.Share(1, 2);
                formed.Merge(w);
            }
            Assert.Equal(7.5f, formed.Total(Iron), 3);
        }

        // ------------------------------------------------------------------ serialization

        [Fact]
        public void SerializationRoundTrips()
        {
            var pool = new MassPool();
            pool.Add(Iron, 4.37f);
            pool.Add(Thread, 8.74f);
            pool.Share(3, 10);
            byte[] data = pool.ToBytes();
            Assert.Equal(MassPool.FormatVersion, data[0]);
            var back = new MassPool();
            Assert.True(back.ReadBytes(data));
            Assert.Equal(pool.Count, back.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                Assert.Equal(pool.HashAt(i), back.HashAt(i));
                Assert.Equal(pool.QtyAt(i), back.QtyAt(i));
                Assert.Equal(pool.CarryAt(i), back.CarryAt(i));
            }
        }

        [Fact]
        public void EmptyAndMalformedPoolsAreSafe()
        {
            var pool = new MassPool();
            pool.Add(Iron, 1f);
            Assert.True(pool.ReadBytes(null));
            Assert.True(pool.IsEmpty);
            Assert.True(pool.ReadBytes(new byte[0]));

            pool.Add(Iron, 1f);
            Assert.False(pool.ReadBytes(new byte[] { 99, 0 }));                     // unknown version
            Assert.True(pool.IsEmpty);
            Assert.False(pool.ReadBytes(new byte[] { MassPool.FormatVersion, 17 })); // too many entries
            Assert.False(pool.ReadBytes(new byte[] { MassPool.FormatVersion, 2, 1, 0, 0, 0 })); // truncated
            Assert.True(pool.IsEmpty);

            var pkg = new ZPackage();
            pkg.Write(MassPool.FormatVersion);
            pkg.Write((byte)1);
            pkg.Write(Iron);
            pkg.Write(float.NaN);
            pkg.Write(0f);
            Assert.False(pool.ReadBytes(pkg.GetArray()));
            Assert.True(pool.IsEmpty);
        }

        // ------------------------------------------------------------------ vanilla cost (plan 6, A1)

        [Theory]
        [InlineData(10, 3, false)]
        [InlineData(20, 10, false)]
        [InlineData(8, 4, true)]
        [InlineData(2, 0, false)]
        public void StepAmountIsExactlyRequirementGetAmount(int amount, int perLevel, bool upgrader)
        {
            var req = new Piece.Requirement { m_amount = amount, m_amountPerLevel = perLevel, m_upgraderResource = upgrader };
            for (int level = 1; level <= 8; level++)
                Assert.Equal(req.GetAmount(level), ItemCost.StepAmount(amount, perLevel, upgrader, level));
        }

        [Fact]
        public void PaddedCuirassCostsAcrossQualities()
        {
            // ArmorPaddedCuirass: Iron 10 (+3), LinenThread 20 (+10); GetAmount is the cost of ONE step (q2 = 1 x, q3 = 2 x, q4 = 4 x the per-level amount).
            Assert.Equal(10, ItemCost.Cumulative(10, 3, false, 1, true));
            Assert.Equal(13, ItemCost.Cumulative(10, 3, false, 2, true));
            Assert.Equal(19, ItemCost.Cumulative(10, 3, false, 3, true));
            Assert.Equal(31, ItemCost.Cumulative(10, 3, false, 4, true));
            Assert.Equal(20, ItemCost.Cumulative(20, 10, false, 1, true));
            Assert.Equal(30, ItemCost.Cumulative(20, 10, false, 2, true));
            Assert.Equal(50, ItemCost.Cumulative(20, 10, false, 3, true));
            Assert.Equal(90, ItemCost.Cumulative(20, 10, false, 4, true));
            Assert.Equal(10, ItemCost.Cumulative(10, 3, false, 4, false));   // without the upgrade materials only the crafting cost counts
            // the worked example of spec 7.2: 437 of 1000 durability points on a quality 1 cuirass
            Assert.Equal(4.37f, 437f / 1000f * ItemCost.Cumulative(10, 3, false, 1, true), 3);
            Assert.Equal(8.74f, 437f / 1000f * ItemCost.Cumulative(20, 10, false, 1, true), 3);
        }

        [Fact]
        public void IronSwordCostsAcrossQualities()
        {
            // SwordIron: Wood 2 (+1), Iron 20 (+10), LeatherScraps 3 (+2).
            Assert.Equal(2 + 1 + 2 + 4, ItemCost.Cumulative(2, 1, false, 4, true));
            Assert.Equal(20 + 10 + 20 + 40, ItemCost.Cumulative(20, 10, false, 4, true));
            Assert.Equal(3 + 2 + 4 + 8, ItemCost.Cumulative(3, 2, false, 4, true));
            Assert.Equal(20 + 10, ItemCost.Cumulative(20, 10, false, 2, true));
        }

        // ------------------------------------------------------------------ pool table and store

        [Fact]
        public void PoolTableHandsOutAndReclaimsSlots()
        {
            var table = new PoolTable(3);
            table.Acquire();
            int b = table.Acquire();
            table.Acquire();
            Assert.Equal(-1, table.Acquire());
            table.Get(b).Add(Iron, 1f);
            table.Release(b);
            Assert.Equal(2, table.InUse);
            int again = table.Acquire();
            Assert.Equal(b, again);
            Assert.True(table.Get(again).IsEmpty);
            Assert.Equal(3, table.InUse);
        }

        [Fact]
        public void FreeingAWormReleasesItsPool()
        {
            var store = new WormStore(4);
            int i = store.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f);
            Assert.Equal(-1, store.poolIndex[i]);
            store.poolIndex[i] = store.Pools.Acquire();
            store.Pools.Get(store.poolIndex[i]).Add(Iron, 2f);
            Assert.Equal(1, store.Pools.InUse);
            store.Free(i);
            Assert.Equal(0, store.Pools.InUse);
            int j = store.Alloc(Vector3.zero, 0f, WormState.Idle, 10f, 0f);
            Assert.Equal(-1, store.poolIndex[j]);
        }

        // ------------------------------------------------------------------ MW_Consumed

        private static ZPackage Packet(params (int hash, float qty)[] entries)
        {
            var pkg = new ZPackage();
            pkg.Write(entries.Length);
            foreach (var e in entries) { pkg.Write(e.hash); pkg.Write(e.qty); }
            return pkg;
        }

        [Fact]
        public void ConsumedPacketIsValidated()
        {
            var into = new ConsumedList();
            Func<int, bool> known = h => h == Iron || h == Thread;

            Assert.Equal(0, ConsumedList.TryRead(Packet((Iron, 4.37f), (Thread, 50f)), known, into));
            Assert.Equal(2, into.Count);

            // unknown prefab, too much, zero, negative, NaN: ignored, the good entry is kept
            int ignored = ConsumedList.TryRead(Packet((999, 1f), (Iron, 50.01f), (Iron, 0f), (Iron, -2f), (Iron, float.NaN), (Thread, 2f)), known, into);
            Assert.Equal(5, ignored);
            Assert.Equal(1, into.Count);
            Assert.Equal(Thread, into.Hash[0]);

            // more than 16 entries: the first 16 are read, the rest ignored
            var many = new (int, float)[20];
            for (int i = 0; i < many.Length; i++) many[i] = (i % 2 == 0 ? Iron : Thread, 1f);
            ignored = ConsumedList.TryRead(Packet(many), known, into);
            Assert.Equal(4, ignored);
            Assert.Equal(2, into.Count);
            Assert.Equal(8f, into.Qty[0], 3);

            // a negative count and a truncated package
            var bad = new ZPackage();
            bad.Write(-5);
            Assert.True(ConsumedList.TryRead(bad, known, into) > 0);
            Assert.Equal(0, into.Count);
            var cut = new ZPackage();
            cut.Write(3);
            cut.Write(Iron);
            Assert.True(ConsumedList.TryRead(cut, known, into) > 0);
        }

        [Fact]
        public void BigQuantitiesAreSplitAcrossEntriesAndPackets()
        {
            var list = new ConsumedList();
            list.Add(Iron, 120f);
            for (int i = 0; i < 20; i++) list.Add(5000 + i, 1f);
            var received = new ConsumedList();
            float sum = 0f;
            int entry = 0, packets = 0;
            float done = 0f;
            Func<int, bool> known = h => true;
            while (true)
            {
                var pkg = new ZPackage();
                if (!list.WriteNextPacket(pkg, ref entry, ref done)) break;
                packets++;
                Assert.Equal(0, ConsumedList.TryRead(pkg, known, received));
                Assert.True(received.Count <= ConsumedList.MaxEntriesPerPacket);
                sum += received.Total;
            }
            Assert.Equal(list.Total, sum, 2);
            Assert.True(packets >= 2);
        }
    }
}
