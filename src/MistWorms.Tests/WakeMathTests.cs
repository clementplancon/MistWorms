using System;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>The alert chain between the nest groups, the waking and the return to sleep (spec 5.1, 5.2; plan 7-2).</summary>
    public class WakeMathTests
    {
        private const int Count = 8;

        private static void Run(Vector3[] pos, bool[] alive, float radius, int start, float[] times)
        {
            AlertMath.Schedule(pos, alive, Count, radius, 0.5f, start, 10f, times, new int[Count]);
        }

        private static Vector3[] Line(params float[] x)
        {
            var p = new Vector3[Count];
            for (int i = 0; i < x.Length; i++) p[i] = new Vector3(x[i], 0f, 0f);
            return p;
        }

        private static bool[] Alive(int n)
        {
            var a = new bool[Count];
            for (int i = 0; i < n; i++) a[i] = true;
            return a;
        }

        // ------------------------------------------------------------------ alert chain

        [Fact]
        public void AChainWakesOneHopEveryHalfSecond()
        {
            var times = new float[Count];
            Run(Line(0f, 15f, 30f, 45f), Alive(4), 25f, 0, times);
            Assert.Equal(10f, times[0]);
            Assert.Equal(10.5f, times[1]);
            Assert.Equal(11f, times[2]);
            Assert.Equal(11.5f, times[3]);
        }

        [Fact]
        public void AGroupBeyondTheRadiusOfEveryReachedGroupSleepsOn()
        {
            var times = new float[Count];
            Run(Line(0f, 15f, 30f, 60f), Alive(4), 25f, 0, times);
            Assert.Equal(11f, times[2]);
            Assert.Equal(AlertMath.Never, times[3]);      // 30 m from the last link
        }

        [Fact]
        public void TheRadiusIsInclusive()
        {
            var times = new float[Count];
            Run(Line(0f, 25f), Alive(2), 25f, 0, times);
            Assert.Equal(10.5f, times[1]);
            Run(Line(0f, 25.01f), Alive(2), 25f, 0, times);
            Assert.Equal(AlertMath.Never, times[1]);
        }

        [Fact]
        public void TheShortestChainWins()
        {
            // 0 and 2 are 20 m apart: group 2 is one hop from 0, not two through group 1.
            var times = new float[Count];
            Run(Line(0f, 10f, 20f), Alive(3), 25f, 0, times);
            Assert.Equal(10.5f, times[1]);
            Assert.Equal(10.5f, times[2]);
        }

        [Fact]
        public void ADeadGroupIsNeverReachedAndPassesNothingOn()
        {
            var times = new float[Count];
            bool[] alive = Alive(4);
            alive[1] = false;                              // the nest in the middle is gone
            Run(Line(0f, 15f, 20f, 40f), alive, 25f, 0, times);
            Assert.Equal(AlertMath.Never, times[1]);       // dead: never woken...
            Assert.Equal(10.5f, times[2]);                 // ...but the others are reached by the direct way
            Assert.Equal(11f, times[3]);
        }

        [Fact]
        public void AChainBrokenByADeadGroupStops()
        {
            var times = new float[Count];
            bool[] alive = Alive(3);
            alive[1] = false;
            Run(Line(0f, 20f, 40f), alive, 25f, 0, times);
            Assert.Equal(AlertMath.Never, times[1]);
            Assert.Equal(AlertMath.Never, times[2]);       // 40 m from the start, and the only link is dead
        }

        [Fact]
        public void TheStartWakesAtOnceEvenWithoutANest()
        {
            var times = new float[Count];
            bool[] alive = Alive(3);
            alive[0] = false;
            Run(Line(0f, 10f, 20f), alive, 25f, 0, times);
            Assert.Equal(10f, times[0]);
            Assert.Equal(AlertMath.Never, times[1]);
            Assert.Equal(AlertMath.Never, times[2]);
        }

        [Fact]
        public void AStartOutsideTheTableWakesNobody()
        {
            var times = new float[Count];
            Run(Line(0f, 10f), Alive(2), 25f, Count, times);
            for (int j = 0; j < Count; j++) Assert.Equal(AlertMath.Never, times[j]);
            Run(Line(0f, 10f), Alive(2), 25f, -1, times);
            for (int j = 0; j < Count; j++) Assert.Equal(AlertMath.Never, times[j]);
        }

        [Fact]
        public void TheDistanceIsMeasuredInThreeDimensions()
        {
            var pos = new Vector3[Count];
            pos[1] = new Vector3(0f, 30f, 0f);             // another level of the mine
            var times = new float[Count];
            Run(pos, Alive(2), 25f, 0, times);
            Assert.Equal(AlertMath.Never, times[1]);
        }

        [Fact]
        public void ANewAlertOnlyBringsTheWakeTimeForward()
        {
            var alertAt = new float[Count];
            for (int j = 0; j < Count; j++) alertAt[j] = AlertMath.Never;
            var awake = new bool[Count];
            var times = new float[Count];

            Run(Line(0f, 15f, 30f), Alive(3), 25f, 0, times);
            Assert.Equal(2, AlertMerge.Into(alertAt, times, awake, Count, 0));
            Assert.Equal(10.5f, alertAt[1]);
            Assert.Equal(11f, alertAt[2]);
            Assert.Equal(AlertMath.Never, alertAt[0]);      // the start is not scheduled, it wakes at once

            // A second alert from the other end reaches group 1 at 10.5 again (not earlier: nothing changes) and group 0 at 11: newly scheduled; its own start is left alone.
            Run(Line(0f, 15f, 30f), Alive(3), 25f, 2, times);
            Assert.Equal(1, AlertMerge.Into(alertAt, times, awake, Count, 2));
            Assert.Equal(10.5f, alertAt[1]);
            Assert.Equal(11f, alertAt[0]);
            Assert.Equal(11f, alertAt[2]);

            // A third one, sooner for group 2 (it starts at 9.0 from group 1): its time moves up.
            AlertMath.Schedule(Line(0f, 15f, 30f), Alive(3), Count, 25f, 0.5f, 1, 9f, times, new int[Count]);
            Assert.Equal(0, AlertMerge.Into(alertAt, times, awake, Count, 1));
            Assert.Equal(9.5f, alertAt[0]);
            Assert.Equal(9.5f, alertAt[2]);
            Assert.Equal(10.5f, alertAt[1]);                // the start of an alert is not touched by it
        }

        [Fact]
        public void AnAwakeGroupIsNotScheduled()
        {
            var alertAt = new float[Count];
            for (int j = 0; j < Count; j++) alertAt[j] = AlertMath.Never;
            var awake = new bool[Count];
            awake[1] = true;
            var times = new float[Count];
            Run(Line(0f, 15f), Alive(2), 25f, 0, times);
            Assert.Equal(0, AlertMerge.Into(alertAt, times, awake, Count, 0));
            Assert.Equal(AlertMath.Never, alertAt[1]);
        }

        // ------------------------------------------------------------------ waking

        [Fact]
        public void TheStaggerIsBoundedAndStable()
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int slot = 0; slot < 200; slot++)
            {
                float d = WakeMath.Stagger(slot, 3);
                Assert.InRange(d, 0f, WakeMath.MaxStagger);
                Assert.Equal(d, WakeMath.Stagger(slot, 3));
                min = Math.Min(min, d);
                max = Math.Max(max, d);
            }
            Assert.True(max - min > 0.25f, "the delays of a group are spread over the interval");
            Assert.NotEqual(WakeMath.Stagger(7, 1), WakeMath.Stagger(7, 2));
        }

        // ------------------------------------------------------------------ calm

        [Fact]
        public void TheCalmDelayIsStrictlyExceeded()
        {
            Assert.False(WakeMath.CalmElapsed(100f, 100f, 60f));
            Assert.False(WakeMath.CalmElapsed(159.9f, 100f, 60f));
            Assert.False(WakeMath.CalmElapsed(160f, 100f, 60f));
            Assert.True(WakeMath.CalmElapsed(160.01f, 100f, 60f));
            Assert.True(WakeMath.CalmElapsed(105.1f, 100f, 5f));
        }

        [Fact]
        public void AFreshLastActiveTimeKeepsTheGroupAwake()
        {
            // The calm pass refreshes lastActive every second a player is near: the delay counts from the last such second.
            float last = 0f;
            for (float now = 1f; now <= 200f; now += 1f)
            {
                bool playerNear = now <= 100f;
                if (playerNear) last = now;
                bool asleep = WakeMath.CalmElapsed(now, last, 60f);
                Assert.Equal(now > 160f, asleep);
            }
        }

        // ------------------------------------------------------------------ the ring of the sleepers of a mass

        [Theory]
        [InlineData(1)]
        [InlineData(6)]
        [InlineData(50)]
        [InlineData(200)]
        public void TheRingStaysBetweenItsBounds(int n)
        {
            for (int k = 0; k < n; k++)
            {
                WakeMath.RingPoint(k, n, 0.3f, 0.4f, 2f, out float dx, out float dz);
                float r = (float)Math.Sqrt(dx * dx + dz * dz);
                Assert.InRange(r, 0.4f - 1e-4f, 2f + 1e-4f);
            }
        }

        [Fact]
        public void TheRingPointsAreDistinctAndSpreadAround()
        {
            const int n = 40;
            var seen = new System.Collections.Generic.List<Vector2>();
            int[] octants = new int[8];
            for (int k = 0; k < n; k++)
            {
                WakeMath.RingPoint(k, n, 1f, 0.4f, 2f, out float dx, out float dz);
                var p = new Vector2(dx, dz);
                foreach (Vector2 q in seen) Assert.True((p - q).magnitude > 0.05f, "no two worms on the same spot");
                seen.Add(p);
                octants[(int)((Math.Atan2(dz, dx) + Math.PI) / (2 * Math.PI) * 8) % 8]++;
            }
            foreach (int o in octants) Assert.True(o > 0, "every side of the nest gets worms");
        }

        // ------------------------------------------------------------------ the nests of origin of a dissolution into sleepers

        [Fact]
        public void ADormantDissolutionGivesBackExactlyTheCountsOfEachNest()
        {
            var c = new NestCounts();
            c.Add(0, 3); c.Add(2, 4); c.Add(5, 1); c.Add(NestCounts.None, 2);
            c.FitTo(10);
            var into = new short[10];
            c.DrainTo(into, 10);

            var perNest = new int[8];
            foreach (short v in into) perNest[v + 1]++;      // -1 = no nest
            Assert.Equal(2, perNest[0]);                      // no nest
            Assert.Equal(3, perNest[1]);                      // nest 0
            Assert.Equal(0, perNest[2]);
            Assert.Equal(4, perNest[3]);                      // nest 2
            Assert.Equal(1, perNest[6]);                      // nest 5
            Assert.Equal(0, c.Total);
        }

        [Fact]
        public void ASetShorterThanTheDissolutionLeavesTheRestWithoutANest()
        {
            var c = new NestCounts();
            c.Add(1, 2);
            var into = new short[4];
            c.DrainTo(into, 4);
            Assert.Equal(new short[] { 1, 1, -1, -1 }, into);
        }

        [Fact]
        public void TheMajorityNestIsThePlainMajority()
        {
            var c = new NestCounts();
            Assert.Equal(-1, c.Majority());
            c.Add(NestCounts.None, 5);
            Assert.Equal(-1, c.Majority());                   // lone worms only: no nest
            c.Add(3, 2);
            Assert.Equal(-1, c.Majority());                   // more lone worms than the biggest nest share
            c.Add(3, 3);
            Assert.Equal(3, c.Majority());                    // tie with the no-nest worms: the nest
            c.Add(1, 5);
            Assert.Equal(1, c.Majority());                    // 5 against 5: the lowest index
            c.Add(3, 1);
            Assert.Equal(3, c.Majority());
        }
    }
}
