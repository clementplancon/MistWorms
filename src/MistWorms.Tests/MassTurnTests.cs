using MistWorms.Mass;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Decision 269: the turn of a mass (vanilla UpdateRotation slowdown) and the step assist gate.</summary>
    public class MassTurnTests
    {
        // Quaternion.Euler is a native call, not available in the test host.
        private static Vector3 Yaw(float degrees) => new Vector3((float)System.Math.Sin(degrees * System.Math.PI / 180.0), 0f, (float)System.Math.Cos(degrees * System.Math.PI / 180.0));

        [Fact]
        public void TheVanillaSeekerBroodTakesAboutOneAndAHalfSecondsToTurnBack()
        {
            Assert.Equal(1.4f, MassTurn.SecondsToTurn(150f, 180f, 10f), 2);   // 0.6 s above 90 degrees + 0.8 s down to 10 degrees (measured in game: about 1.0 s to move at 0.5 m/s)
        }

        [Fact]
        public void TheMassTurnSpeedTurnsBackInHalfASecond()
        {
            Assert.Equal(0.5f, MassTurn.SecondsToTurn(MassPrefab.MassTurnSpeed, 180f, 10f), 2);
        }

        [Fact]
        public void TheTurnRateFallsWithTheSquareRootOfTheRemainingAngle()
        {
            Assert.Equal(300f, MassTurn.Rate(300f, 180f), 3);
            Assert.Equal(300f, MassTurn.Rate(300f, 90f), 3);
            Assert.Equal(150f, MassTurn.Rate(300f, 22.5f), 3);
        }

        [Fact]
        public void NoTimeIsNeededWhenAlreadyFacing()
        {
            Assert.Equal(0f, MassTurn.SecondsToTurn(420f, 5f, 10f));
        }

        [Fact]
        public void AWindowWithoutProgressWhileTurningIsNotAStall()
        {
            Vector3 fwd = Vector3.forward;
            Assert.False(MassTurn.CountsAsStall(Vector3.back, fwd, 10f));
            Assert.False(MassTurn.CountsAsStall(Yaw(133f), fwd, 10f));
            Assert.False(MassTurn.CountsAsStall(Yaw(10.5f), fwd, 10f));
        }

        [Fact]
        public void AWindowWithoutProgressWhileFacingTheGoalIsAStall()
        {
            Vector3 fwd = Vector3.forward;
            Assert.True(MassTurn.CountsAsStall(Yaw(5f), fwd, 10f));
            Assert.True(MassTurn.CountsAsStall(new Vector3(0f, 3f, 4f), fwd, 10f));          // the height of the goal does not count
            Assert.True(MassTurn.CountsAsStall(Vector3.zero, fwd, 10f));                       // no horizontal direction: nothing to turn toward
        }
    }
}
