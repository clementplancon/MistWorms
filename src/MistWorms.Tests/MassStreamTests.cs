using MistWorms.Mass;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Thresholds and geometry of the stream form (spec 4.7, plan 4B-2).</summary>
    public class MassStreamTests
    {
        [Fact]
        public void NarrowMeansUnderTwiceTheRadiusPlusAMargin()
        {
            Assert.True(MassStream.TooNarrow(2.0f, 2.44f));     // a 2 m corridor for a mass of 200
            Assert.False(MassStream.TooNarrow(5.4f, 2.44f));    // 2 x 2.44 + 0.5 = 5.38
            Assert.True(MassStream.TooNarrow(5.3f, 2.44f));
        }

        [Fact]
        public void LeavingTheStreamNeedsThePassageWideOverTheWholeFreeDistance()
        {
            // In the dome only the near width counts; in the stream the far width must be wide as well.
            Assert.True(MassStream.NextStream(false, 2f, 9f, 2.44f));
            Assert.False(MassStream.NextStream(false, 9f, 2f, 2.44f));
            Assert.True(MassStream.NextStream(true, 9f, 2f, 2.44f));
            Assert.False(MassStream.NextStream(true, 9f, 9f, 2.44f));
        }

        [Fact]
        public void RibbonIsLongestForBigMassesAndCapped()
        {
            Assert.Equal(6f, MassStream.RibbonLength(200, 0.08f, 6f, 1.9f), 3);   // 16 m wanted, 6 m allowed
            Assert.Equal(3.2f, MassStream.RibbonLength(40, 0.08f, 6f, 1.9f), 3);
            Assert.Equal(1.9f, MassStream.RibbonLength(5, 0.08f, 6f, 1.9f), 3);   // never shorter than it is wide
            Assert.Equal(1.9f, MassStream.RibbonWidth(2.0f), 3);
            Assert.Equal(MassStream.MinRibbonWidth, MassStream.RibbonWidth(0.3f), 3);
        }

        [Fact]
        public void ChainCoversTheRibbonWithAtMostSixSpheres()
        {
            float radius = MassStream.SphereRadius(1.9f);
            Assert.Equal(0.855f, radius, 3);
            int count = MassStream.ChainCount(6f, radius);
            Assert.InRange(count, 2, MassStream.MaxChainSpheres);
            Assert.Equal(1, MassStream.ChainCount(0.5f, radius));
            Assert.Equal(MassStream.MaxChainSpheres, MassStream.ChainCount(50f, radius));

            // Symmetric about the centre, first and last inside the ribbon.
            float first = MassStream.ChainOffset(0, count, 6f);
            float last = MassStream.ChainOffset(count - 1, count, 6f);
            Assert.Equal(-last, first, 4);
            Assert.True(last < 3f && last > 0f);
            Assert.Equal(0f, MassStream.ChainOffset(0, 1, 6f));
        }
    }
}
