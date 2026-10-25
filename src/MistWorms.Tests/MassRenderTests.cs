using System;
using MistWorms.Mass;
using UnityEngine;
using Xunit;

namespace MistWorms.Tests
{
    /// <summary>Pure parts of the swarm rendering (phase 4B-3, pass D): smoothed root, ground tilt, distance caps and LOD hysteresis.</summary>
    public class MassRenderTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;
        public MassRenderTests(Xunit.Abstractions.ITestOutputHelper output) { _out = output; }
        private const float Dt = 1f / 60f;
        private const float Speed = 2.4f;

        [Fact]
        public void RootHasNoLagWhileWalking()
        {
            var root = new MassRoot();
            Vector3 body = new Vector3(10f, 5f, 3f);
            root.Step(body, Dt, Speed, 0f, false);
            float worst = 0f;
            for (int f = 0; f < 180; f++)
            {
                body.x += Speed * Dt;
                root.Step(body, Dt, Speed, 0f, false);
                if (f >= 40) worst = Math.Max(worst, Math.Abs(root.Root.x - body.x));      // after the speed estimate has settled (0.3 s)
            }
            Assert.True(worst < 0.01f, "lag " + worst);
        }

        [Fact]
        public void RootIsSmoothWhileTheBodyMovesInPhysicsSteps()
        {
            // The body moves every 0.02 s (50 Hz), the frames come every 1/60 s: raw, the displacement per frame alternates 0 and 2.4 x 0.04.
            var root = new MassRoot();
            Vector3 body = Vector3.zero;
            root.Step(body, Dt, Speed, 0f, false);
            float t = 0f, nextTick = 0.02f, prevX = 0f, lo = float.MaxValue, hi = float.MinValue, rawLo = float.MaxValue, rawHi = float.MinValue, prevBodyX = 0f;
            float lag = 0f;
            for (int f = 0; f < 240; f++)
            {
                t += Dt;
                while (nextTick <= t) { nextTick += 0.02f; body.x = Speed * (nextTick - 0.02f); }
                root.Step(body, Dt, Speed, 0f, false);
                if (f > 60)
                {
                    float dx = root.Root.x - prevX, raw = body.x - prevBodyX;
                    lo = Math.Min(lo, dx); hi = Math.Max(hi, dx);
                    rawLo = Math.Min(rawLo, raw); rawHi = Math.Max(rawHi, raw);
                    lag = Math.Max(lag, Math.Abs(root.Root.x - Speed * (t - 0.01f)));     // the held body is half a tick behind the ideal path
                }
                prevX = root.Root.x; prevBodyX = body.x;
            }
            Assert.True(hi - lo < 0.015f, $"root step spread {hi - lo:0.0000} m (raw body {rawHi - rawLo:0.0000} m)");
            Assert.True(lag < 0.03f, "lag " + lag);
        }

        [Fact]
        public void ATeleportOfTheBodyIsResorbedNotShown()
        {
            var root = new MassRoot();
            Vector3 body = new Vector3(0f, 0f, 0f);
            root.Step(body, Dt, Speed, 0f, false);
            for (int f = 0; f < 10; f++) root.Step(body, Dt, Speed, 0f, false);

            body.x += 0.6f;                                   // a StepAssist hop
            Vector3 before = root.Root;
            root.Step(body, Dt, Speed, 0f, false);
            float firstFrame = Math.Abs(root.Root.x - before.x);
            Assert.True(firstFrame <= 0.05f + 1.5f * Speed * Dt + 0.6f * (1f - (float)Math.Pow(2.0, -Dt / MassRoot.OffsetHalfLife)) + (0.6f - MassRoot.MaxOffset) + 0.01f, "the root moved " + firstFrame + " in the frame of the hop");
            Assert.True(Math.Abs(root.Root.x - body.x) > 0.39f && Math.Abs(root.Root.x - body.x) <= MassRoot.MaxOffset + 0.01f, "gap " + Math.Abs(root.Root.x - body.x));

            float worstStep = 0f;
            Vector3 prev = root.Root;
            for (int f = 0; f < 90; f++)
            {
                root.Step(body, Dt, Speed, 0f, false);
                worstStep = Math.Max(worstStep, Math.Abs(root.Root.x - prev.x));
                prev = root.Root;
            }
            Assert.True(worstStep < 0.1f, "per frame " + worstStep);
            Assert.True(Math.Abs(root.Root.x - body.x) < 0.01f, "still " + Math.Abs(root.Root.x - body.x) + " m off after 1.5 s");
        }

        [Fact]
        public void ABigJumpSnapsTheRoot()
        {
            var root = new MassRoot();
            root.Step(Vector3.zero, Dt, Speed, 0f, false);
            root.Step(new Vector3(0f, 0f, 6f), Dt, Speed, 0f, false);
            Assert.True(root.Snapped);
            Assert.Equal(6f, root.Root.z, 4);
            Assert.Equal(0f, root.Offset.x, 4);
        }

        [Fact]
        public void HeightFollowsTheGroundPlaneWithoutAStep()
        {
            var root = new MassRoot();
            root.Step(Vector3.zero, Dt, Speed, 0f, true);
            float prev = root.Root.y;
            float worst = 0f;
            for (int f = 0; f < 60; f++)
            {
                root.Step(Vector3.zero, Dt, Speed, 0.4f, true);   // a 0.4 m stair in the plane
                worst = Math.Max(worst, Math.Abs(root.Root.y - prev));
                prev = root.Root.y;
            }
            Assert.True(worst <= 0.08f, "vertical step per frame " + worst);
            Assert.True(root.Root.y > 0.38f);
        }

        [Fact]
        public void UpAxisIsLimitedTo25Degrees()
        {
            var g = new MassGround();
            Assert.Equal(1f, g.UpAxis().y, 4);                // no plane yet
            g.Have = true;
            g.Gx = 1f;                                          // a 45 degree slope
            Vector3 u = g.UpAxis();
            Assert.True(Vector3.Angle(Vector3.up, u) <= 25.01f, "tilt " + Vector3.Angle(Vector3.up, u));
            g.Gx = 0.1f;
            float small = Vector3.Angle(Vector3.up, g.UpAxis());
            Assert.True(small > 0.5f && small < 5f, "tilt " + small);
            Assert.Equal(1f, u.magnitude, 3);
        }

        [Fact]
        public void PlaneHeightIsLinear()
        {
            var g = new MassGround { Have = true, H0 = 2f, X0 = 10f, Z0 = -4f, Gx = 0.5f, Gz = -0.25f };
            Assert.Equal(2f, g.HeightAt(10f, -4f), 4);
            Assert.Equal(3f, g.HeightAt(12f, -4f), 4);
            Assert.Equal(2.5f, g.HeightAt(10f, -6f), 4);
        }

        [Fact]
        public void CapsSlideWithTheDistance()
        {
            const float rd = 60f;
            int c0 = MassLayoutConst.Lod0Cap, c1 = MassLayoutConst.Lod1Cap, c2 = MassLayoutConst.Lod2Cap;
            Assert.Equal(c0, MassRenderSystem.CapAt(0f, rd, c0));
            Assert.Equal(c0, MassRenderSystem.CapAt(8f, rd, c0));
            Assert.Equal(c1, MassRenderSystem.CapAt(20f, rd, c0));
            Assert.Equal(c2, MassRenderSystem.CapAt(45f, rd, c0));
            Assert.Equal(0, MassRenderSystem.CapAt(rd + 1f, rd, c0));
            int prev = int.MaxValue;
            for (float d = 0f; d <= 70f; d += 0.25f)
            {
                int c = MassRenderSystem.CapAt(d, rd, MassLayoutConst.Lod0Cap);
                Assert.True(c <= prev, "cap rises at " + d);
                prev = c;
            }
            Assert.Equal(100, MassRenderSystem.CapAt(20f, rd, 100));    // a lowered LOD0 cap lowers the others
        }

        [Fact]
        public void LodHasAOneMetreHysteresis()
        {
            Assert.Equal(0, MassRenderSystem.NextLod(0, 12.5f));
            Assert.Equal(1, MassRenderSystem.NextLod(1, 12.5f));
            Assert.Equal(1, MassRenderSystem.NextLod(0, 13.5f));
            Assert.Equal(0, MassRenderSystem.NextLod(1, 10.5f));
            Assert.Equal(1, MassRenderSystem.NextLod(1, 30.5f));
            Assert.Equal(2, MassRenderSystem.NextLod(2, 30.5f));
            Assert.Equal(2, MassRenderSystem.NextLod(1, 31.5f));
            Assert.Equal(1, MassRenderSystem.NextLod(2, 28.5f));
        }

        private struct CountSink : IHeapSink
        {
            public int Count;
            public void AddWorm(in SlotOut o) { Count++; }
        }

        private static MassFrameParams Params(int n, float t)
        {
            var P = MassFrameParams.Default(MistWorms.Render.MassMath.Radius(n, 0.8f, 0.55f), n);
            P.T = t; P.Tread = t * 0.2f; P.Boil = t * 0.1f; P.OdoFlux = t * 6f; P.OdoBorder = t * 2f; P.Sigma = 0.5f;
            return P;
        }

        [Fact]
        public void SelectedEvaluationMatchesTheFullOneUnderTheThreshold()
        {
            var full = new HeapSlots(77u);
            var part = new HeapSlots(77u);
            var P = Params(200, 3.3f);
            int v = MassLayout.Evaluate(P, full);
            int v2 = MassLayout.Evaluate(P, part, 0.4f, 10, 1, 1);
            Assert.Equal(v, v2);
            int under = 0;
            for (int i = 0; i < v; i++)
            {
                if (part.Prio[i] < 0.4f)
                {
                    under++;
                    Assert.Equal(full.Out[i].Visible, part.Out[i].Visible);
                    Assert.Equal(full.Out[i].Pos.X, part.Out[i].Pos.X, 5);
                    Assert.Equal(full.Out[i].Pos.Z, part.Out[i].Pos.Z, 5);
                    Assert.Equal(full.Out[i].Frame, part.Out[i].Frame);
                }
                else Assert.False(part.Out[i].Visible);
            }
            Assert.InRange(under, (int)(0.3f * v), (int)(0.5f * v));
        }

        [Fact]
        public void DrawAddsTheSameWormsAsEmit()
        {
            var a = new HeapSlots(5u);
            var b = new HeapSlots(5u);
            var P = Params(120, 1.1f);
            var sa = new CountSink();
            var sb = new CountSink();
            int ea = MassLayout.Emit(ref sa, P, a, 150, 0.7f);
            int v = MassLayout.VisibleCount(120, a.Count);
            float p = Math.Min(1f, 150f / (v * 0.7f));
            int eb = MassLayout.EmitSelected(ref sb, P, b, 150, p, 4, 1, 1, out int evaluated, out int visible);
            Assert.Equal(ea, eb);
            Assert.Equal(sa.Count, sb.Count);
            Assert.True(evaluated >= eb && visible >= eb);
        }

        [Fact]
        public void RefreshDivisorsEvaluateAQuarterOfTheBodiesPerFrame()
        {
            var s = new HeapSlots(9u);
            var P = Params(200, 2f);
            MassLayout.Evaluate(P, s, 1f, 0, 4, 2);
            int[] prev = (int[])s.EvalFrame.Clone();
            int bodies = 0, flux = 0, refreshedBodies = 0, refreshedFlux = 0;
            MassLayout.Evaluate(P, s, 1f, 1, 4, 2);
            int v = MassLayout.VisibleCount(200, s.Count);
            for (int i = 0; i < v; i++)
            {
                bool refreshed = s.EvalFrame[i] == 1;
                if (s.Role[i] == MassLayout.RoleBody) { bodies++; if (refreshed) refreshedBodies++; }
                else { flux++; if (refreshed) refreshedFlux++; }
            }
            Assert.InRange(refreshedBodies / (float)bodies, 0.15f, 0.35f);
            Assert.InRange(refreshedFlux / (float)flux, 0.35f, 0.65f);
            // Every place is refreshed at least once in `div` frames.
            for (int f = 2; f < 5; f++) MassLayout.Evaluate(P, s, 1f, f, 4, 2);
            for (int i = 0; i < v; i++) Assert.True(4 - s.EvalFrame[i] <= 4, "slot " + i + " last " + s.EvalFrame[i]);
            for (int i = 0; i < v; i++) Assert.True(s.EvalFrame[i] >= 1, "slot " + i + " stale");
        }

        /// <summary>Informational (this runtime is not Mono, expect 4 to 5 times more in game): cost of one mass of 200 per frame, in microseconds.</summary>
        [Fact]
        public void BenchmarkOneMassOf200()
        {
            var s = new HeapSlots(123u);
            var P = Params(200, 2f);
            var frame = MassLayout.MakeFrame(Vector3.zero, Vector3.forward, Vector3.up);
            const int N = 2000;
            MassLayout.Evaluate(P, s);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int k = 0; k < N; k++) { P.T = k * 0.016f; MassLayout.Evaluate(P, s); }
            double full = sw.Elapsed.TotalMilliseconds * 1000.0 / N;
            sw.Restart();
            for (int k = 0; k < N; k++) { P.T = k * 0.016f; MassLayout.Evaluate(P, s, 1f, k, 6, 2); }
            double divided = sw.Elapsed.TotalMilliseconds * 1000.0 / N;
            int drawnCount = 0;
            for (int i = 0; i < 500; i++) if (s.Out[i].Visible) drawnCount++;
            sw.Restart();
            Matrix4x4 m = default;
            for (int k = 0; k < N; k++) for (int i = 0; i < 322; i++) MassLayout.ComposeWorld(frame, s.Out[i], out m);
            double compose = sw.Elapsed.TotalMilliseconds * 1000.0 / N;
            sw.Restart();
            for (int k = 0; k < N; k++) for (int i = 0; i < 322; i++) MassRenderSystem.ComposeFast(frame, s.Out[i], out m);
            double composeFast = sw.Elapsed.TotalMilliseconds * 1000.0 / N;
            _out.WriteLine($"ComposeFast x322 {composeFast:0.0} us");
            _out.WriteLine($"n=200: Evaluate all {full:0.0} us, with divisors 6/2 (LOD0) {divided:0.0} us, ComposeWorld x322 {compose:0.0} us, visible places {drawnCount}");
            Assert.True(divided < full);
        }

        [Fact]
        public void ComposeFastEqualsComposeWorld()
        {
            var s = new HeapSlots(8u);
            var P = Params(120, 4f);
            int v = MassLayout.Evaluate(P, s);
            var frame = MassLayout.MakeFrame(new Vector3(3f, 1f, -2f), new Vector3(0.6f, 0f, 0.8f), Vector3.up);
            for (int i = 0; i < v; i++)
            {
                Matrix4x4 a, b;
                MassLayout.ComposeWorld(frame, s.Out[i], out a);
                MassRenderSystem.ComposeFast(frame, s.Out[i], out b);
                for (int k = 0; k < 16; k++) Assert.Equal(a[k], b[k], 4);
            }
        }

        [Fact]
        public void ConfigMigrationOnlyReplacesTheOldDefault()
        {
            Assert.True(MistWorms.Config.ConfigMigrations.ShouldReplace(40, 40));
            Assert.False(MistWorms.Config.ConfigMigrations.ShouldReplace(120, 40));
        }

        /// <summary>Informational: highest visible place at engulf 1 (target 0.6 m past the edge), over all places and under the draw threshold, n = 50 and 200.</summary>
        [Fact]
        public void EngulfColumnHeightByCount()
        {
            foreach (int n in new[] { 50, 200 })
            {
                float all = 0f, drawn = 0f, worst = 9f;
                int cols = 0;
                for (int seed = 1; seed <= 6; seed++)
                {
                    var s = new HeapSlots((uint)seed * 7919u);
                    float r = MistWorms.Render.MassMath.Radius(n, 0.8f, 0.55f);
                    for (float t = 0f; t < 20f; t += 1.7f)
                    {
                        var P = Params(n, t);
                        P.Engulf = 1f; P.TargetDist = r + 0.6f; P.TargetTurns = 0.02f;
                        int v = MassLayout.Evaluate(P, s);
                        int nv = 0;
                        for (int i = 0; i < v; i++) if (s.Out[i].Visible) nv++;
                        float p = Math.Min(1f, 400f / Math.Max(1f, nv));
                        float mAll = 0f, mDrawn = 0f;
                        for (int i = 0; i < v; i++)
                        {
                            if (!s.Out[i].Visible) continue;
                            mAll = Math.Max(mAll, s.Out[i].Pos.Y);
                            if (s.Out[i].Prio < p) mDrawn = Math.Max(mDrawn, s.Out[i].Pos.Y);
                        }
                        all = Math.Max(all, mAll); drawn = Math.Max(drawn, mDrawn); worst = Math.Min(worst, mDrawn); cols++;
                    }
                }
                _out.WriteLine($"n={n}: engulf 1 highest visible place {all:0.00} m (all), {drawn:0.00} m (under the draw threshold), lowest per-frame maximum {worst:0.00} m over {cols} frames");
            }
        }

        [Fact]
        public void EngulfingOnlyForcesItsOwnWormsOutOfTheCache()
        {
            var s = new HeapSlots(31u);
            var P = Params(200, 2f);
            P.Engulf = 0.4f; P.TargetDist = 3f; P.TargetTurns = 0.1f;     // part of the places recruited (at 1 the layout recruits them all)
            P.PseudoRamp = 0.5f;
            for (int f = 0; f < 8; f++) MassLayout.Evaluate(P, s, 1f, f, 4, 2);       // warm the cache
            int v = MassLayout.VisibleCount(200, s.Count);
            int recruited = 0, recruitedRefreshed = 0, others = 0, othersRefreshed = 0;
            MassLayout.Evaluate(P, s, 1f, 8, 4, 2);
            float engThr = (MassLayoutConst.ColFrac + MassLayoutConst.BaseFrac) * 0.4f + MassLayoutConst.RecruitSoft;
            float armThr = MassLayoutConst.ArmFrac * 0.5f + MassLayoutConst.RecruitSoft;
            for (int i = 0; i < v; i++)
            {
                bool forced = s.Thr[i] < engThr || s.Thr2[i] < armThr;
                bool refreshed = s.EvalFrame[i] == 8;
                if (forced) { recruited++; if (refreshed) recruitedRefreshed++; }
                else { others++; if (refreshed) othersRefreshed++; }
            }
            _out.WriteLine($"engulf + arm: {recruited} forced places (refreshed {recruitedRefreshed}), {others} others (refreshed {othersRefreshed})");
            Assert.Equal(recruited, recruitedRefreshed);
            Assert.True(others > 0 && othersRefreshed < others * 0.6f);
        }
    }
}
