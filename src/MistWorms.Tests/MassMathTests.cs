using MistWorms.Render;
using Xunit;

namespace MistWorms.Tests
{
    public class MassMathTests
    {
        // Defaults of Mass.BaseRadius / Mass.RadiusExponent (spec §4.5).
        private const float R10 = 0.83f;
        private const float E = 0.36f;

        [Fact]
        public void TenWormsGiveBaseRadius()
        {
            Assert.Equal(0.83f, MassMath.Radius(10, R10, E), 3);
        }

        [Theory]
        [InlineData(3, 0.54f)]
        [InlineData(20, 1.07f)]
        [InlineData(50, 1.48f)]
        [InlineData(100, 1.90f)]
        [InlineData(200, 2.44f)]
        public void MatchesSpecTable(int n, float expected)
        {
            Assert.Equal(expected, MassMath.Radius(n, R10, E), 2);
        }

        [Fact]
        public void RadiusGrowsWithN()
        {
            Assert.True(MassMath.Radius(11, R10, E) > MassMath.Radius(10, R10, E));
        }

        [Fact]
        public void NonPositiveCountIsTreatedAsOne()
        {
            Assert.Equal(MassMath.Radius(1, R10, E), MassMath.Radius(0, R10, E));
        }

        [Fact]
        public void DomeScaleIsFlattenedByEightTenths()
        {
            var s = MassMath.DomeScale(2.0f);
            Assert.Equal(2.0f, s.x);
            Assert.Equal(1.6f, s.y, 5);
            Assert.Equal(2.0f, s.z);
        }

        [Fact]
        public void HitSphereIsNineTenthsOfTheRadiusAtFourTenthsOfItsHeight()
        {
            Assert.Equal(1.8f, MassMath.HitRadius(2.0f), 5);
            Assert.Equal(0.8f, MassMath.HitCenterHeight(2.0f), 5);
        }

        [Fact]
        public void RadiusSmoothingReachesNinetyFivePercentInThreeTenthsOfASecond()
        {
            float shown = 1f;
            for (int i = 0; i < 30; i++) shown = MassMath.SmoothRadius(shown, 2f, 0.01f);   // 0.3 s at 100 Hz
            Assert.InRange(shown, 1.94f, 1.96f);
        }

        [Fact]
        public void RadiusSmoothingNeverOvershootsNorJumpsWithBigFrames()
        {
            Assert.True(MassMath.SmoothRadius(1f, 2f, 10f) <= 2f);
            Assert.Equal(1f, MassMath.SmoothRadius(1f, 2f, 0f));
            Assert.True(MassMath.SmoothRadius(2f, 1f, 0.016f) < 2f);
        }

        [Theory]
        [InlineData(1, 1f)]
        [InlineData(10, 1f)]
        [InlineData(40, 0.25f)]
        [InlineData(100, 0.1f)]
        [InlineData(200, 0.1f)]
        public void KnockbackFactorIsTenOverNClamped(int n, float expected)
        {
            Assert.Equal(expected, MassMath.KnockbackFactor(n, 0.1f), 5);
        }

        [Fact]
        public void KnockbackFactorFollowsTheConfiguredMinimum()
        {
            Assert.Equal(0.05f, MassMath.KnockbackFactor(200, 0.05f), 5);
            Assert.Equal(0.3f, MassMath.KnockbackFactor(200, 0.3f), 5);
            Assert.Equal(1f, MassMath.KnockbackFactor(0, 0.1f), 5);
        }

        [Theory]
        [InlineData(0, 12, 12f, true, 0f, 0)]
        [InlineData(1, 12, 12f, true, 0f, 12 % 12)]
        [InlineData(1, 12, 12f, true, 0.25f, 3)]
        [InlineData(1, 12, 12f, true, -1f / 12f, 11)]
        [InlineData(1, 6, 10f, false, 5f, 5)]
        [InlineData(1, 6, 10f, false, 0.05f, 0)]
        public void FrameSelection(int _, int frames, float fps, bool loop, float time, int expected)
        {
            Assert.Equal(expected, Flipbook.FrameAt(frames, fps, loop, time, 0f));
        }

        [Fact]
        public void PhaseShiftsLoopingFrame()
        {
            Assert.Equal(5, Flipbook.FrameAt(12, 12f, true, 0f, 5f));
        }
    }
}
