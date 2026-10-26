using System.Linq;
using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Split of the lone worm budget of a mine over its rooms (spec 8.3).</summary>
    public class SeedMathTests
    {
        [Fact]
        public void BudgetFitsAndNoRoomExceedsTheMaximum()
        {
            int[] c = SeedMath.LoneWormsPerRoom(70, 40, 2, 123);
            Assert.Equal(40, c.Length);
            Assert.Equal(70, c.Sum());
            Assert.All(c, n => Assert.InRange(n, 1, 2));
        }

        [Fact]
        public void BudgetIsCappedByRoomsTimesMaximum()
        {
            int[] c = SeedMath.LoneWormsPerRoom(70, 10, 2, 5);
            Assert.Equal(20, c.Sum());
            Assert.All(c, n => Assert.Equal(2, n));
        }

        [Fact]
        public void FewRoomsGetOneEachBeforeAnyGetsTwo()
        {
            int[] c = SeedMath.LoneWormsPerRoom(7, 10, 2, 9);
            Assert.Equal(7, c.Sum());
            Assert.All(c, n => Assert.InRange(n, 0, 1));
        }

        [Fact]
        public void SameSeedSameSplit()
        {
            Assert.Equal(SeedMath.LoneWormsPerRoom(70, 33, 2, 77), SeedMath.LoneWormsPerRoom(70, 33, 2, 77));
        }

        [Fact]
        public void DegenerateInputsGiveNothing()
        {
            Assert.Empty(SeedMath.LoneWormsPerRoom(70, 0, 2, 1));
            Assert.Equal(0, SeedMath.LoneWormsPerRoom(0, 5, 2, 1).Sum());
            Assert.Equal(0, SeedMath.LoneWormsPerRoom(70, 5, 0, 1).Sum());
            Assert.Equal(0, SeedMath.LoneWormsPerRoom(-3, 5, 2, 1).Sum());
        }

        [Fact]
        public void MineSeedIsStableAndDependsOnTheWorldAndThePosition()
        {
            int a = SeedMath.MineSeed(1234, 100.2f, 5000.1f, -300.4f);
            Assert.Equal(a, SeedMath.MineSeed(1234, 100.2f, 5000.1f, -300.4f));
            Assert.Equal(a, SeedMath.MineSeed(1234, 100.4f, 5000f, -300.2f));     // rounded to the metre
            Assert.NotEqual(a, SeedMath.MineSeed(1235, 100.2f, 5000.1f, -300.4f));
            Assert.NotEqual(a, SeedMath.MineSeed(1234, 612f, 5000f, -300f));
            Assert.NotEqual(a, SeedMath.MineSeed(1234, 100f, 5000f, 300f));
        }

        [Fact]
        public void SeedWorksForNegativeValues()
        {
            int[] c = SeedMath.LoneWormsPerRoom(5, 8, 2, int.MinValue + 3);
            Assert.Equal(5, c.Sum());
        }
    }
}
