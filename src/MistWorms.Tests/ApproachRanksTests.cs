using MistWorms.Colony;
using Xunit;

namespace MistWorms.Tests
{
    public class ApproachRanksTests
    {
        [Fact]
        public void RanksCountUpPerTargetWithTheArrivalBearingAsBase()
        {
            var r = new ApproachRanks();
            Assert.Equal(0, r.Assign(7, 10f, 1.2f, out float b0));
            Assert.Equal(1, r.Assign(7, 10.1f, 2.5f, out float b1));
            Assert.Equal(2, r.Assign(7, 10.2f, -1f, out float b2));
            Assert.Equal(1.2f, b0);
            Assert.Equal(1.2f, b1);   // the first arrival of the wave fixes the base
            Assert.Equal(1.2f, b2);
        }

        [Fact]
        public void TargetsHaveTheirOwnCounter()
        {
            var r = new ApproachRanks();
            Assert.Equal(0, r.Assign(1, 0f, 0f, out _));
            Assert.Equal(0, r.Assign(2, 0f, 0f, out _));
            Assert.Equal(1, r.Assign(1, 0.1f, 0f, out _));
            Assert.Equal(1, r.Assign(2, 0.1f, 0f, out _));
        }

        [Fact]
        public void TheCounterRestartsAfterTwoQuietSeconds()
        {
            var r = new ApproachRanks();
            r.Assign(1, 0f, 0.5f, out _);
            r.Assign(1, 1f, 0f, out _);
            Assert.Equal(2, r.Assign(1, 2.9f, 0f, out _));            // 1.9 s since the last one: same wave
            Assert.Equal(0, r.Assign(1, 5.0f, 2.0f, out float b));    // 2.1 s: new wave, new base
            Assert.Equal(2.0f, b);
        }

        [Fact]
        public void ARankIsNotChangedByOthersComingAndGoing()
        {
            // The rank is a value held by the worm: a later arrival gets a larger one and never changes the first one's.
            var r = new ApproachRanks();
            int first = r.Assign(1, 0f, 0f, out _);
            for (int k = 0; k < 50; k++) r.Assign(1, 0.01f * k, 0f, out _);
            Assert.Equal(0, first);
            Assert.Equal(51, r.Assign(1, 0.6f, 0f, out _));
        }

        [Fact]
        public void AFullTableEvictsTheOldestTarget()
        {
            var r = new ApproachRanks();
            for (int k = 0; k < ApproachRanks.Slots; k++) r.Assign(100 + k, 10f + 0.1f * k, 0f, out _);
            r.Assign(1000, 11f, 0f, out _);                         // evicts target 100 (the oldest)
            Assert.Equal(1, r.Assign(1000, 11.1f, 0f, out _));
            Assert.Equal(1, r.Assign(101, 11.2f, 0f, out _));       // 101 kept its counter
            Assert.Equal(0, r.Assign(100, 11.3f, 0f, out _));       // 100 starts again
        }
    }
}
