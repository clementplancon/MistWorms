using System;
using System.Collections.Generic;
using System.Linq;
using MistWorms.Bites;
using Xunit;

namespace MistWorms.Tests
{
    public class BiteMathTests
    {
        private static BiteItem Item(int id, float durability, bool wisplight = false) =>
            new BiteItem { Id = id, Durability = durability, IsWisplight = wisplight };

        private static BiteResult Run(IList<BiteItem> items, float p, int worms, int seed, out List<BiteLoss> losses, bool wisplightLast = true, int itemsPerTen = 1)
        {
            losses = new List<BiteLoss>();
            return BiteMath.Select(items, p, worms, itemsPerTen, wisplightLast, new Random(seed), losses);
        }

        [Theory]
        [InlineData(1, 1, 10, 1)]
        [InlineData(10, 1, 10, 1)]
        [InlineData(11, 1, 10, 2)]
        [InlineData(40, 1, 10, 4)]
        [InlineData(40, 1, 3, 3)]      // clamped to the eligible items
        [InlineData(10, 2, 10, 2)]     // ItemsPerTenWorms = 2
        [InlineData(0, 1, 5, 1)]       // never below one piece
        [InlineData(5, 2, 10, 1)]      // ceil(2 x 5 / 10) = 1 (plan 5, A6)
        [InlineData(11, 2, 10, 3)]     // ceil(22 / 10) = 3
        [InlineData(40, 2, 10, 8)]
        public void PiecesPerBiteFollowsTheSpecFormula(int worms, int itemsPerTen, int eligible, int expected)
        {
            Assert.Equal(expected, BiteMath.PiecesPerBite(worms, itemsPerTen, eligible));
        }

        [Fact]
        public void NoEligibleItemLeavesEverythingRemaining()
        {
            BiteResult r = Run(new List<BiteItem>(), 2f, 1, 1, out List<BiteLoss> losses);
            Assert.False(r.HadEligible);
            Assert.Equal(2f, r.Remaining);
            Assert.Empty(losses);

            // Items without durability left do not count either.
            r = Run(new[] { Item(0, 0f), Item(1, 0f) }, 2f, 1, 1, out losses);
            Assert.False(r.HadEligible);
            Assert.Empty(losses);
        }

        [Fact]
        public void SingleBiteTakesExactlyPFromOneItem()
        {
            BiteResult r = Run(new[] { Item(7, 100f), Item(8, 100f), Item(9, 100f) }, 2f, 1, 3, out List<BiteLoss> losses);
            Assert.True(r.HadEligible);
            Assert.Single(losses);
            Assert.Equal(2f, losses[0].Amount, 3);
            Assert.Equal(2f, r.Eaten, 3);
            Assert.Equal(0f, r.Remaining, 3);
        }

        [Fact]
        public void PieceChoiceIsUniform()
        {
            var items = new[] { Item(0, 1000f), Item(1, 1000f), Item(2, 1000f), Item(3, 1000f), Item(4, 1000f) };
            var counts = new int[5];
            var rng = new Random(42);
            var losses = new List<BiteLoss>();
            const int trials = 20000;
            for (int t = 0; t < trials; t++)
            {
                BiteMath.Select(items, 1f, 1, 1, true, rng, losses);
                counts[losses[0].Id]++;
            }
            // Expected 20 % each, standard deviation 0.28 points: +-1.5 points is a very wide band.
            foreach (int c in counts) Assert.InRange(c / (double)trials, 0.185, 0.215);
        }

        [Fact]
        public void BigMassBitesSeveralDistinctPieces()
        {
            var items = Enumerable.Range(0, 10).Select(i => Item(i, 100f)).ToList();
            BiteResult r = Run(items, 8f, 40, 5, out List<BiteLoss> losses);   // k = 4
            Assert.Equal(4, losses.Count);
            Assert.Equal(4, losses.Select(l => l.Id).Distinct().Count());
            Assert.All(losses, l => Assert.Equal(2f, l.Amount, 3));
            Assert.Equal(8f, r.Eaten, 3);
        }

        [Fact]
        public void WisplightIsLast()
        {
            var items = new[] { Item(0, 50f), Item(1, 100f, wisplight: true) };
            for (int seed = 0; seed < 50; seed++)
            {
                BiteResult r = Run(items, 5f, 1, seed, out List<BiteLoss> losses);
                Assert.True(r.HadEligible);
                Assert.Equal(0, losses.Single().Id);
            }

            // Alone, the Wisplight becomes eligible.
            BiteResult alone = Run(new[] { Item(1, 100f, wisplight: true) }, 5f, 1, 1, out List<BiteLoss> lonely);
            Assert.True(alone.HadEligible);
            Assert.Equal(1, lonely.Single().Id);

            // With WisplightLast off it is an ordinary item.
            bool seen = false;
            for (int seed = 0; seed < 50 && !seen; seed++)
            {
                Run(items, 5f, 1, seed, out List<BiteLoss> free, wisplightLast: false);
                seen = free.Any(l => l.Id == 1);
            }
            Assert.True(seen);
        }

        [Fact]
        public void WisplightTakesWhatTheOtherItemsCannotAbsorbInTheSameTick()
        {
            var items = new[] { Item(0, 3f), Item(1, 100f, wisplight: true) };
            BiteResult r = Run(items, 10f, 1, 4, out List<BiteLoss> losses);
            Assert.Equal(10f, r.Eaten, 3);
            Assert.Equal(0f, r.Remaining, 3);
            Assert.Equal(3f, losses.Single(l => l.Id == 0).Amount, 3);
            Assert.Equal(7f, losses.Single(l => l.Id == 1).Amount, 3);
        }

        [Fact]
        public void RemainderGoesToHealthOnceWisplightIsSpentToo()
        {
            // 40 worms x 1 point: the rest of P (what nothing could eat) is what the health phase prorates on.
            var items = new[] { Item(0, 10f), Item(1, 5f, wisplight: true) };
            BiteResult r = Run(items, 40f, 40, 2, out List<BiteLoss> losses);
            Assert.True(r.HadEligible);
            Assert.Equal(15f, r.Eaten, 3);
            Assert.Equal(25f, r.Remaining, 3);
        }

        [Fact]
        public void ExcessIsCarriedToOtherItems()
        {
            // One tiny item among big ones: whichever is drawn first, all of P is eaten.
            var items = new[] { Item(0, 1f), Item(1, 100f), Item(2, 100f) };
            for (int seed = 0; seed < 100; seed++)
            {
                BiteResult r = Run(items, 10f, 1, seed, out List<BiteLoss> losses);
                Assert.Equal(10f, r.Eaten, 3);
                Assert.Equal(0f, r.Remaining, 3);
                Assert.All(losses, l => Assert.True(l.Amount <= items[l.Id].Durability + 1e-4f));
            }
        }

        [Fact]
        public void ExcessBeyondEveryItemRemains()
        {
            var items = new[] { Item(0, 3f), Item(1, 4f) };
            BiteResult r = Run(items, 100f, 1, 9, out List<BiteLoss> losses);
            Assert.True(r.HadEligible);
            Assert.Equal(7f, r.Eaten, 3);
            Assert.Equal(93f, r.Remaining, 3);
            Assert.Equal(2, losses.Count);
        }

        [Fact]
        public void NothingIsEatenForZeroP()
        {
            BiteResult r = Run(new[] { Item(0, 10f) }, 0f, 1, 1, out List<BiteLoss> losses);
            Assert.True(r.HadEligible);
            Assert.Empty(losses);
            Assert.Equal(0f, r.Eaten);
        }
    }
}
