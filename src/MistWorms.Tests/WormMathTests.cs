using System;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    public class WormMathTests
    {
        private const float Pi = (float)Math.PI;

        [Theory]
        [InlineData(0f, 0f)]
        [InlineData(1.5f * Pi, -Pi / 2f)]
        [InlineData(-3 * Pi + 0.001f, -Pi + 0.001f)]
        [InlineData(2 * Pi + 0.5f, 0.5f)]
        public void WrapPiKeepsAnglesInRange(float input, float expected)
        {
            Assert.Equal(expected, WormMath.WrapPi(input), 3);
        }

        [Theory]
        [InlineData(0f, 1f, 0f)]            // +z
        [InlineData(1f, 0f, Pi / 2f)]       // +x
        [InlineData(-1f, 0f, -Pi / 2f)]
        public void HeadingMatchesTheForwardConvention(float dx, float dz, float expected)
        {
            float h = WormMath.HeadingOf(dx, dz);
            Assert.Equal(expected, h, 3);
            // forward = (sin h, cos h) must point back along (dx, dz)
            Assert.Equal(dx, (float)Math.Sin(h), 3);
            Assert.Equal(dz, (float)Math.Cos(h), 3);
        }

        [Fact]
        public void TurnTowardLimitsTheStep()
        {
            Assert.Equal(0.1f, WormMath.TurnToward(0f, 1f, 0.1f), 5);
            Assert.Equal(-0.1f, WormMath.TurnToward(0f, -1f, 0.1f), 5);
            Assert.Equal(1f, WormMath.TurnToward(0.95f, 1f, 0.1f), 5); // within reach: lands on the target
        }

        [Fact]
        public void TurnTowardTakesTheShortWayAcrossTheSeam()
        {
            // From +170 degrees to -170 degrees is a 20 degree turn through 180, not 340 the other way.
            float from = 170f * Mathf.Deg2Rad;
            float to = -170f * Mathf.Deg2Rad;
            float step = 5f * Mathf.Deg2Rad;
            float h = WormMath.TurnToward(from, to, step);
            Assert.Equal(175f * Mathf.Deg2Rad, h, 4);
            h = WormMath.TurnToward(h, to, step);
            h = WormMath.TurnToward(h, to, step);
            h = WormMath.TurnToward(h, to, step);
            Assert.Equal(to, h, 4);
        }

        [Fact]
        public void TurnTowardConvergesWithin360DegreesPerSecond()
        {
            // Worm.TurnSpeed 360 deg/s at 20 Hz: a half turn takes 10 steps.
            float step = 360f * Mathf.Deg2Rad / 20f;
            float h = 0f;
            int steps = 0;
            while (Math.Abs(WormMath.WrapPi(Pi - h)) > 1e-4f && steps < 100)
            {
                h = WormMath.TurnToward(h, Pi, step);
                steps++;
            }
            Assert.Equal(10, steps);
        }

        [Fact]
        public void InsideBoxChecksAllThreeAxes()
        {
            var c = new Vector3(0f, 5000f, 0f);
            var half = new Vector3(10f, 4f, 20f);
            Assert.True(WormMath.InsideBox(new Vector3(9f, 5003f, -19f), c, half));
            Assert.False(WormMath.InsideBox(new Vector3(11f, 5000f, 0f), c, half));
            Assert.False(WormMath.InsideBox(new Vector3(0f, 5005f, 0f), c, half));
            Assert.False(WormMath.InsideBox(new Vector3(0f, 5000f, -21f), c, half));
        }

        [Fact]
        public void ObbExpansionAccountsForRotation()
        {
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            // A 20 x 4 x 8 room turned 90 degrees about y spans 8 in x and 20 in z.
            WormMath.ExpandByObb(new Vector3(100f, 5000f, 50f), new Quaternion(0f, Mathf.Sqrt(0.5f), 0f, Mathf.Sqrt(0.5f)), new Vector3(10f, 2f, 4f), ref min, ref max); // 90 degrees about y (Quaternion.Euler is a native call)
            Assert.Equal(96f, min.x, 3);
            Assert.Equal(104f, max.x, 3);
            Assert.Equal(40f, min.z, 3);
            Assert.Equal(60f, max.z, 3);
            Assert.Equal(4998f, min.y, 3);
            Assert.Equal(5002f, max.y, 3);

            // A second room extends the union.
            WormMath.ExpandByObb(new Vector3(130f, 5000f, 50f), new Quaternion(0f, 0f, 0f, 1f), new Vector3(10f, 2f, 4f), ref min, ref max);
            Assert.Equal(96f, min.x, 3);
            Assert.Equal(140f, max.x, 3);
        }

        [Theory]
        [InlineData(10f, 15f, false, 20f, true)]    // outdoors, loud enough
        [InlineData(10f, 15f, false, 5f, false)]    // quieter than the distance
        [InlineData(14f, 15f, false, 99f, true)]
        [InlineData(16f, 15f, false, 99f, false)]   // beyond hear range
        [InlineData(13f, 15f, true, 99f, false)]    // interior caps hearing at 12 m (BaseAI.CanHearTarget)
        [InlineData(11f, 15f, true, 99f, true)]
        [InlineData(11f, 8f, true, 99f, false)]     // the cap only lowers the range
        public void HearingFollowsBaseAI(float distance, float range, bool interior, float noise, bool expected)
        {
            Assert.Equal(expected, WormMath.CanHear(distance, range, interior, noise));
        }

        [Fact]
        public void ViewAngleIsAFullAngle()
        {
            // Facing +z (heading 0).
            Assert.True(WormMath.InViewAngle(0f, 0f, 5f, 90f));        // straight ahead
            Assert.True(WormMath.InViewAngle(0f, 3f, 5f, 90f));        // 31 degrees, inside the 45 degree half angle
            Assert.False(WormMath.InViewAngle(0f, 5f, 3f, 90f));       // 59 degrees
            Assert.False(WormMath.InViewAngle(0f, 0f, -5f, 90f));      // behind
            Assert.True(WormMath.InViewAngle(0f, 0f, -5f, 360f));      // 360 = all around
            Assert.True(WormMath.InViewAngle(Pi, 0f, -5f, 90f));       // turned around, the target is ahead
        }

        [Fact]
        public void SimRateByDetailLevel()
        {
            Assert.Equal(2f, WormMath.SimRate(true, 0f, 20f, 5f));
            Assert.Equal(20f, WormMath.SimRate(false, 29f * 29f, 20f, 5f));
            Assert.Equal(5f, WormMath.SimRate(false, 31f * 31f, 20f, 5f));
            Assert.Equal(5f, WormMath.SimRate(false, float.MaxValue, 20f, 5f));
        }
    }
}
