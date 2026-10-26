using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The bytes per second counters of a colony (phase 8, plan A9): a 10 second sliding window.</summary>
    public class RateWindowTests
    {
        [Fact]
        public void NothingAddedIsZero()
        {
            var w = new RateWindow();
            Assert.Equal(0.0, w.PerSecond(5.0));
            Assert.Equal(0L, w.Total);
        }

        [Fact]
        public void ASteadyFlowGivesItsRateOnceTheWindowIsFull()
        {
            var w = new RateWindow(10);
            for (int s = 0; s < 30; s++)
                for (int k = 0; k < 5; k++) w.Add(200, s + k * 0.2);   // 1 000 per second
            Assert.Equal(1000.0, w.PerSecond(29.99), 0);
            Assert.Equal(30 * 1000L, w.Total);
        }

        [Fact]
        public void TheFirstSecondsAreNotDilutedByTheWholeWindow()
        {
            var w = new RateWindow(10);
            w.Add(3000, 100.0);
            w.Add(3000, 102.0);
            // 6 000 bytes over the 2 s that have elapsed, not over 10 s.
            Assert.Equal(3000.0, w.PerSecond(102.0), 0);
        }

        [Fact]
        public void OldTrafficLeavesTheWindow()
        {
            var w = new RateWindow(10);
            w.Add(10000, 0.0);
            Assert.True(w.PerSecond(5.0) > 0.0);
            Assert.Equal(0.0, w.PerSecond(25.0), 3);          // 25 s later nothing is left
            Assert.Equal(10000L, w.Total);                       // the total keeps everything
            w.Add(500, 25.5);
            Assert.True(w.PerSecond(25.5) > 0.0);
        }

        [Fact]
        public void ABurstSpreadsOverTheWindow()
        {
            var w = new RateWindow(10);
            for (int s = 0; s < 20; s++) w.Add(0, s);
            w.Add(10000, 20.0);
            Assert.Equal(1000.0, w.PerSecond(20.0), 0);          // 10 000 bytes over a full window of 10 s
        }
    }
}
