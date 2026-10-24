using System;
using System.Collections.Generic;
using MistWorms.Drops;
using Xunit;

namespace MistWorms.Tests
{
    public class NativeDropsTests
    {
        private static readonly BaitTable Table = new BaitTable(new[] { "FishingBait", "FishingBaitMistlands" }, new[] { 40f, 25f });

        [Fact]
        public void ProbabilitiesMatchTheSpecOver100000Draws()
        {
            var rng = new Random(12345);
            const int n = 100000;
            int bait = 0, entrails = 0, bloodbag = 0;
            for (int i = 0; i < n; i++)
            {
                NativeDropRoll r = NativeDrops.Roll(rng, 0.08f, 0.08f, 0.04f, Table);
                if (r.Bait != null) bait++;
                if (r.Entrails) entrails++;
                if (r.Bloodbag) bloodbag++;
            }
            // +-0.5 point, about 6 standard deviations at 8 %.
            Assert.InRange(bait / (double)n, 0.075, 0.085);
            Assert.InRange(entrails / (double)n, 0.075, 0.085);
            Assert.InRange(bloodbag / (double)n, 0.035, 0.045);
        }

        [Fact]
        public void TheThreeRollsAreIndependent()
        {
            var rng = new Random(99);
            const int n = 200000;
            int both = 0;
            for (int i = 0; i < n; i++)
            {
                NativeDropRoll r = NativeDrops.Roll(rng, 0.08f, 0.08f, 0.04f, Table);
                if (r.Bait != null && r.Entrails) both++;
            }
            // 0.08 * 0.08 = 0.64 %
            Assert.InRange(both / (double)n, 0.0050, 0.0078);
        }

        [Fact]
        public void ChanceExtremesAreExact()
        {
            var rng = new Random(1);
            for (int i = 0; i < 1000; i++)
            {
                NativeDropRoll none = NativeDrops.Roll(rng, 0f, 0f, 0f, Table);
                Assert.Null(none.Bait);
                Assert.False(none.BaitRolled || none.Entrails || none.Bloodbag);
                NativeDropRoll all = NativeDrops.Roll(rng, 1f, 1f, 1f, Table);
                Assert.NotNull(all.Bait);
                Assert.True(all.Entrails && all.Bloodbag);
            }
        }

        [Fact]
        public void EmptyTableRollsBaitButDropsNone()
        {
            NativeDropRoll r = NativeDrops.Roll(new Random(1), 1f, 0f, 0f, new BaitTable(new string[0], new float[0]));
            Assert.True(r.BaitRolled);
            Assert.Null(r.Bait);
        }

        [Fact]
        public void TheRollSequenceDoesNotDependOnTheBaitTable()
        {
            // The scenario seed gives the same categories whatever the config holds.
            var a = new Random(77);
            var b = new Random(77);
            var other = new BaitTable(new[] { "X" }, new[] { 1f });
            for (int i = 0; i < 500; i++)
            {
                NativeDropRoll ra = NativeDrops.Roll(a, 0.08f, 0.08f, 0.04f, Table);
                NativeDropRoll rb = NativeDrops.Roll(b, 0.08f, 0.08f, 0.04f, other);
                Assert.Equal(ra.BaitRolled, rb.BaitRolled);
                Assert.Equal(ra.Entrails, rb.Entrails);
                Assert.Equal(ra.Bloodbag, rb.Bloodbag);
            }
        }

        [Fact]
        public void ScenarioSeedPasses200RepetitionsWithinThreePoints()
        {
            // S01 part B (decisions.md 50): 200 kills with the fixed seed, every category within +-3 points of its probability.
            var rng = new Random(NativeDrops.ScenarioSeed);
            int bait = 0, entrails = 0, bloodbag = 0;
            for (int i = 0; i < 200; i++)
            {
                NativeDropRoll r = NativeDrops.Roll(rng, 0.08f, 0.08f, 0.04f, Table);
                if (r.BaitRolled) bait++;
                if (r.Entrails) entrails++;
                if (r.Bloodbag) bloodbag++;
            }
            Assert.Equal(17, bait);       // pinned: the S01 scenario reads the same numbers in game
            Assert.InRange(bait / 200.0, 0.05, 0.11);
            Assert.Equal(16, entrails);
            Assert.InRange(entrails / 200.0, 0.05, 0.11);
            Assert.Equal(8, bloodbag);
            Assert.InRange(bloodbag / 200.0, 0.01, 0.07);
        }
    }
}
