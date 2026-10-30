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

        // ---- worms that never had a floor (decision 274)

        [Fact]
        public void RestoredWormWithoutFloorSurvivesBeyondTheGraceInsideTheWindow()
        {
            float since = WormGround.NoFailure, waitEnd = 100f + WormGround.FirstFloorWait;
            for (float t = 100f; t < waitEnd; t += 0.25f)
            {
                Assert.True(WormGround.AwaitingFirstFloor(false, t, waitEnd));
                Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, t, ref since, WormGround.FailGrace, false, waitEnd));
            }
            Assert.Equal(WormGround.NoFailure, since);
        }

        [Fact]
        public void WormThatGetsItsFloorInsideTheWindowPlaysNormallyAgain()
        {
            float waitEnd = 100f + WormGround.FirstFloorWait;
            Assert.True(WormGround.AwaitingFirstFloor(false, 110f, waitEnd));
            // First successful probe: the flag is set, the window no longer matters.
            Assert.False(WormGround.AwaitingFirstFloor(true, 110.25f, waitEnd));
            // A later loss of the floor follows decision 273.
            float since = WormGround.NoFailure;
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, 111f, ref since, WormGround.FailGrace, true, waitEnd));
            Assert.Equal(GroundVerdict.Kill, WormGround.OnProbeFailed(true, false, 111f + WormGround.FailGrace, ref since, WormGround.FailGrace, true, waitEnd));
        }

        [Fact]
        public void WormStillWithoutFloorAfterTheWindowFollowsTheGraceRule()
        {
            float waitEnd = 100f + WormGround.FirstFloorWait;
            float since = WormGround.NoFailure;
            Assert.False(WormGround.AwaitingFirstFloor(false, waitEnd, waitEnd));
            Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, false, waitEnd, ref since, WormGround.FailGrace, false, waitEnd));
            Assert.Equal(waitEnd, since);
            Assert.Equal(GroundVerdict.Kill, WormGround.OnProbeFailed(true, false, waitEnd + WormGround.FailGrace, ref since, WormGround.FailGrace, false, waitEnd));
            // A sleeper keeps its place for good.
            since = WormGround.NoFailure;
            for (float t = waitEnd; t < waitEnd + 60f; t += 0.25f)
                Assert.Equal(GroundVerdict.Keep, WormGround.OnProbeFailed(true, true, t, ref since, WormGround.FailGrace, false, waitEnd));
        }

        [Fact]
        public void TheWindowOutlastsTheRestoreWaitAndTheDungeonLoad()
        {
            Assert.True(WormGround.FirstFloorWait > 20f);   // RestoreGroundWait
        }

        // ---- fallback of the sleepers and sleepers after the window (decision 275)

        [Fact]
        public void DepositUsesTheRingFirstThenTheLongProbeThenKeepsTheWormUnsupported()
        {
            Assert.Equal(WormGround.DepositSpot.Ring, WormGround.ChooseDepositSpot(true, true));
            Assert.Equal(WormGround.DepositSpot.Ring, WormGround.ChooseDepositSpot(true, false));
            Assert.Equal(WormGround.DepositSpot.LongProbe, WormGround.ChooseDepositSpot(false, true));
            Assert.Equal(WormGround.DepositSpot.Unsupported, WormGround.ChooseDepositSpot(false, false));
        }

        [Fact]
        public void SleeperWaitsWhileTheZoneIsNotLoadedOrTheWindowIsOpen()
        {
            float waitEnd = 100f + WormGround.FirstFloorWait;
            Assert.Equal(WormGround.SleeperAction.Wait, WormGround.OnSleeperNoFloor(false, true, 500f, waitEnd, true));
            Assert.Equal(WormGround.SleeperAction.Wait, WormGround.OnSleeperNoFloor(true, false, 110f, waitEnd, true));
            // A sleeper that had a floor is not in a window, whatever the time.
            Assert.Equal(WormGround.SleeperAction.Fall, WormGround.OnSleeperNoFloor(true, true, 110f, waitEnd, true));
        }

        [Fact]
        public void SleeperAfterTheWindowFallsToAFloorOrIsKeptNeverKilled()
        {
            float waitEnd = 100f + WormGround.FirstFloorWait;
            Assert.Equal(WormGround.SleeperAction.Fall, WormGround.OnSleeperNoFloor(true, false, waitEnd, waitEnd, true));
            for (float t = waitEnd; t < waitEnd + 120f; t += 0.25f)
                Assert.Equal(WormGround.SleeperAction.KeepTraced, WormGround.OnSleeperNoFloor(true, false, t, waitEnd, false));
        }
    }
}
