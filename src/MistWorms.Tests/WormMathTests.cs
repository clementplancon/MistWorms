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

        [Fact]
        public void IdlePoseStartsAfterTheDelay()
        {
            Assert.False(WormMath.UseIdlePose(10.2f, 10f));    // 0.2 s still
            Assert.True(WormMath.UseIdlePose(10.31f, 10f));
            Assert.False(WormMath.UseIdlePose(10f, 10f));      // just moved
        }

        [Fact]
        public void VariationIsStableAndBounded()
        {
            int different = 0;
            for (int slot = 0; slot < 500; slot++)
            {
                WormMath.Variation(slot, 3, out float scale, out float roll);
                WormMath.Variation(slot, 3, out float scale2, out float roll2);
                Assert.Equal(scale, scale2);
                Assert.Equal(roll, roll2);
                Assert.InRange(scale, 0.9f, 1.1f);
                Assert.InRange(roll, -8f, 8f);
                WormMath.Variation(slot, 4, out float scale3, out _);
                if (scale3 != scale) different++;
            }
            Assert.True(different > 450, "a reused slot (new generation) must get a new look");
        }

        [Fact]
        public void VariationCoversTheWholeRange()
        {
            float minS = 9f, maxS = 0f, minR = 99f, maxR = -99f;
            for (int slot = 0; slot < 2000; slot++)
            {
                WormMath.Variation(slot, 1, out float s, out float r);
                minS = Math.Min(minS, s); maxS = Math.Max(maxS, s);
                minR = Math.Min(minR, r); maxR = Math.Max(maxR, r);
            }
            Assert.True(minS < 0.92f && maxS > 1.08f);
            Assert.True(minR < -6.5f && maxR > 6.5f);
        }

        // ---- group behaviour (plan 4A-3)

        [Fact]
        public void ApproachAngleStartsAtTheBaseAndAdvancesByTheGoldenAngle()
        {
            Assert.Equal(0.3f, WormMath.ApproachAngle(0, 0.3f), 4);
            float one = WormMath.WrapPi(WormMath.ApproachAngle(1, 0.3f) - 0.3f);
            Assert.Equal(137.5f * Mathf.Deg2Rad, one, 4);
            float two = WormMath.WrapPi(WormMath.ApproachAngle(2, 0.3f) - 0.3f);
            Assert.Equal(WormMath.WrapPi(275f * Mathf.Deg2Rad), two, 4);   // 275 degrees = -85
        }

        [Fact]
        public void ApproachAnglesOfThirtyRanksAreWellSpread()
        {
            var angles = new float[30];
            for (int r = 0; r < 30; r++) angles[r] = WormMath.ApproachAngle(r, 1.1f);
            float minGap = 99f;
            for (int a = 0; a < 30; a++)
                for (int b = a + 1; b < 30; b++)
                    minGap = Math.Min(minGap, Math.Abs(WormMath.WrapPi(angles[a] - angles[b])));
            Assert.True(minGap > 3f * Mathf.Deg2Rad, "no two of 30 places closer than 3 degrees, got " + minGap * Mathf.Rad2Deg);
            // Large ranks stay exact (no float drift of rank x 137.5).
            Assert.Equal(WormMath.ApproachAngle(1, 0f), WormMath.ApproachAngle(721, 0f), 3);   // 720 x 137.5 = 275 turns
        }

        [Fact]
        public void BodyMidIsBehindTheHead()
        {
            WormMath.BodyMid(1f, 2f, 0f, out float mx, out float mz);          // heading +z
            Assert.Equal(1f, mx, 4);
            Assert.Equal(2f - WormMath.BodyMidOffset, mz, 4);
            WormMath.BodyMid(0f, 0f, (float)Math.PI / 2f, out mx, out mz);     // heading +x
            Assert.Equal(-WormMath.BodyMidOffset, mx, 4);
            Assert.Equal(0f, mz, 4);
        }

        [Theory]
        [InlineData(0f, 0.5f, 0f, true)]        // straight ahead, 0.5 m, same heading
        [InlineData(0f, 0.7f, 0f, false)]       // too far
        [InlineData(0f, -0.4f, 0f, false)]      // behind
        [InlineData(0.3f, 0.4f, 0f, false)]     // atan(0.3 / 0.4) = 36.9 degrees: outside the 30 degree cone
        [InlineData(0.2f, 0.4f, 0f, true)]      // 26.6 degrees: inside
        public void IsAheadInFileCone(float dx, float dz, float otherHeading, bool expected)
        {
            Assert.Equal(expected, WormMath.IsAheadInFile(0f, dx, dz, otherHeading));
        }

        [Fact]
        public void IsAheadInFileNeedsTheSameDirection()
        {
            Assert.True(WormMath.IsAheadInFile(0f, 0.1f, 0.5f, 40f * Mathf.Deg2Rad));    // 11 degrees off, 40 degrees apart
            Assert.False(WormMath.IsAheadInFile(0f, 0.1f, 0.5f, 50f * Mathf.Deg2Rad));   // 50 degrees apart
            Assert.False(WormMath.IsAheadInFile(0f, 0f, 0.5f, (float)Math.PI));          // coming head on
            Assert.False(WormMath.IsAheadInFile(0f, 0f, 0f, 0f));                        // stacked: handled by the separation
        }

        [Fact]
        public void IsAheadInFileFollowsTheHeading()
        {
            float h = (float)Math.PI / 2f;   // facing +x
            Assert.True(WormMath.IsAheadInFile(h, 0.5f, 0f, h));
            Assert.False(WormMath.IsAheadInFile(h, 0f, 0.5f, h));
        }

        [Fact]
        public void FileSpeedCapIsTheLeaderPlusFivePercentWithAFloor()
        {
            Assert.Equal(2.1f, WormMath.FileSpeedCap(2f, 0.2f), 4);
            Assert.Equal(0.2f, WormMath.FileSpeedCap(0f, 0.2f), 4);
        }

        [Fact]
        public void RingsFillOneAfterTheOther()
        {
            const float inner = 1.08f;
            int c0 = WormMath.RingCapacity(0, inner), c1 = WormMath.RingCapacity(1, inner);
            Assert.Equal(19, c0);   // floor(2 pi 1.08 / 0.35)
            Assert.Equal(26, c1);   // floor(2 pi 1.48 / 0.35)
            Assert.Equal(0, WormMath.ApproachPlace(0, 0f, inner, out _, out float r0));
            Assert.Equal(1.08f, r0, 4);
            Assert.Equal(0, WormMath.ApproachPlace(c0 - 1, 0f, inner, out _, out _));
            Assert.Equal(1, WormMath.ApproachPlace(c0, 0f, inner, out _, out float r1));
            Assert.Equal(1.48f, r1, 4);
            Assert.Equal(2, WormMath.ApproachPlace(c0 + c1, 0f, inner, out _, out float r2));
            Assert.Equal(1.88f, r2, 4);
        }

        [Fact]
        public void ThirtyWormsKeepAtLeastTheRingSpacingApart()
        {
            const float inner = 1.08f;
            var pts = new Vector2[30];
            for (int rank = 0; rank < 30; rank++)
            {
                WormMath.ApproachPlace(rank, 0.7f, inner, out float a, out float r);
                pts[rank] = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
            }
            float min = 99f;
            for (int i = 0; i < 30; i++)
                for (int j = i + 1; j < 30; j++) min = Math.Min(min, Vector2.Distance(pts[i], pts[j]));
            Assert.True(min >= 0.3f, "closest two of 30 places: " + min);
        }

        [Fact]
        public void PartialRingIsSpreadAroundTheTarget()
        {
            // The first 8 places of ring 0 (golden order) leave no gap wider than 120 degrees.
            var angles = new System.Collections.Generic.List<float>();
            for (int rank = 0; rank < 8; rank++)
            {
                WormMath.ApproachPlace(rank, 0f, 1.08f, out float a, out _);
                angles.Add((a * Mathf.Rad2Deg + 360f) % 360f);
            }
            angles.Sort();
            float widest = 360f - angles[angles.Count - 1] + angles[0];
            for (int i = 1; i < angles.Count; i++) widest = Math.Max(widest, angles[i] - angles[i - 1]);
            Assert.True(widest < 120f, "widest gap " + widest);
        }

        [Fact]
        public void GoldenStepIsCoprime()
        {
            for (int cap = 3; cap < 80; cap++)
            {
                int step = WormMath.GoldenStep(cap);
                var seen = new bool[cap];
                for (int k = 0; k < cap; k++) seen[(int)((long)k * step % cap)] = true;
                Assert.DoesNotContain(false, seen);   // every place is reached exactly once
            }
        }
    }
}
