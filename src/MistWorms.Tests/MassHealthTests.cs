using MistWorms.Mass;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The health model of a mass (spec §4.2, plan 4B-1): n and partial derived from the vanilla health, and the layout of mw_state.</summary>
    public class MassHealthTests
    {
        private const float H = 10f;

        [Theory]
        [InlineData(400f, 40, 0f)]
        [InlineData(345f, 35, 5f)]     // S03: 55 blunt on 40
        [InlineData(334f, 34, 6f)]     // S03: 22 pierce = 11
        [InlineData(274f, 28, 6f)]     // S03: 30 fire x2 = 60
        [InlineData(214f, 22, 6f)]     // S03: 6 x 10 blunt
        [InlineData(40f, 4, 0f)]       // a whole worm left exactly
        [InlineData(0.5f, 1, 9.5f)]
        public void CountAndPartialFollowTheHealth(float health, int n, float partial)
        {
            int count = MassHealth.CountFor(health, H);
            Assert.Equal(n, count);
            Assert.Equal(partial, MassHealth.PartialFor(count, health, H), 3);
        }

        [Fact]
        public void FloatNoiseOnAWholeWormDoesNotAddOne()
        {
            // 34 worms exactly, stored with a rounding error above the boundary.
            Assert.Equal(34, MassHealth.CountFor(340.0004f, H));
            Assert.Equal(34, MassHealth.CountFor(339.9999f, H));
            Assert.Equal(35, MassHealth.CountFor(340.5f, H));
        }

        [Fact]
        public void NoHealthMeansNoWorm()
        {
            Assert.Equal(0, MassHealth.CountFor(0f, H));
            Assert.Equal(0, MassHealth.CountFor(-3f, H));
            Assert.Equal(0, MassHealth.CountFor(10f, 0f));
        }

        [Fact]
        public void PartialStaysInsideOneWorm()
        {
            Assert.Equal(0f, MassHealth.PartialFor(5, 50.00001f, H));   // float noise never gives a negative partial
            Assert.Equal(0f, MassHealth.PartialFor(5, 40f, H));         // a stale count never gives a partial above one worm
        }

        [Fact]
        public void HealthIsRebuiltFromCountAndPartial()
        {
            Assert.Equal(345f, MassHealth.HealthFor(35, 5f, H));
            Assert.Equal(350f, MassHealth.MaxHealthFor(35, H));
            // The round trip of a hit: the new health gives back the count and the partial that were written.
            float health = MassHealth.HealthFor(22, 6f, H);
            Assert.Equal(22, MassHealth.CountFor(health, H));
            Assert.Equal(6f, MassHealth.PartialFor(22, health, H), 3);
        }

        [Fact]
        public void StateCodesKeepModeFlagsAndStreamApart()
        {
            byte code = MassStateCodes.WithMode(0, MassStateCodes.Hunt);
            code = MassStateCodes.With(code, MassStateCodes.Stream, true);
            code = MassStateCodes.With(code, MassStateCodes.Merging, true);
            Assert.Equal(MassStateCodes.Hunt, MassStateCodes.Mode(code));
            Assert.True(MassStateCodes.IsStream(code));
            Assert.True(MassStateCodes.IsMerging(code));
            Assert.False(MassStateCodes.IsDissolving(code));

            code = MassStateCodes.WithMode(code, MassStateCodes.Engulf);
            Assert.Equal(MassStateCodes.Engulf, MassStateCodes.Mode(code));
            Assert.True(MassStateCodes.IsStream(code));          // changing the mode keeps the shape bit and the transition

            code = MassStateCodes.With(code, MassStateCodes.Stream, false);
            Assert.False(MassStateCodes.IsStream(code));
            Assert.True(MassStateCodes.IsMerging(code));
        }

        [Fact]
        public void StateBitsDoNotOverlap()
        {
            Assert.Equal(0, MassStateCodes.ModeMask & MassStateCodes.Merging);
            Assert.Equal(0, MassStateCodes.ModeMask & MassStateCodes.Dissolving);
            Assert.Equal(0, MassStateCodes.Merging & MassStateCodes.Dissolving);
            Assert.Equal(0, MassStateCodes.Stream & (MassStateCodes.ModeMask | MassStateCodes.Merging | MassStateCodes.Dissolving));
            Assert.Equal("Dormant+Dissolving+Stream", MassStateCodes.Describe((byte)(MassStateCodes.Dormant | MassStateCodes.Dissolving | MassStateCodes.Stream)));
        }
    }
}
