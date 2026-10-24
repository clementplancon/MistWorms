using System.Linq;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    public class SpatialGridTests
    {
        private const int K = 4;

        private static int Query(SpatialGrid grid, Vector3[] pos, int self, float radius, int[] idx, float[] d2, float maxDy = 1.5f)
            => grid.QueryNearest(pos[self].x, pos[self].y, pos[self].z, radius, maxDy, self, pos, K, idx, d2);

        private static SpatialGrid Build(Vector3[] pos)
        {
            var grid = new SpatialGrid(pos.Length);
            for (int i = 0; i < pos.Length; i++) grid.Insert(i, pos[i].x, pos[i].z);
            return grid;
        }

        [Fact]
        public void FindsNeighborsSortedByDistanceAndExcludesSelf()
        {
            var pos = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0.4f, 0f, 0f),
                new Vector3(0.1f, 0f, 0.1f),
                new Vector3(0f, 0f, 0.3f),
                new Vector3(5f, 0f, 5f),
            };
            SpatialGrid grid = Build(pos);
            var idx = new int[K];
            var d2 = new float[K];

            int n = Query(grid, pos, 0, 0.5f, idx, d2);

            Assert.Equal(3, n);
            Assert.Equal(new[] { 2, 3, 1 }, new[] { idx[0], idx[1], idx[2] });
            Assert.True(d2[0] <= d2[1] && d2[1] <= d2[2]);
        }

        [Fact]
        public void NegativeCoordinatesAndCellBorders()
        {
            // Two points either side of x = 0 and of the cell border at x = -2.
            var pos = new[]
            {
                new Vector3(-0.05f, 0f, -0.05f),
                new Vector3(0.05f, 0f, 0.05f),
                new Vector3(-2.01f, 0f, 0f),
                new Vector3(-1.99f, 0f, 0f),
            };
            SpatialGrid grid = Build(pos);
            var idx = new int[K];
            var d2 = new float[K];

            Assert.Equal(1, Query(grid, pos, 0, 0.5f, idx, d2));
            Assert.Equal(1, idx[0]);
            Assert.Equal(1, Query(grid, pos, 2, 0.5f, idx, d2));
            Assert.Equal(3, idx[0]);
            Assert.Equal(-1, SpatialGrid.CellOf(-0.0001f));
            Assert.Equal(-2, SpatialGrid.CellOf(-2.01f));
            Assert.Equal(0, SpatialGrid.CellOf(1.99f));
        }

        [Fact]
        public void KeepsOnlyTheKClosest()
        {
            var pos = new Vector3[10];
            for (int i = 0; i < pos.Length; i++) pos[i] = new Vector3(i * 0.05f, 0f, 0f);
            SpatialGrid grid = Build(pos);
            var idx = new int[K];
            var d2 = new float[K];

            int n = Query(grid, pos, 0, 2f, idx, d2);

            Assert.Equal(K, n);
            Assert.Equal(new[] { 1, 2, 3, 4 }, idx);
        }

        [Fact]
        public void HeightDifferenceExcludesOtherFloors()
        {
            var pos = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.1f, 3f, 0f), new Vector3(0.1f, 0.5f, 0f) };
            SpatialGrid grid = Build(pos);
            var idx = new int[K];
            var d2 = new float[K];

            Assert.Equal(1, Query(grid, pos, 0, 1f, idx, d2));
            Assert.Equal(2, idx[0]);
        }

        [Fact]
        public void ClearAllowsReuseWithTheSameArrays()
        {
            var pos = new[] { new Vector3(0f, 0f, 0f), new Vector3(0.2f, 0f, 0f) };
            var grid = new SpatialGrid(pos.Length);
            var idx = new int[K];
            var d2 = new float[K];

            for (int round = 0; round < 3; round++)
            {
                grid.Clear();
                Assert.Equal(0, grid.Count);
                // Nothing from the previous round may survive a Clear.
                Assert.Equal(0, Query(grid, pos, 0, 1f, idx, d2));
                for (int i = 0; i < pos.Length; i++) grid.Insert(i, pos[i].x, pos[i].z);
                Assert.Equal(2, grid.Count);
                Assert.Equal(1, Query(grid, pos, 0, 1f, idx, d2));
            }
        }

        [Fact]
        public void ManyWormsAtTheCapacityLimit()
        {
            // Every item in its own cell: the table must hold capacity cells without looping forever.
            const int count = 450;
            var pos = new Vector3[count];
            for (int i = 0; i < count; i++) pos[i] = new Vector3((i % 30) * 2.5f - 30f, 0f, (i / 30) * 2.5f - 15f);
            SpatialGrid grid = Build(pos);
            var idx = new int[K];
            var d2 = new float[K];

            Assert.Equal(count, grid.Count);
            Assert.Equal(0, Query(grid, pos, 0, 0.5f, idx, d2)); // 2.5 m apart
            // Item 31 has four neighbours at 2.5 m (left, right, above, below); the diagonals are 3.5 m away.
            Assert.Equal(4, Query(grid, pos, 31, 2.6f, idx, d2));
            Assert.Equal(new[] { 1, 30, 32, 61 }, new[] { idx[0], idx[1], idx[2], idx[3] }.OrderBy(x => x).ToArray());
        }
    }
}
