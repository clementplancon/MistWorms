using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using MistWorms.Mass;
using MistWorms.Render;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MistWorms.Tests
{
    /// <summary>The layout worker (phase 4B-3): same result as the direct evaluation, no deadlock when masses come and go, no allocation, cost figures.</summary>
    public class MassWorkerTests
    {
        private readonly ITestOutputHelper _out;

        public MassWorkerTests(ITestOutputHelper output)
        {
            _out = output;
            // Frames per (lod, anim) and bounding radius, as the renderer would give them.
            foreach (int lod in new[] { 0, 1 })
            {
                MassTables.Set(lod, (int)WormAnim.Crawl, lod == 0 ? 16 : 8, 0.5f);
                MassTables.Set(lod, (int)WormAnim.Idle, lod == 0 ? 8 : 4, 0.5f);
                MassTables.Set(lod, (int)WormAnim.Coil, lod == 0 ? 12 : 6, 0.5f);
            }
        }

        private static MassFrameParams Params(int n, float t)
        {
            var P = MassFrameParams.Default(MistWorms.Render.MassMath.Radius(n, 0.8f, 0.55f), n);
            P.T = t; P.Tread = t * 0.2f; P.Boil = t * 0.1f; P.OdoFlux = t * 6f; P.OdoBorder = t * 2f; P.Sigma = 0.4f;
            return P;
        }

        private static MassJob NewJob(uint seed, int n, float t, int cap = 400, float p = 0.8f)
        {
            var job = new MassJob(new HeapSlots(seed));
            job.P = Params(n, t);
            job.Frame = MassLayout.MakeFrame(new Vector3(5f, 1f, -3f), new Vector3(0.6f, 0f, 0.8f), Vector3.up);
            job.Cam = new Vector3(5f, 6f, -10f);
            job.Unculled = true;
            job.Cap = cap;
            job.PThreshold = p;
            job.FrameCounter = 10;
            job.DivBody = 1; job.DivFlux = 1;
            job.RenderDistance = 60f;
            job.WriteIdx = 0;
            return job;
        }

        private struct RecordingSink : IHeapSink
        {
            public Matrix4x4 Frame;
            public List<float> X;
            public void AddWorm(in SlotOut o)
            {
                Matrix4x4 m;
                MassLayout.ComposeWorld(Frame, o, out m);
                X.Add(m.m03);
            }
        }

        [Fact]
        public void JobOutputEqualsTheDirectEvaluation()
        {
            MassJob job = NewJob(7u, 200, 3f);
            job.Run();
            Assert.True(job.Done);
            MassOutput o = job.Out[0];
            Assert.True(o.Valid, job.Error);

            var slots = new HeapSlots(7u);
            MassFrameParams P = Params(200, 3f);
            int count = MassLayout.Evaluate(P, slots, 0.8f, 10, 1, 1);
            var rec = new RecordingSink { Frame = job.Frame, X = new List<float>() };
            int drawn = MassLayout.Draw(ref rec, slots, count, 400, 0.8f, out int ne, out int nv);

            Assert.Equal(drawn, o.Added);
            Assert.Equal(ne, o.NEval);
            Assert.Equal(nv, o.NVis);
            int inBuckets = 0;
            var fromJob = new List<float>();
            for (int u = 0; u < o.UsedCount; u++)
            {
                MassBucket b = o.Buckets[o.Used[u]];
                inBuckets += b.Count;
                for (int k = 0; k < b.Count; k++) fromJob.Add(b.M[k].m03);
            }
            Assert.Equal(drawn, inBuckets);
            rec.X.Sort(); fromJob.Sort();
            for (int k = 0; k < rec.X.Count; k++) Assert.Equal(rec.X[k], fromJob[k], 3);
            Assert.True(drawn > 100, "drawn " + drawn);
            for (int j = 0; j < o.Lobes.Length; j++) Assert.Equal(slots.Lobes[j].Cx, o.Lobes[j].Cx, 4);
        }

        [Fact]
        public void BucketBoundsContainTheirWorms()
        {
            MassJob job = NewJob(11u, 120, 1.5f);
            job.Run();
            MassOutput o = job.Out[0];
            for (int u = 0; u < o.UsedCount; u++)
            {
                MassBucket b = o.Buckets[o.Used[u]];
                for (int k = 0; k < b.Count; k++)
                {
                    Assert.True(b.M[k].m03 >= b.Min.x && b.M[k].m03 <= b.Max.x);
                    Assert.True(b.M[k].m13 >= b.Min.y && b.M[k].m13 <= b.Max.y);
                    Assert.True(b.M[k].m23 >= b.Min.z && b.M[k].m23 <= b.Max.z);
                }
            }
        }

        [Fact]
        public void TheWorkerThreadRunsJobsAndStopsCleanly()
        {
            MassJob job = NewJob(3u, 80, 2f);
            job.Busy = true;
            Assert.True(MassWorker.Enqueue(job));
            var watch = Stopwatch.StartNew();
            while (!job.Done && watch.ElapsedMilliseconds < 5000) Thread.Sleep(1);
            Assert.True(job.Done, "the worker did not run the job");
            Assert.True(job.Out[0].Valid, job.Error);
            Assert.True(job.Out[0].Added > 20);
            MassWorker.Stop();
            Assert.False(MassWorker.Running);
        }

        [Fact]
        public void MassesComingAndGoingWhileTheWorkerRunsNeverDeadlock()
        {
            var jobs = new MassJob[16];
            for (int i = 0; i < jobs.Length; i++) jobs[i] = NewJob((uint)(100 + i), 30 + 10 * i, 1f);
            var watch = Stopwatch.StartNew();
            var rng = new System.Random(5);
            int observed = 0, replaced = 0;
            for (int step = 0; step < 4000 && watch.ElapsedMilliseconds < 20000; step++)
            {
                int i = rng.Next(jobs.Length);
                MassJob j = jobs[i];
                if (j.Done)
                {
                    Assert.True(j.Out[j.WriteIdx].Valid, j.Error);
                    j.ReadyIdx = j.WriteIdx; j.Busy = false; j.ClearDone(); observed++;
                }
                if (!j.Busy)
                {
                    j.P = Params(30 + 10 * i, step * 0.016f);
                    j.FrameCounter = step;
                    j.WriteIdx = j.ReadyIdx < 0 ? 0 : 1 - j.ReadyIdx;
                    j.Busy = true; j.ClearDone();
                    if (!MassWorker.Enqueue(j)) j.Busy = false;
                }
                else if (rng.Next(20) == 0)
                {
                    jobs[i] = NewJob((uint)(500 + step), 40, 1f);       // the mass is destroyed while the worker may still hold its job
                    replaced++;
                }
                if (step % 50 == 0) Thread.Sleep(1);
            }
            MassWorker.Stop();
            Assert.False(MassWorker.Running);
            Assert.True(observed > 50, "observed " + observed);
            _out.WriteLine($"stress: {observed} results observed, {replaced} masses replaced while busy, {MassWorker.JobsRun} jobs run");
        }

        [Fact]
        public void RunningAJobAllocatesNothingOnceWarm()
        {
            MassJob job = NewJob(9u, 200, 3f);
            for (int k = 0; k < 1200; k++) { job.FrameCounter = k; job.P.T = 3f + k * 0.016f; job.WriteIdx = k & 1; job.Run(); }
            AppDomain.MonitoringIsEnabled = true;
            // The counter is process wide and xunit runs other tests at the same time: the least of three runs is the job's own allocation.
            long delta = long.MaxValue;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                for (int k = 0; k < 500; k++) { job.FrameCounter = 1200 + attempt * 500 + k; job.P.T = 3f + (k + 1200) * 0.016f; job.WriteIdx = k & 1; job.Run(); }
                long d = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;
                if (d < delta) delta = d;
            }
            _out.WriteLine($"500 jobs allocated {delta} bytes");
            Assert.True(delta < 64 * 1024, "allocated " + delta);
        }

        /// <summary>Informational (.NET, not Mono): cost of the worker job of a mass of 200 and of the block copy of its buffer into renderer lists.</summary>
        [Fact]
        public void BenchmarkWorkerJobAndBulkCopy()
        {
            MassJob job = NewJob(123u, 200, 2f, 600, 1f);
            job.DivBody = 6; job.DivFlux = 2;
            for (int k = 0; k < 30; k++) { job.FrameCounter = k; job.Run(); }
            const int N = 1000;
            var sw = Stopwatch.StartNew();
            for (int k = 0; k < N; k++) { job.FrameCounter = 30 + k; job.P.T = 2f + k * 0.016f; job.WriteIdx = k & 1; job.Run(); }
            double jobUs = sw.Elapsed.TotalMilliseconds * 1000.0 / N;

            MassOutput o = job.Out[(N - 1) & 1];
            var lists = new Dictionary<int, InstanceList>();
            for (int u = 0; u < o.UsedCount; u++) lists[o.Used[u]] = new InstanceList();
            var bounds = new Bounds();
            sw.Restart();
            for (int k = 0; k < N; k++)
            {
                for (int u = 0; u < o.UsedCount; u++)
                {
                    MassBucket b = o.Buckets[o.Used[u]];
                    InstanceList l = lists[o.Used[u]];
                    l.Reset();
                    bounds.SetMinMax(b.Min, b.Max);
                    l.AddRange(b.M, b.Count, bounds.min, bounds.max);
                }
            }
            double copyUs = sw.Elapsed.TotalMilliseconds * 1000.0 / N;
            _out.WriteLine($"n=200, cap 600, divisors 6/2: worker job {jobUs:0.0} us (evaluate {o.EvalMs * 1000.0:0.0} + draw {o.DrawMs * 1000.0:0.0}); block copy of {o.Added} worms in {o.UsedCount} buckets {copyUs:0.0} us");
            Assert.True(o.Added > 100);
        }
    }
}
