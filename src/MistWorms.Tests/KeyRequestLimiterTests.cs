using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>One keyframe request per peer and per second (phase 8, decision 225).</summary>
    public class KeyRequestLimiterTests
    {
        [Fact]
        public void AFirstRequestIsServedAndASecondWithinASecondIsNot()
        {
            var l = new KeyRequestLimiter();
            Assert.True(l.Allow(7L, 10f));
            Assert.False(l.Allow(7L, 10.5f));
            Assert.False(l.Allow(7L, 10.99f));
            Assert.True(l.Allow(7L, 11f));
        }

        [Fact]
        public void PeersAreLimitedIndependently()
        {
            var l = new KeyRequestLimiter();
            Assert.True(l.Allow(1L, 5f));
            Assert.True(l.Allow(2L, 5f));
            Assert.False(l.Allow(1L, 5.1f));
            Assert.False(l.Allow(2L, 5.1f));
        }

        [Fact]
        public void ATableFullOfPeersStillServesANewOneAndForgetsTheOldest()
        {
            var l = new KeyRequestLimiter();
            for (int i = 0; i < 40; i++) Assert.True(l.Allow(1000 + i, i * 0.01f));
            Assert.True(l.Allow(5000L, 1f));
            Assert.False(l.Allow(5000L, 1.1f));
        }

        [Fact]
        public void ClearForgetsEveryone()
        {
            var l = new KeyRequestLimiter();
            l.Allow(1L, 0f);
            l.Clear();
            Assert.True(l.Allow(1L, 0.1f));
        }
    }
}
