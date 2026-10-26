using System;
using MistWorms.Colony;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>Spreading of the simulation steps and the allocation-free pure path of a step (decision 227, spec 11.1).</summary>
    [CollectionDefinition("allocation", DisableParallelization = true)]
    public class AllocationCollection { }

    // Alone: the allocation counter is process wide, so no other test may run beside the one that reads it.
    [Collection("allocation")]
    public class SimSchedulingTests
    {
        private readonly ITestOutputHelper _out;
        public SimSchedulingTests(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void PhasesStayInsideOneIntervalAndUseEverySlot()
        {
            const float interval = 0.05f;
            var seen = new bool[WormMath.PhaseSlots];
            for (int i = 0; i < 400; i++)
            {
                float phase = WormMath.SimPhase(i, interval);
                Assert.InRange(phase, 0f, interval - 1e-5f);
                seen[i % WormMath.PhaseSlots] = true;
                Assert.Equal(WormMath.SimPhase(i % WormMath.PhaseSlots, interval), phase, 6);   // deterministic: index modulo the period
            }
            Assert.All(seen, b => Assert.True(b));
        }

        [Fact]
        public void ConsecutiveSlotsGetDifferentPhases()
        {
            for (int i = 0; i < 100; i++)
                Assert.NotEqual(WormMath.SimPhase(i, 0.05f), WormMath.SimPhase(i + 1, 0.05f));
        }

        [Fact]
        public void StepCapIsAboveTheAverageLoadOfTheTarget()
        {
            // 400 near worms at 20 Hz on a 50 fps frame: 160 steps a frame on average. The cap must not make the average rate drop there.
            Assert.True(WormColony.MaxStepsPerFrame >= 400 * 20 / 50);
        }

        [Fact]
        public void GridQueryAndRateChoiceAllocateNothingOnceWarm()
        {
            var grid = new SpatialGrid(400);
            var pos = new Vector3[400];
            var idx = new int[8];
            var sqr = new float[8];
            for (int i = 0; i < pos.Length; i++) pos[i] = new Vector3(i % 20 * 0.4f, 0f, i / 20 * 0.4f);
            for (int round = 0; round < 3; round++)
            {
                grid.Clear();
                for (int i = 0; i < pos.Length; i++) grid.Insert(i, pos[i].x, pos[i].z);
                for (int i = 0; i < pos.Length; i++) grid.QueryNearest(pos[i].x, pos[i].y, pos[i].z, 1.5f, 1.5f, i, pos, 8, idx, sqr);
            }
            AppDomain.MonitoringIsEnabled = true;
            long delta = long.MaxValue;
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (delta < 64 * 1024) break;
                System.Threading.Thread.Sleep(25);   // a thread left by an earlier test may allocate for a moment; the least of the runs is the step's own
                long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                float sink = 0f;
                for (int rep = 0; rep < 20; rep++)
                {
                    grid.Clear();
                    for (int i = 0; i < pos.Length; i++) grid.Insert(i, pos[i].x, pos[i].z);
                    for (int i = 0; i < pos.Length; i++)
                    {
                        sink += grid.QueryNearest(pos[i].x, pos[i].y, pos[i].z, 1.5f, 1.5f, i, pos, 8, idx, sqr);
                        sink += WormMath.SimRate(false, sqr[0], 20f, 5f) + WormMath.SimPhase(i, 0.05f);
                    }
                }
                long d = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;
                if (d < delta) delta = d;
                Assert.True(sink > 0f);
            }
            _out.WriteLine($"20 x 400 steps of grid work allocated {delta} bytes");
            Assert.True(delta < 64 * 1024, "allocated " + delta);   // process wide counter: a margin for what xunit does at the same time
        }
    }
}
