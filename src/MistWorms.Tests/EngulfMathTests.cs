using MistWorms.Mass;
using Xunit;

namespace MistWorms.Tests
{
    public class EngulfMathTests
    {
        [Theory]
        [InlineData(1.9f, 1.37f, 0.6f, true)]
        [InlineData(1.97f, 1.37f, 0.6f, true)]
        [InlineData(2.0f, 1.37f, 0.6f, false)]
        public void EngulfStartsWithinRadiusPlusRange(float flat, float r, float range, bool expected)
        {
            Assert.Equal(expected, EngulfMath.InEngulfRange(flat, r, range));
        }

        [Fact]
        public void TickIntervalHoldsItsRhythm()
        {
            var t = new BiteTicker();
            t.Start(10f, 0.5f);
            Assert.False(t.Due(10.4f));
            Assert.True(t.Due(10.5f));
            t.Advance(10.52f, 0.5f);                 // a little late: the rhythm is kept
            Assert.Equal(11.0f, t.Next, 3);
            t.Advance(11.0f, 0.5f);
            Assert.Equal(11.5f, t.Next, 3);
            t.Advance(15f, 0.5f);                    // a long pause restarts from now
            Assert.Equal(15.5f, t.Next, 3);
        }

        [Fact]
        public void CancelledTickDelaysTheNextOneByTheInterval()
        {
            var t = new BiteTicker();
            t.Start(0f, 0.5f);
            t.Advance(0.5f, 0.5f, cancelled: true, cancelDelay: 0.5f);
            Assert.Equal(1.5f, t.Next, 3);           // 1.0 s after the cancelled tick
            Assert.False(t.Due(1.4f));
            Assert.True(t.Due(1.5f));
            t.Postpone(0.5f);
            Assert.Equal(2.0f, t.Next, 3);
            t.Stop();
            Assert.False(t.Due(100f));
        }

        [Fact]
        public void DetachNeedsTheDistanceHeldForTheDelay()
        {
            var d = new DetachTracker();
            float limit = EngulfMath.DetachLimit(1.37f, 1.5f);
            Assert.False(d.Update(0f, limit - 0.1f, limit, 0.75f));
            Assert.False(d.Update(1f, limit + 0.5f, limit, 0.75f));       // beyond since t = 1
            Assert.False(d.Update(1.5f, limit + 0.5f, limit, 0.75f));
            Assert.True(d.Update(1.75f, limit + 0.5f, limit, 0.75f));
            Assert.False(d.Update(1.8f, limit - 0.1f, limit, 0.75f));     // back within: the count restarts
            Assert.False(d.Update(2.0f, limit + 0.5f, limit, 0.75f));
            Assert.False(d.Update(2.5f, limit + 0.5f, limit, 0.75f));
            Assert.True(d.Update(2.75f, limit + 0.5f, limit, 0.75f));
        }

        [Fact]
        public void ChasingMassOnlyLosesAPlayerWhoKeepsOutrunningIt()
        {
            // Plan 5-1 check: a player teleported to r + 2.5 m is caught up by the mass (2.4 m/s) before DetachDelay (0.75 s) is over:
            // the gap to r + 1.5 closes in 1.0 / 2.4 = 0.42 s. Only a sprint (about 3.5 m/s slowed) that keeps the gap open detaches.
            var d = new DetachTracker();
            float limit = EngulfMath.DetachLimit(1.37f, 1.5f);
            bool detached = false;
            float gap = 1.0f;                                         // r + 2.5 compared with the limit r + 1.5
            for (float t = 0f; t < 3f; t += 0.02f)
            {
                gap = System.Math.Max(0f, gap - 2.4f * 0.02f);
                detached |= d.Update(t, limit + gap, limit, 0.75f);
            }
            Assert.False(detached);

            d = new DetachTracker();
            gap = 0f;                                                 // at the limit, then 3.5 - 2.4 = 1.1 m/s of escape
            detached = false;
            for (float t = 0f; t < 4f && !detached; t += 0.02f)
            {
                gap += 1.1f * 0.02f;
                detached = d.Update(t, limit + gap, limit, 0.75f);
            }
            Assert.True(detached);
        }

        [Fact]
        public void DetachDecisionOrder()
        {
            Assert.Equal(DetachReason.None, EngulfMath.Decide(true, false, false, false));
            Assert.Equal(DetachReason.TargetInvalid, EngulfMath.Decide(false, false, false, true));
            Assert.Equal(DetachReason.Dissolution, EngulfMath.Decide(false, true, false, true));
            Assert.Equal(DetachReason.Fire, EngulfMath.Decide(true, false, true, true));
            Assert.Equal(DetachReason.TooFar, EngulfMath.Decide(true, false, false, true));
        }

        [Fact]
        public void TickDurabilityScalesWithWormsAndWorld()
        {
            Assert.Equal(40f, EngulfMath.TickDurability(40, 1f, 1.5f, false), 3);
            Assert.Equal(60f, EngulfMath.TickDurability(40, 1f, 1.5f, true), 3);
        }
    }
}
