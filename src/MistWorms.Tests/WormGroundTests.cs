using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Fate of a worm whose floor probe failed (decision 273, spec 11.1).</summary>
    public class WormGroundTests
    {
        [Fact]
        public void IsolatedFailureKeepsTheWormAndSuccessResetsNothingElse()
        {
            float since = WormGround.NoFailure;
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, 10f, ref since));
            Assert.Equal(10f, since);
        }

        [Fact]
        public void SleepingWormNeverDies()
        {
            float since = WormGround.NoFailure;
            for (float t = 0f; t < 60f; t += 0.25f)
                Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, true, t, ref since));
            Assert.Equal(WormGround.NoFailure, since);
        }

        [Fact]
        public void UnloadedZoneFreezesAndRestartsTheDelay()
        {
            float since = 5f;   // had failed before the zone unloaded
            for (float t = 6f; t < 60f; t += 0.25f)
                Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(false, false, t, ref since));
            Assert.Equal(WormGround.NoFailure, since);
            // The zone is back: the delay starts now, not at the old failure.
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, 60f, ref since));
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, 60f + WormGround.FailGrace - 0.1f, ref since));
        }

        [Fact]
        public void ContinuousFailureBeyondTheDelayKills()
        {
            float since = WormGround.NoFailure;
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, 100f, ref since));
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, 100f + WormGround.FailGrace - 0.01f, ref since));
            Assert.Equal(GroundVerdict.Kill, WormGround.OnProbeFailed(true, false, 100f + WormGround.FailGrace, ref since));
        }

        [Fact]
        public void FallingAsleepDuringTheDelayCancelsIt()
        {
            float since = WormGround.NoFailure;
            WormGround.OnProbeFailed(true, false, 0f, ref since);
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, true, 10f, ref since));
            Assert.Equal(WormGround.NoFailure, since);
        }
    }
}
