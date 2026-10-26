using System;
using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The nestCounts of a mass (spec 8.2) and the ownership rules of the colony of a mine (spec 8.3).</summary>
    public class NestCountsTests
    {
        private static NestCounts Make(int a, int b, int c, int none)
        {
            var n = new NestCounts();
            n.Add(0, a); n.Add(1, b); n.Add(2, c); n.Add(NestCounts.None, none);
            return n;
        }

        private static int Sum(NestCounts c)
        {
            int s = 0;
            for (int i = 0; i <= NestCounts.None; i++) s += c[i];
            return s;
        }

        [Fact]
        public void TotalIsTheSumOfTheSlots()
        {
            NestCounts c = Make(5, 3, 0, 7);
            Assert.Equal(15, c.Total);
            Assert.Equal(15, Sum(c));
            Assert.Equal(7, c[NestCounts.None]);
            Assert.Equal(7, c[-1]);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(14)]
        [InlineData(15)]
        [InlineData(40)]
        public void ProrataTakesExactlyKAndKeepsTheSumEqualToN(int k)
        {
            NestCounts c = Make(5, 3, 0, 7);
            var removed = new NestCounts();
            int took = c.TakeProrata(k, removed);
            Assert.Equal(Math.Min(k, 15), took);
            Assert.Equal(took, removed.Total);
            Assert.Equal(15 - took, c.Total);
            Assert.Equal(c.Total, Sum(c));
            Assert.Equal(removed.Total, Sum(removed));
            for (int i = 0; i <= NestCounts.None; i++) Assert.True(c[i] >= 0);
        }

        [Fact]
        public void ProrataIsProportionalAndRoundsWithTheLargestRemainders()
        {
            var c = new NestCounts();
            c.Add(0, 10); c.Add(1, 20); c.Add(2, 30);       // 60 worms
            var share = new NestCounts();
            c.CopyProrata(6, share);
            Assert.Equal(1, share[0]); Assert.Equal(2, share[1]); Assert.Equal(3, share[2]);
            c.CopyProrata(4, share);                          // 0.67 / 1.33 / 2 -> the largest remainder gets the extra unit
            Assert.Equal(4, share.Total);
            Assert.Equal(2, share[2]);
            Assert.Equal(60, c.Total);                        // CopyProrata does not change the source
        }

        [Fact]
        public void ProrataNeverTakesMoreThanASlotHolds()
        {
            NestCounts c = Make(1, 1, 1, 1);
            for (int k = 1; k <= 4; k++)
            {
                var share = new NestCounts();
                c.CopyProrata(k, share);
                Assert.Equal(k, share.Total);
                for (int i = 0; i <= NestCounts.None; i++) Assert.True(share[i] <= c[i]);
            }
        }

        [Fact]
        public void AbsorptionMergeAndLossKeepTheSumEqualToN()
        {
            // a mass of 12: 5 from nest 0, 4 from nest 1, 3 with no nest
            var big = new NestCounts();
            big.Add(0, 5); big.Add(1, 4); big.Add(NestCounts.None, 3);
            int n = 12;

            big.Add(2, 1); n += 1;                              // a worm of nest 2 climbs on
            Assert.Equal(n, big.Total);

            var small = new NestCounts();
            small.Add(1, 3); small.Add(2, 2);                   // a mass of 5 merges in whole
            big.AddAll(small); n += 5;
            Assert.Equal(n, big.Total);

            var moved = new NestCounts();                       // a partial merge hands 7 worms over
            big.TakeProrata(7, moved); n -= 7;
            Assert.Equal(7, moved.Total);
            Assert.Equal(n, big.Total);

            big.TakeProrata(4, null); n -= 4;                   // four worms die
            Assert.Equal(n, big.Total);
            Assert.Equal(n, Sum(big));
        }

        [Fact]
        public void FitToAddsMissingWormsWithoutNestAndCutsTheSurplus()
        {
            NestCounts c = Make(2, 2, 0, 0);
            c.FitTo(7);
            Assert.Equal(7, c.Total);
            Assert.Equal(3, c[NestCounts.None]);
            c.FitTo(3);
            Assert.Equal(3, c.Total);
            Assert.Equal(3, Sum(c));
            c.FitTo(0);
            Assert.Equal(0, c.Total);
        }

        [Fact]
        public void SubtractNeverGoesBelowZero()
        {
            NestCounts c = Make(3, 2, 0, 0);
            NestCounts d = Make(5, 1, 0, 0);
            c.Subtract(d);
            Assert.Equal(0, c[0]);
            Assert.Equal(1, c[1]);
            Assert.Equal(1, c.Total);
        }

        [Fact]
        public void TakeOneGivesTheNestsBackInOrderThenNothing()
        {
            var c = new NestCounts();
            c.Add(3, 2); c.Add(NestCounts.None, 1);
            Assert.Equal(3, c.TakeOne());
            Assert.Equal(3, c.TakeOne());
            Assert.Equal(NestCounts.None, c.TakeOne());
            Assert.Equal(-1, c.TakeOne());
            Assert.Equal(0, c.Total);
        }

        [Fact]
        public void BytesRoundTrip()
        {
            NestCounts c = Make(5, 3, 0, 7);
            var back = new NestCounts();
            Assert.True(back.ReadBytes(c.ToBytes()));
            Assert.Equal(c.Total, back.Total);
            for (int i = 0; i <= NestCounts.None; i++) Assert.Equal(c[i], back[i]);
            Assert.True(back.ReadBytes(null));
            Assert.Equal(0, back.Total);
        }

        [Fact]
        public void BadBytesGiveAnEmptySet()
        {
            var c = new NestCounts();
            Assert.False(c.ReadBytes(new byte[] { 9, 0 }));                 // unknown version
            Assert.False(c.ReadBytes(new byte[] { 1, 2, 0, 5, 0 }));        // truncated
            Assert.False(c.ReadBytes(new byte[] { 1, 1, 200, 5, 0 }));      // slot out of range
            Assert.Equal(0, c.Total);
        }

        // ---------------------------------------------------------------- ownership of the colony of a mine

        [Fact]
        public void WithoutAColonyThePlayerInsideCreatesIt()
        {
            Assert.Equal(MineStep.Create, MineOwnership.Decide(false, false, false, false));
        }

        [Fact]
        public void TheOwnerOfAnUnseededColonySeedsAndAnOwnerOfASeededOneDoesNothing()
        {
            Assert.Equal(MineStep.Seed, MineOwnership.Decide(true, true, false, false));
            Assert.Equal(MineStep.Nothing, MineOwnership.Decide(true, true, false, true));
        }

        [Fact]
        public void AnOwnerWithNobodyInTheMineLosesTheColonyToThePlayerInside()
        {
            Assert.Equal(MineStep.Claim, MineOwnership.Decide(true, false, false, false));
            Assert.Equal(MineStep.Claim, MineOwnership.Decide(true, false, false, true));
            Assert.Equal(MineStep.Nothing, MineOwnership.Decide(true, false, true, false));
        }

        [Fact]
        public void TheSmallestZdoIdStaysAndTheSeededFlagIsInherited()
        {
            long[] users = { 7, 3, 3 };
            uint[] ids = { 1, 9, 4 };
            Assert.Equal(2, MineOwnership.Keep(users, ids, 3));
            Assert.Equal(-1, MineOwnership.Keep(users, ids, 0));
            Assert.True(MineOwnership.InheritSeeded(false, true));
            Assert.True(MineOwnership.InheritSeeded(true, false));
            Assert.False(MineOwnership.InheritSeeded(false, false));
            Assert.True(MineOwnership.Compare(3, 4, 7, 1) < 0);
            Assert.Equal(0, MineOwnership.Compare(3, 4, 3, 4));
        }
    }
}
