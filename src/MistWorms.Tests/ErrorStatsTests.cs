using System.Collections.Generic;
using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The mean and percentile S17 asserts on (phase 8-2).</summary>
    public class ErrorStatsTests
    {
        [Fact]
        public void EmptyListGivesZero()
        {
            Assert.Equal(0.0, ErrorStats.Mean(new List<float>()));
            Assert.Equal(0f, ErrorStats.Percentile(new List<float>(), 95));
            Assert.Equal(0f, ErrorStats.Max(new List<float>()));
            Assert.Equal(0.0, ErrorStats.Mean(null));
        }

        [Fact]
        public void MeanIsTheArithmeticMean()
        {
            Assert.Equal(2.0, ErrorStats.Mean(new List<float> { 1f, 2f, 3f }), 6);
        }

        [Fact]
        public void P95OfAHundredIsTheNinetyFifthSmallest()
        {
            var v = new List<float>();
            for (int i = 100; i >= 1; i--) v.Add(i);   // unsorted on purpose
            Assert.Equal(95f, ErrorStats.Percentile(v, 95));
            Assert.Equal(100f, ErrorStats.Percentile(v, 100));
            Assert.Equal(1f, ErrorStats.Percentile(v, 1));
            Assert.Equal(100f, v[0]);   // the input is left alone
        }

        [Fact]
        public void PercentileOfOneSampleIsThatSample()
        {
            Assert.Equal(0.7f, ErrorStats.Percentile(new List<float> { 0.7f }, 95));
        }

        [Fact]
        public void AFewOutliersMoveP95NotTheBulk()
        {
            var v = new List<float>();
            for (int i = 0; i < 97; i++) v.Add(0.2f);
            v.Add(5f); v.Add(6f); v.Add(7f);
            Assert.Equal(0.2f, ErrorStats.Percentile(v, 95));
            Assert.Equal(7f, ErrorStats.Max(v));
        }
    }
}
