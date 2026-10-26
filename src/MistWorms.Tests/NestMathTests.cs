using System.Linq;
using MistWorms.Colony;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Choice of the rooms that get a nest, caps and laying timer of the nests (spec 8.1 to 8.3, plan 7 A1).</summary>
    public class NestMathTests
    {
        private static readonly Vector3 Entrance = new Vector3(0f, 5000f, 0f);

        private static Vector3[] Line(params float[] xs) => xs.Select(x => new Vector3(x, 5000f, 0f)).ToArray();

        private static int[] Choose(Vector3[] points, float[] areas, int max, int seed = 1) =>
            NestMath.ChooseNests(points, areas, Entrance, max, NestMath.MinEntranceDistance, NestMath.MinSpacing, seed);

        [Fact]
        public void TheFirstNestIsTheBiggestRoomFarEnoughFromTheEntrance()
        {
            Vector3[] p = Line(5f, 30f, 60f, 90f);
            float[] a = { 400f, 64f, 100f, 36f };        // the biggest room is only 5 m from the entrance: not eligible
            int[] chosen = Choose(p, a, 1);
            Assert.Equal(new[] { 2 }, chosen);
        }

        [Fact]
        public void NoNestCloserThanTheMinimumToTheEntrance()
        {
            Vector3[] p = Line(3f, 8f, 12f, 19f, 25f, 40f);
            float[] a = { 50f, 50f, 50f, 50f, 50f, 50f };
            int[] chosen = Choose(p, a, 8);
            Assert.NotEmpty(chosen);
            Assert.All(chosen, i => Assert.True(Vector3.Distance(p[i], Entrance) >= NestMath.MinEntranceDistance));
        }

        [Fact]
        public void TheNextNestIsTheFarthestFromTheOnesAlreadyChosen()
        {
            Vector3[] p = Line(30f, 40f, 100f, 70f);      // the biggest is at 70: then the farthest from it is 30 (40 m), not 100 (30 m)
            float[] a = { 30f, 30f, 30f, 200f };
            int[] chosen = Choose(p, a, 2);
            Assert.Equal(new[] { 3, 0 }, chosen);
        }

        [Fact]
        public void ABiggerRoomWinsATieOfDistance()
        {
            // two rooms equally far (within the tie distance) from the first nest, the smaller one a little farther: the larger area is picked
            var p = new[] { new Vector3(50f, 5000f, 0f), new Vector3(50f, 5000f, 30.3f), new Vector3(50f, 5000f, -30f) };
            float[] a = { 500f, 40f, 90f };
            int[] chosen = Choose(p, a, 2);
            Assert.Equal(new[] { 0, 2 }, chosen);
        }

        [Fact]
        public void NeverMoreThanTheMaximum()
        {
            Vector3[] p = Line(Enumerable.Range(0, 40).Select(i => 25f + i * 10f).ToArray());
            float[] a = Enumerable.Repeat(60f, 40).ToArray();
            Assert.Equal(8, Choose(p, a, 8).Length);
            Assert.Equal(3, Choose(p, a, 3).Length);
            Assert.Empty(Choose(p, a, 0));
        }

        [Fact]
        public void FewerRoomsGiveFewerNests()
        {
            Vector3[] p = Line(30f, 50f);
            float[] a = { 40f, 60f };
            Assert.Equal(2, Choose(p, a, 8).Length);
            Assert.Empty(NestMath.ChooseNests(new Vector3[0], new float[0], Entrance, 8, 20f, 6f, 1));
        }

        [Fact]
        public void NestsKeepTheirDistanceFromEachOther()
        {
            // a cluster of rooms 2 m apart: only one of them can be chosen
            var p = new[] { new Vector3(40f, 5000f, 0f), new Vector3(42f, 5000f, 0f), new Vector3(44f, 5000f, 0f) };
            float[] a = { 50f, 60f, 70f };
            int[] chosen = Choose(p, a, 8);
            Assert.Equal(new[] { 2 }, chosen);

            Vector3[] wide = Line(Enumerable.Range(0, 30).Select(i => 25f + i * 7f).ToArray());
            int[] many = Choose(wide, Enumerable.Repeat(40f, 30).ToArray(), 8);
            for (int x = 0; x < many.Length; x++)
                for (int y = x + 1; y < many.Length; y++)
                    Assert.True(Vector3.Distance(wide[many[x]], wide[many[y]]) >= NestMath.MinSpacing);
        }

        [Fact]
        public void SameInputsSameNests()
        {
            Vector3[] p = Line(Enumerable.Range(0, 20).Select(i => 25f + i * 9f).ToArray());
            float[] a = Enumerable.Repeat(64f, 20).ToArray();      // all equal: only the seeded hash breaks the ties
            Assert.Equal(Choose(p, a, 8, 77), Choose(p, a, 8, 77));
            Assert.NotEqual(Choose(p, a, 8, 77).ToArray(), Choose(p, a, 8, 78).ToArray());
        }

        [Fact]
        public void MismatchedArraysGiveNothing()
        {
            Assert.Empty(NestMath.ChooseNests(Line(30f, 50f), new[] { 10f }, Entrance, 8, 20f, 6f, 1));
        }

        // ---------------------------------------------------------------- caps

        [Theory]
        [InlineData(0, 25, 0, 200, true)]
        [InlineData(24, 25, 199, 200, true)]
        [InlineData(25, 25, 100, 200, false)]      // the nest is full
        [InlineData(3, 25, 200, 200, false)]       // the mine is full
        [InlineData(26, 25, 250, 200, false)]
        public void CanLayChecksTheNestAndTheMine(int fromNest, int maxNest, int mine, int maxMine, bool expected)
        {
            Assert.Equal(expected, NestMath.CanLay(fromNest, maxNest, mine, maxMine));
        }

        [Fact]
        public void WormsInMassesCountTowardsTheCapOfTheNest()
        {
            // 20 free worms of the nest and 5 inside a mass: 25, the nest is full
            Assert.False(NestMath.CanLay(20 + 5, 25, 60, 200));
            Assert.True(NestMath.CanLay(20 + 4, 25, 60, 200));
        }

        // ---------------------------------------------------------------- laying timer

        [Fact]
        public void TheTimerLaysOneEggPerInterval()
        {
            float timer = 0f;
            int eggs = 0;
            for (int k = 0; k < 600; k++)         // 60 s at 10 Hz, interval 30 s
                if (NestMath.LayStep(ref timer, 0.1f, 30f, false, true)) eggs++;
            Assert.InRange(eggs, 1, 2);
        }

        [Fact]
        public void ThePausedTimerStandsStill()
        {
            float timer = 10f;
            for (int k = 0; k < 100; k++) Assert.False(NestMath.LayStep(ref timer, 0.1f, 30f, true, true));
            Assert.Equal(10f, timer);
        }

        [Fact]
        public void AFullNestWaitsAndLaysAsSoonAsThereIsRoom()
        {
            float timer = 0f;
            for (int k = 0; k < 100; k++) Assert.False(NestMath.LayStep(ref timer, 0.1f, 2f, false, false));
            Assert.Equal(2f, timer);
            Assert.True(NestMath.LayStep(ref timer, 0.05f, 2f, false, true));
            Assert.Equal(0f, timer);
        }

        [Fact]
        public void ThereIsNoCatchingUpAfterALongFrame()
        {
            float timer = 0f;
            Assert.True(NestMath.LayStep(ref timer, 100f, 30f, false, true));
            Assert.Equal(0f, timer);
            Assert.False(NestMath.LayStep(ref timer, 0.1f, 30f, false, true));
        }
    }
}
