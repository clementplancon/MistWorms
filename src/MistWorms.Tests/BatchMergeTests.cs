using MistWorms.Render;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    public class BatchMergeTests
    {
        private static InstanceList[] Frames(params int[] counts)
        {
            var lists = new InstanceList[counts.Length];
            for (int i = 0; i < counts.Length; i++)
            {
                lists[i] = new InstanceList();
                lists[i].Reset();
                for (int k = 0; k < counts[i]; k++)
                {
                    var m = new Matrix4x4();
                    m.m03 = i * 100000 + k; // tag: frame and index
                    lists[i].Add(m, new Vector3(i, 0, k), 0.5f);
                }
            }
            return lists;
        }

        private static int Total(params InstanceList[][] groups)
        {
            int t = 0;
            foreach (var g in groups) foreach (var l in g) t += l.Count;
            return t;
        }

        private static int Used(InstanceList[][] groups, InstanceList[] fixedLists)
        {
            int u = 0;
            foreach (var l in fixedLists) u += l.Calls;
            foreach (var g in groups) foreach (var l in g) u += l.Calls;
            return u;
        }

        [Fact]
        public void UnderBudgetNothingMoves()
        {
            var g = new[] { Frames(3, 2, 1, 4) };
            var fx = new[] { Frames(5)[0] };
            int merged = BatchMerge.Apply(g, fx, 64, out int dropped);
            Assert.Equal(0, merged);
            Assert.Equal(0, dropped);
            Assert.Equal(new[] { 3, 2, 1, 4 }, new[] { g[0][0].Count, g[0][1].Count, g[0][2].Count, g[0][3].Count });
        }

        [Fact]
        public void NoInstanceIsLostAndBudgetIsReached()
        {
            var g = new[] { Frames(1, 1, 1, 1, 1, 1), Frames(2, 2, 2, 2) };
            var fx = new[] { Frames(1)[0] };
            int before = Total(g) + fx[0].Count;
            Assert.Equal(11, Used(g, fx));
            int merged = BatchMerge.Apply(g, fx, 6, out int dropped);
            Assert.Equal(0, dropped);
            Assert.Equal(before, Total(g) + fx[0].Count);
            Assert.True(Used(g, fx) <= 6);
            Assert.True(merged > 0);
        }

        [Fact]
        public void EvenFramesKeepTheirInstancesAndOddOnesEmpty()
        {
            var g = new[] { Frames(2, 3, 2, 3) };
            BatchMerge.Apply(g, new InstanceList[0], 1, out _);
            Assert.Equal(5, g[0][0].Count);
            Assert.Equal(0, g[0][1].Count);
            Assert.Equal(5, g[0][2].Count);
            Assert.Equal(0, g[0][3].Count);
            // original even instances are still there, in front
            Assert.Equal(0f, g[0][0].Matrices[0].m03);
            Assert.Equal(1f, g[0][0].Matrices[1].m03);
            Assert.Equal(100000f, g[0][0].Matrices[2].m03);
        }

        [Fact]
        public void PriorityOrderIsRespected()
        {
            var first = Frames(1, 1);
            var second = Frames(1, 1);
            var g = new[] { first, second };
            // 4 calls, budget 3: only the first group needs folding
            int merged = BatchMerge.Apply(g, new InstanceList[0], 3, out _);
            Assert.Equal(1, merged);
            Assert.Equal(2, first[0].Count);
            Assert.Equal(0, first[1].Count);
            Assert.Equal(1, second[0].Count);
            Assert.Equal(1, second[1].Count);
        }

        [Fact]
        public void EmptyEvenFrameIsNotUsedAsTarget()
        {
            var g = new[] { Frames(0, 4) };
            BatchMerge.Apply(g, new InstanceList[0], 0, out _);
            Assert.Equal(0, g[0][0].Count);
            Assert.Equal(4, g[0][1].Count);
        }

        [Fact]
        public void LargeListsCountSlices()
        {
            var g = new[] { Frames(1000, 1000) };
            Assert.Equal(2, Used(g, new InstanceList[0]));
            BatchMerge.Apply(g, new InstanceList[0], 1, out int dropped);
            Assert.Equal(0, dropped);
            Assert.Equal(2000, g[0][0].Count);
            Assert.Equal(2, g[0][0].Calls); // 2000 / 1023: the budget cannot be reached, nothing is lost
        }

        [Fact]
        public void BoundsAreUnioned()
        {
            var g = new[] { Frames(1, 1) };
            BatchMerge.Apply(g, new InstanceList[0], 0, out _);
            Bounds b = g[0][0].Bounds();
            Assert.True(b.min.x <= -0.5f + 1e-4f && b.max.x >= 1.5f - 1e-4f);
        }
    }
}
