using System.Collections.Generic;
using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The stable identity of a colony (decision 211) and the choice of the colony of a mine that stays.</summary>
    public class MineKeyTests
    {
        [Fact]
        public void TheKeyOfAMineIsTheSameEveryTimeAndNeverZero()
        {
            long a = MineKey.FromPosition(3136f, 5044.5f, 5154f);
            Assert.Equal(a, MineKey.FromPosition(3136f, 5044.5f, 5154f));
            Assert.True(a > 0);
            Assert.NotEqual(0L, MineKey.FromPosition(0f, 0f, 0f));
        }

        [Fact]
        public void TheKeyIsThePureFunctionOfThePositionOfTheDungeon()
        {
            // Fixed value: a change of the hash would split the colonies of the worlds already saved, so it must be a deliberate decision (and a migration).
            Assert.Equal(MineKey.FromPosition(2816f, 5044f, -5470f), MineKey.FromPosition(2816.0f, 5044.0f, -5470.0f));
            var seen = new HashSet<long>();
            float[] xs = { 3136f, 2816f, -4672f, -5248f, 5888f };
            float[] zs = { 5154f, -5470f, -3870f, -3422f, -1758f };
            for (int i = 0; i < xs.Length; i++)
                Assert.True(seen.Add(MineKey.FromPosition(xs[i], 5044.5f, zs[i])));
            Assert.NotEqual(MineKey.FromPosition(3136f, 5044.5f, 5154f), MineKey.FromPosition(3136f, 5044.5f, 5154.5f));
            Assert.NotEqual(MineKey.FromPosition(3136f, 5044.5f, 5154f), MineKey.FromPosition(5154f, 5044.5f, 3136f));
        }

        [Fact]
        public void TheFloatNoiseOfTwoReadsOfOnePositionDoesNotSplitAMine()
        {
            Assert.Equal(MineKey.FromPosition(3136f, 5044.5f, 5154f), MineKey.FromPosition(3136.0001f, 5044.4999f, 5154.00005f));
        }

        [Fact]
        public void ARandomKeyIsPositiveAndDifferentEachTime()
        {
            long a = MineKey.NewRandom(), b = MineKey.NewRandom();
            Assert.True(a > 0 && b > 0);
            Assert.NotEqual(a, b);
            Assert.Equal("none", MineKey.Describe(0));
        }

        private static KeeperCandidate C(bool seeded, int worms, long created, long user = 1, uint id = 1) =>
            new KeeperCandidate { Seeded = seeded, Worms = worms, CreatedTicks = created, User = user, Id = id };

        [Fact]
        public void TheSeededColonyStaysWhateverItsAgeOrItsWorms()
        {
            Assert.True(MineOwnership.CompareKeeper(C(true, 0, 900), C(false, 99, 1)) < 0);
            Assert.True(MineOwnership.CompareKeeper(C(false, 99, 1), C(true, 0, 900)) > 0);
        }

        [Fact]
        public void ThenTheColonyWithMoreWormsThenTheOlderOne()
        {
            Assert.True(MineOwnership.CompareKeeper(C(true, 90, 500), C(true, 31, 100)) < 0);
            Assert.True(MineOwnership.CompareKeeper(C(true, 31, 100), C(true, 31, 500)) < 0);
            // An unknown creation time (0, a colony saved before the key existed) is the youngest.
            Assert.True(MineOwnership.CompareKeeper(C(true, 31, 500), C(true, 31, 0)) < 0);
            Assert.True(MineOwnership.CompareKeeper(C(true, 31, 0), C(true, 31, 500)) > 0);
        }

        [Fact]
        public void OnlyAnExactTieFallsBackOnTheZdoIdAndTheChoiceDoesNotDependOnTheOrder()
        {
            Assert.True(MineOwnership.CompareKeeper(C(true, 5, 100, 1, 10), C(true, 5, 100, 1, 20)) < 0);
            Assert.Equal(0, MineOwnership.CompareKeeper(C(true, 5, 100, 1, 10), C(true, 5, 100, 1, 10)));

            KeeperCandidate[] ab = { C(false, 87, 300, 1, 90409), C(true, 113, 100, 1, 90249), C(true, 113, 200, 1, 12) };
            KeeperCandidate[] ba = { ab[2], ab[0], ab[1] };
            Assert.Equal(ab[1].Id, ab[MineOwnership.PickKeeper(ab, 3)].Id);
            Assert.Equal(ab[1].Id, ba[MineOwnership.PickKeeper(ba, 3)].Id);
            Assert.Equal(-1, MineOwnership.PickKeeper(ab, 0));
        }

        [Fact]
        public void TheWormCountOfASnapshotIsReadFromItsHeader()
        {
            Assert.Equal(0, SnapshotFormat.PeekCount(null));
            Assert.Equal(0, SnapshotFormat.PeekCount(new byte[] { SnapshotFormat.Version }));
            Assert.Equal(0, SnapshotFormat.PeekCount(new byte[] { 99, 5, 0 }));
            Assert.Equal(261, SnapshotFormat.PeekCount(new byte[] { SnapshotFormat.Version, 5, 1 }));
        }
    }
}
