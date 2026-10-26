using System;
using MistWorms.Colony;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Gathering, formation and merging rules of the masses (spec §3.3, §4.1, §4.4; phase 4B-4), out of game.</summary>
    public class MassGatherTests
    {
        private static Vector3[] Ring(int n, float radius, Vector3 center)
        {
            var pts = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                float a = i * 2f * Mathf.PI / n;
                pts[i] = center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            }
            return pts;
        }

        [Fact]
        public void AClusterWithinTheRadiusFormsAMassAtItsCentroid()
        {
            Vector3[] pts = Ring(6, 1.5f, new Vector3(10f, 0f, 5f));
            var taken = new bool[6];
            var members = new int[6];
            int k = WormGather.SelectCluster(pts, taken, 6, 2.5f, 3, 200, members, 0, out Vector3 c);
            Assert.Equal(6, k);
            Assert.Equal(10f, c.x, 2);
            Assert.Equal(5f, c.z, 2);
            Assert.All(taken, t => Assert.True(t));
        }

        [Fact]
        public void TooFewWormsFormNothingAndMarkNothing()
        {
            Vector3[] pts = { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(20, 0, 0), new Vector3(-20, 0, 0) };
            var taken = new bool[4];
            var members = new int[4];
            Assert.Equal(0, WormGather.SelectCluster(pts, taken, 4, 2.5f, 3, 200, members, 0, out _));
            Assert.All(taken, t => Assert.False(t));
        }

        [Fact]
        public void WormsOnAnotherFloorAreNotInTheCluster()
        {
            Vector3[] pts = { new Vector3(0, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0, 0, 0.5f), new Vector3(0.2f, 6f, 0.2f) };
            var taken = new bool[4];
            var members = new int[4];
            int k = WormGather.SelectCluster(pts, taken, 4, 2.5f, 3, 200, members, 0, out _);
            Assert.Equal(3, k);
            Assert.False(taken[3]);
        }

        [Fact]
        public void TwoClustersAreFoundOneAfterTheOther()
        {
            Vector3[] a = Ring(4, 1f, new Vector3(0f, 0f, 0f));
            Vector3[] b = Ring(5, 1f, new Vector3(30f, 0f, 0f));
            var pts = new Vector3[9];
            Array.Copy(a, pts, 4);
            Array.Copy(b, 0, pts, 4, 5);
            var taken = new bool[9];
            var members = new int[9];
            int k1 = WormGather.SelectCluster(pts, taken, 9, 2.5f, 3, 200, members, 0, out Vector3 c1);
            int k2 = WormGather.SelectCluster(pts, taken, 9, 2.5f, 3, 200, members, 0, out Vector3 c2);
            Assert.Equal(9, k1 + k2);
            Assert.True(Mathf.Abs(c1.x - c2.x) > 25f);
            Assert.Equal(0, WormGather.SelectCluster(pts, taken, 9, 2.5f, 3, 200, members, 0, out _));
        }

        [Fact]
        public void AClusterNeverHoldsMoreThanTheMaximum()
        {
            Vector3[] pts = Ring(30, 1f, Vector3.zero);
            var taken = new bool[30];
            var members = new int[30];
            int k = WormGather.SelectCluster(pts, taken, 30, 2.5f, 3, 12, members, 0, out _);
            Assert.Equal(12, k);
        }

        [Fact]
        public void ARotatingSeedFindsAClusterFarFromTheFirstWorms()
        {
            // 20 scattered loners, then a tight group of 3: the first seeds fail, a later seed finds it.
            var pts = new Vector3[23];
            for (int i = 0; i < 20; i++) pts[i] = new Vector3(i * 10f, 0f, 0f);
            pts[20] = new Vector3(500f, 0f, 0f); pts[21] = new Vector3(500.5f, 0f, 0f); pts[22] = new Vector3(500f, 0f, 0.5f);
            var taken = new bool[23];
            var members = new int[23];
            int k = 0;
            for (int start = 0; start < 23 && k == 0; start += WormGather.MaxSeeds)
                k = WormGather.SelectCluster(pts, taken, 23, 2.5f, 3, 200, members, start, out _);
            Assert.Equal(3, k);
        }

        [Fact]
        public void AWormThatStaysStillOutsideTheReachOfAMassIsStalled()
        {
            float r = MassMath.Radius(39, 0.83f, 0.36f);
            Assert.True(WormGather.IsStalled(3.3f, r, 0f), "3.3 m from a mass of 39 and motionless is a failure");
            Assert.False(WormGather.IsStalled(3.3f, r, 1.2f), "it is moving");
            Assert.False(WormGather.IsStalled(2.0f, r, 0f), "inside the reach it is absorbed, not stalled");
        }

        [Fact]
        public void AMassWithRoomBeatsAGroupAndTheWormTouchesItsWiderEnvelope()
        {
            Vector3[] pos = { new Vector3(10, 0, 0), new Vector3(4, 0, 0), new Vector3(2.8f, 0, 0) };
            int[] load = { 5, 200, 39 };
            Vector3 worm = Vector3.zero;
            Assert.Equal(2, WormGather.NearestMass(worm, pos, load, 3, 200, 30f, 4f));          // the nearest with room: the full one at 4 m is skipped
            Assert.Equal(-1, WormGather.NearestMass(worm, pos, new[] { 200, 200, 200 }, 3, 200, 30f, 4f));
            Assert.Equal(-1, WormGather.NearestMass(worm, pos, load, 3, 200, 2f, 4f));            // out of range
            Assert.Equal(-1, WormGather.NearestMass(new Vector3(0, 9, 0), pos, load, 3, 200, 30f, 4f));   // another floor
            // A mass of 39 (r about 1.35): the worm at 2.8 m used to wait for ever (r + 0.5 = 1.95); the envelope reaches 2.26.
            float r = MassMath.Radius(39, 0.83f, 0.36f);
            Assert.True(WormGather.AbsorbReach(r) > r + 0.5f);
            Assert.True(WormGather.AbsorbReach(r) >= 2.2f);
        }

        [Fact]
        public void ASmallMassStillReachesTwoMetresAndAHalf()
        {
            // r(38) = 0.42 m after the v3 config (BaseRadius 0.26): the envelope term is 1.05 m, two worms 1.7 m from the centre of a mass of 38 waited for ever.
            float r38 = MassMath.Radius(38, 0.26f, 0.36f);
            Assert.True(WormGather.AbsorbReach(r38) >= 1.7f + 0.3f);
            Assert.Equal(WormGather.MinAbsorbReach, WormGather.AbsorbReach(MassMath.Radius(5, 0.26f, 0.36f)));
            Assert.Equal(WormGather.MinAbsorbReach, WormGather.AbsorbReach(MassMath.Radius(200, 0.26f, 0.36f)));
            // a big mass keeps its envelope term
            Assert.Equal(3f * 1.3f + 0.5f, WormGather.AbsorbReach(3f), 3);
            // the stall rule follows the same reach
            Assert.False(WormGather.IsStalled(1.7f, r38, 0f));
            Assert.True(WormGather.IsStalled(3.5f, r38, 0f));
            Assert.True(WormGather.StalledAbsorbReach > WormGather.MinAbsorbReach);
        }

        [Fact]
        public void GroupCentroidNeedsOthersWithinTheRadius()
        {
            Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(8, 0, 0), new Vector3(100, 0, 0) };
            int[] idx = { 0, 1, 2, 3 };
            byte[] st = new byte[4];
            int n = WormGather.GroupCentroid(pos, idx, st, 4, 0, pos[0], 20f, 4f, true, (byte)WormState.Fleeing, out Vector3 c);
            Assert.Equal(2, n);
            Assert.Equal(4f, c.x, 3);   // (0 + 4 + 8) / 3: the worm itself counts
            // A worm alone with a single neighbour is not a group (the caller wants 2).
            Assert.Equal(1, WormGather.GroupCentroid(pos, idx, st, 2, 0, pos[0], 20f, 4f, true, (byte)WormState.Fleeing, out _));
        }

        [Fact]
        public void FleeingWormsCanBeLeftOutOfAGroup()
        {
            Vector3[] pos = { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) };
            int[] idx = { 0, 1, 2 };
            byte[] st = { 0, (byte)WormState.Fleeing, 0 };
            Assert.Equal(1, WormGather.GroupCentroid(pos, idx, st, 3, 0, pos[0], 20f, 4f, false, (byte)WormState.Fleeing, out _));
            Assert.Equal(2, WormGather.GroupCentroid(pos, idx, st, 3, 0, pos[0], 20f, 4f, true, (byte)WormState.Fleeing, out _));
        }

        // ------------------------------------------------------------------ merging

        [Theory]
        [InlineData(1.0f, 0f, 0.54f, 0.54f, true)]     // two small heaps: the floor of 1.35 m (two capsules) applies
        [InlineData(1.4f, 0f, 0.54f, 0.54f, false)]
        [InlineData(2.0f, 0f, 1.2f, 0.9f, true)]
        [InlineData(2.2f, 0f, 1.2f, 0.9f, false)]
        [InlineData(1.0f, 3f, 1.2f, 0.9f, false)]      // another floor
        public void TouchingUsesTheSumOfTheRadiiWithAFloor(float d, float dy, float r1, float r2, bool expected)
        {
            Assert.Equal(expected, MassMergeMath.Touching(d, dy, r1, r2));
        }

        [Fact]
        public void TheSmallerMassSlidesAndATieGoesToTheLowerId()
        {
            Assert.True(MassMergeMath.IsSmaller(10, 5, 1, 30, 5, 2));
            Assert.False(MassMergeMath.IsSmaller(30, 5, 2, 10, 5, 1));
            Assert.True(MassMergeMath.IsSmaller(20, 5, 1, 20, 5, 2));
            Assert.False(MassMergeMath.IsSmaller(20, 5, 2, 20, 5, 1));
            Assert.True(MassMergeMath.IsSmaller(20, 4, 9, 20, 5, 1));
            // Exactly one of the two acts.
            Assert.NotEqual(MassMergeMath.IsSmaller(20, 5, 1, 20, 5, 2), MassMergeMath.IsSmaller(20, 5, 2, 20, 5, 1));
        }

        [Theory]
        [InlineData(10, 30, 200, 10, true)]
        [InlineData(100, 100, 200, 100, true)]          // exactly full: still complete
        [InlineData(100, 101, 200, 99, false)]          // the small one (100) fills the big one (101) to 200 and keeps 1
        [InlineData(150, 190, 200, 10, false)]
        [InlineData(40, 200, 200, 0, false)]            // a full mass absorbs nothing
        public void TransferGivesEverythingOrExactlyWhatFits(int small, int big, int max, int expected, bool complete)
        {
            int moved = MassMergeMath.Transfer(small, big, max, out bool isComplete);
            Assert.Equal(expected, moved);
            Assert.Equal(complete, isComplete);
        }

        [Fact]
        public void OfTwoMassesExactlyTheSmallerOneActsAndNeverTheBigOne()
        {
            int[] counts = { 1, 3, 10, 15, 40, 100, 199, 200 };
            foreach (int a in counts)
                foreach (int b in counts)
                    foreach (bool aLowerId in new[] { true, false })
                    {
                        uint idA = aLowerId ? 1u : 2u, idB = aLowerId ? 2u : 1u;
                        var planA = MassMergeMath.Decide(a, 7, idA, b, 7, idB, 200, out int movedA);
                        var planB = MassMergeMath.Decide(b, 7, idB, a, 7, idA, 200, out int movedB);
                        bool aActs = planA != MassMergeMath.MergePlan.None, bActs = planB != MassMergeMath.MergePlan.None;
                        Assert.False(aActs && bActs, $"both act: {a} and {b}");
                        if (aActs) { Assert.True(a < b || (a == b && aLowerId), $"the bigger acts: {a} vs {b}"); Assert.True(b < 200); Assert.True(movedA >= 1 && movedA <= a); }
                        if (bActs) { Assert.True(b < a || (a == b && !aLowerId), $"the bigger acts: {b} vs {a}"); Assert.True(a < 200); Assert.True(movedB >= 1 && movedB <= b); }
                        bool someoneHasRoom = a < 200 && b < 200 && !(a == 200 || b == 200);
                        if (someoneHasRoom) Assert.True(aActs ^ bActs, $"nobody acts for {a} and {b}");
                    }
        }

        [Fact]
        public void AMassThatIsFullIsNeverTheTargetAndTheDecisionMovesTheRightCount()
        {
            Assert.Equal(MassMergeMath.MergePlan.None, MassMergeMath.Decide(10, 1, 1, 200, 1, 2, 200, out int moved));
            Assert.Equal(0, moved);
            Assert.Equal(MassMergeMath.MergePlan.Complete, MassMergeMath.Decide(10, 1, 1, 15, 1, 2, 200, out moved));
            Assert.Equal(10, moved);
            Assert.Equal(MassMergeMath.MergePlan.Partial, MassMergeMath.Decide(150, 1, 1, 190, 1, 2, 200, out moved));
            Assert.Equal(10, moved);
            Assert.Equal(MassMergeMath.MergePlan.None, MassMergeMath.Decide(15, 1, 1, 10, 1, 2, 200, out _));    // the bigger never moves
        }

        [Fact]
        public void APartialMergeNeverOverfillsAndConservesTheWorms()
        {
            for (int big = 1; big < 200; big += 7)
                for (int small = 1; small <= big; small += 5)
                {
                    int moved = MassMergeMath.Transfer(small, big, 200, out bool complete);
                    Assert.True(big + moved <= 200);
                    Assert.True(moved <= small);
                    if (!complete) Assert.Equal(200, big + moved);
                }
        }

        // ------------------------------------------------------------------ release points

        [Fact]
        public void ReleasePointsLieOnTheHeapFootprintAndAreDeterministic()
        {
            var a = new Vector3[40];
            var b = new Vector3[40];
            Vector3 root = new Vector3(3f, 1f, -2f);
            MassLayout.ReleasePoints(12345u, 40, 1.6f, root, Vector3.forward, Vector3.left, a);
            MassLayout.ReleasePoints(12345u, 40, 1.6f, root, Vector3.forward, Vector3.left, b);
            for (int i = 0; i < 40; i++)
            {
                Assert.Equal(a[i], b[i]);
                Vector3 d = a[i] - root;
                float horizontal = Mathf.Sqrt(d.x * d.x + d.z * d.z);
                Assert.True(horizontal <= 1.6f * MassLayout.ReleaseReach + 1e-3f);
                Assert.True(d.y >= 0f && d.y <= 1.6f * MassLayout.ReleaseHeight + 1e-3f);
                Assert.False(float.IsNaN(a[i].x + a[i].y + a[i].z));
            }
            MassLayout.ReleasePoints(777u, 40, 1.6f, root, Vector3.forward, Vector3.left, b);
            Assert.NotEqual(a[3], b[3]);
        }

        [Fact]
        public void ReleasePointsAreSpreadAroundTheHeap()
        {
            var p = new Vector3[60];
            MassLayout.ReleasePoints(99u, 60, 2f, Vector3.zero, Vector3.forward, Vector3.left, p);
            int[] quadrants = new int[4];
            foreach (Vector3 v in p) quadrants[(v.x >= 0 ? 0 : 1) + (v.z >= 0 ? 0 : 2)]++;
            Assert.All(quadrants, q => Assert.True(q >= 6));
        }

        [Fact]
        public void TheNewStateAndTheTransferBitDoNotCollideWithTheOthers()
        {
            Assert.Equal(7, (int)WormState.Absorbing);
            Assert.Equal(8, (int)WormState.Absorbed);
            Assert.Equal(9, (int)WormState.Dead);
            byte code = MassStateCodes.With(MassStateCodes.WithMode(0, MassStateCodes.Hunt), MassStateCodes.Transferring, true);
            Assert.True(MassStateCodes.IsTransferring(code));
            Assert.False(MassStateCodes.IsMerging(code));
            Assert.False(MassStateCodes.IsStream(code));
            Assert.Equal(MassStateCodes.Hunt, MassStateCodes.Mode(code));
            Assert.Contains("Transferring", MassStateCodes.Describe(code));
        }

        [Fact]
        public void AFreshStoreSlotHasNoMassLinkAndNoLeap()
        {
            var s = new WormStore(2);
            int i = s.Alloc(new Vector3(1, 2, 3), 0f, WormState.Idle, 10f, 0f);
            Assert.Equal(ZDOID.None, s.massRef[i]);
            Assert.Equal(0f, s.leapHeight[i]);
            Assert.Equal(new Vector3(1, 2, 3), s.absorbFrom[i]);
        }
    }
}
