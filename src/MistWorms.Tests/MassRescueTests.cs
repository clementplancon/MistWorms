using MistWorms.Mass;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The fall safety net of a mass (spec 4.6, decision 102): rescued only when it left the world, never for a fall to a real floor.</summary>
    public class MassRescueTests
    {
        [Fact]
        public void AFloorSixMetresLowerIsNoReasonToRescue()
        {
            // A mass that jumped off a ledge: floor below, above the terrain, falling at 8 m/s.
            Assert.False(MassRescue.Needed(true, true, true, 40f, 34f, -8f));
            Assert.False(MassRescue.Needed(true, false, false, 3010f, 0f, -8f));     // inside a mine, a floor 6 m lower
        }

        [Fact]
        public void NoFloorMeansRescue()
        {
            Assert.True(MassRescue.Needed(false, true, true, 40f, 34f, -2f));
            Assert.True(MassRescue.Needed(false, false, false, 3010f, 0f, 0f));
        }

        [Fact]
        public void UnderTheTerrainOutdoorsMeansRescueEvenWithSomethingBelow()
        {
            Assert.True(MassRescue.Needed(true, true, true, 36.9f, 40f, 0f));        // 3.1 m under the heightmap
            Assert.False(MassRescue.Needed(true, true, true, 39.5f, 40f, 0f));       // inside the margin
            Assert.False(MassRescue.Needed(true, false, false, 3010f, 3020f, 0f));   // indoors there is no heightmap to compare
        }

        [Fact]
        public void FallingFasterThanThirtyMetresPerSecondMeansRescue()
        {
            Assert.True(MassRescue.Needed(true, true, true, 40f, 30f, -30.5f));
            Assert.False(MassRescue.Needed(true, true, true, 40f, 30f, -29f));
        }
    }
}
