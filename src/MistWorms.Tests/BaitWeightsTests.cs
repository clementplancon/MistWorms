using System.Collections.Generic;
using MistWorms.Drops;
using Xunit;

namespace MistWorms.Tests
{
    public class BaitWeightsTests
    {
        [Theory]
        [InlineData("FishingBait", 0, 40f)]
        [InlineData("FishingBaitMistlands", 1, 25f)]   // the fixed weights win over the formula
        [InlineData("FishingBaitForest", 0, 10f)]
        [InlineData("FishingBaitForest", 1, 5f)]
        [InlineData("FishingBaitAshlands", 2, 3f)]     // 10 / 3 = 3.33 -> 3
        [InlineData("FishingBaitDeepNorth", 3, 3f)]    // 10 / 4 = 2.5 rounds away from zero to 3
        [InlineData("FishingBaitX", 9, 2f)]            // 10 / 10 = 1 -> raised to the minimum 2
        public void WeightOfFollowsTheSpec(string prefab, int trophies, float expected)
        {
            float w = BaitWeights.WeightOf(prefab, trophies, "FishingBait", "FishingBaitMistlands");
            Assert.Equal(expected, w);
        }

        [Fact]
        public void ComputeListsEveryBaitSortedByName()
        {
            var baits = new List<BaitCandidate>
            {
                new BaitCandidate("FishingBaitSwamp", 1),
                new BaitCandidate("FishingBait", 0),
                new BaitCandidate("FishingBaitMistlands", 1),
                new BaitCandidate("FishingBaitAshlands", 1),
            };
            string text = BaitWeights.Compute(baits, "FishingBait", "FishingBaitMistlands");
            Assert.Equal("FishingBait:40,FishingBaitAshlands:5,FishingBaitMistlands:25,FishingBaitSwamp:5", text);
        }

        [Fact]
        public void ParseReadsEntriesAndReportsProblems()
        {
            var problems = new List<string>();
            List<KeyValuePair<string, float>> list = BaitWeights.Parse(" A:40 , B:2.5,C,D:0,E:-1,F:x,,A:10 ", problems);
            Assert.Equal(2, list.Count);
            Assert.Equal("A", list[0].Key);
            Assert.Equal(10f, list[0].Value);   // a duplicate keeps the last weight
            Assert.Equal("B", list[1].Key);
            Assert.Equal(2.5f, list[1].Value);
            Assert.Equal(4, problems.Count);    // C, D, E, F
        }

        [Fact]
        public void ParseOfEmptyTextIsEmpty()
        {
            Assert.Empty(BaitWeights.Parse(null, null));
            Assert.Empty(BaitWeights.Parse("   ", null));
        }

        [Fact]
        public void ComputeThenParseRoundTrips()
        {
            string text = BaitWeights.Compute(new[] { new BaitCandidate("FishingBait", 0), new BaitCandidate("FishingBaitCave", 2) },
                "FishingBait", "FishingBaitMistlands");
            List<KeyValuePair<string, float>> list = BaitWeights.Parse(text, null);
            Assert.Equal(2, list.Count);
            Assert.Equal(new KeyValuePair<string, float>("FishingBait", 40f), list[0]);
            Assert.Equal(new KeyValuePair<string, float>("FishingBaitCave", 3f), list[1]);
        }

        [Fact]
        public void BuildDropsUnknownPrefabsAndReportsThem()
        {
            List<KeyValuePair<string, float>> entries = BaitWeights.Parse("A:1,B:3", null);
            var unknown = new List<string>();
            BaitTable table = BaitWeights.Build(entries, name => name == "B", unknown);
            Assert.Equal(1, table.Count);
            Assert.Equal(new[] { "A" }, unknown);
            Assert.Equal("B", table.Pick(0.0));
            Assert.Equal("B", table.Pick(0.999));
        }

        [Fact]
        public void PickFollowsTheCumulativeWeights()
        {
            var table = new BaitTable(new[] { "A", "B" }, new[] { 1f, 3f });
            Assert.Equal("A", table.Pick(0.0));
            Assert.Equal("A", table.Pick(0.24));
            Assert.Equal("B", table.Pick(0.26));
            Assert.Equal("B", table.Pick(0.999));
            Assert.Null(new BaitTable(new string[0], new float[0]).Pick(0.5));
        }

        [Fact]
        public void PickIsProportionalToWeights()
        {
            var table = new BaitTable(new[] { "A", "B", "C" }, new[] { 40f, 25f, 5f });
            var rng = new System.Random(7);
            var counts = new Dictionary<string, int> { { "A", 0 }, { "B", 0 }, { "C", 0 } };
            const int n = 100000;
            for (int i = 0; i < n; i++) counts[table.Pick(rng.NextDouble())]++;
            Assert.InRange(counts["A"] / (double)n, 40 / 70.0 - 0.01, 40 / 70.0 + 0.01);
            Assert.InRange(counts["B"] / (double)n, 25 / 70.0 - 0.01, 25 / 70.0 + 0.01);
            Assert.InRange(counts["C"] / (double)n, 5 / 70.0 - 0.01, 5 / 70.0 + 0.01);
        }
    }
}
