using System;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Mass;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>Properties of the heap layout (plan 4B-3, pass C): roles, determinism, finiteness, bounds, continuity, treadmill, allocation.</summary>
    public class MassLayoutTests
    {
        private readonly ITestOutputHelper _out;
        public MassLayoutTests(ITestOutputHelper output) { _out = output; }

        private const float V = 2.4f;   // Worm.MoveSpeed

        private static float RadiusOf(int n) { return 0.83f * (float)Math.Pow(n / 10.0, 0.36); }

        private struct CountingSink : IHeapSink
        {
            public int Worms;
            public float Acc;
            public void AddWorm(in SlotOut o) { Worms++; Acc += o.Pos.X; }
        }

        private static bool Finite(float f) { return !float.IsNaN(f) && !float.IsInfinity(f); }
        private static bool Finite(V3 v) { return Finite(v.X) && Finite(v.Y) && Finite(v.Z); }

        [Fact]
        public void RoleProportionsHoldForEveryPrefix()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var s = new HeapSlots(seed * 7919u);
                foreach (int k in new[] { 20, 60, 120, 200, 512 })
                {
                    int body = 0, flux = 0, border = 0;
                    for (int i = 0; i < k; i++)
                    {
                        if (s.Role[i] == MassLayout.RoleBody) body++;
                        else if (s.Role[i] == MassLayout.RoleFlux) flux++;
                        else border++;
                    }
                    Assert.InRange((double)body / k, 0.60 - 0.05, 0.60 + 0.05);
                    Assert.InRange((double)flux / k, 0.30 - 0.05, 0.30 + 0.05);
                    Assert.InRange((double)border / k, 0.10 - 0.05, 0.10 + 0.05);
                }
            }
        }

        [Fact]
        public void VisibleCountFollowsTheDensityLaw()
        {
            Assert.Equal(60, MassLayout.VisibleCount(10, 768));     // k(10) = 6
            Assert.InRange(MassLayout.VisibleCount(50, 768), 270, 285);
            Assert.Equal(768, MassLayout.VisibleCount(200, 768));   // k(200) = 3.84
            Assert.Equal(768, MassLayout.VisibleCount(300, 768));
        }

        [Fact]
        public void SameSeedSameResultOtherSeedOtherResult()
        {
            var a = new HeapSlots(12345u);
            var b = new HeapSlots(12345u);
            var c = new HeapSlots(54321u);
            var P = MassFrameParams.Default(RadiusOf(50), 50);
            P.T = 3.3f; P.Sigma = 0.4f; P.Tread = 1.2f; P.Boil = 2.2f;
            int n = MassLayout.Evaluate(P, a);
            MassLayout.Evaluate(P, b);
            MassLayout.Evaluate(P, c);
            int diff = 0;
            for (int i = 0; i < n; i++)
            {
                Matrix4x4 ma, mb, mc;
                MassLayout.Compose(a.Out[i], out ma);
                MassLayout.Compose(b.Out[i], out mb);
                MassLayout.Compose(c.Out[i], out mc);
                Assert.Equal(ma, mb);
                if (!ma.Equals(mc)) diff++;
            }
            Assert.True(diff > n / 2);
        }

        [Fact]
        public void NothingIsNaNOrInfinite()
        {
            var s = new HeapSlots(777u);
            foreach (int n in new[] { 1, 2, 3, 5, 6, 8, 10, 15, 16, 25, 40, 77, 120, 199, 200 })
                foreach (float sigma in new[] { 0f, 0.5f, 1f })
                    foreach (float streamK in new[] { 0f, 0.5f, 1f })
                        foreach (float t in new[] { 0f, 7.1f, 119.9f })
                        {
                            var P = MassFrameParams.Default(RadiusOf(n), n);
                            P.Sigma = sigma; P.StreamK = streamK; P.T = t; P.RibbonW = 2.2f; P.RibbonL = 5f;
                            P.Tread = 3.7f + t; P.Boil = 11.1f; P.OdoFlux = 123.4f; P.OdoBorder = 55.5f;
                            P.TargetDist = 3f; P.TargetTurns = 0.3f; P.PseudoRamp = 1f; P.Engulf = (n % 3) * 0.5f; P.HasCam = (n % 2 == 0); P.CamL = new V3(0.3f, 0.5f, 0.8f);
                            int count = MassLayout.Evaluate(P, s);
                            for (int i = 0; i < count; i++)
                            {
                                SlotOut o = s.Out[i];
                                Assert.True(Finite(o.Pos) && Finite(o.Fwd) && Finite(o.Up) && Finite(o.Scale), "n=" + n + " slot " + i);
                                Assert.InRange(o.Frame, 0, 15);
                            }
                            for (int j = 0; j < MassLayoutConst.LobeMax; j++)
                            {
                                Lobe L = s.Lobes[j];
                                Assert.True(Finite(L.Cx) && Finite(L.Cz) && Finite(L.Rx) && Finite(L.Ry) && Finite(L.Rz) && Finite(L.Psi) && Finite(L.Lt), "lobe " + j);
                                Assert.True(L.Rx > 0f && L.Ry > 0f && L.Rz > 0f);
                            }
                        }
        }

        [Fact]
        public void PolesAndRimAreFinite()
        {
            // the pole (X = Z = 0) and the rim (rho -> 1) come from the slot placement and the lane ends: sweep many seeds
            for (uint seed = 1; seed <= 200; seed++)
            {
                var s = new HeapSlots(seed);
                var P = MassFrameParams.Default(RadiusOf(200), 200);
                P.Sigma = (seed % 3) * 0.5f; P.T = seed * 0.37f;
                int count = MassLayout.Evaluate(P, s);
                for (int i = 0; i < count; i++) Assert.True(Finite(s.Out[i].Pos) && Finite(s.Out[i].Fwd) && Finite(s.Out[i].Up));
            }
        }

        [Fact]
        public void LobesStayInsideTheFootprint()
        {
            var s = new HeapSlots(31u);
            foreach (int n in new[] { 10, 50, 200 })
                foreach (float sigma in new[] { 0f, 1f })
                    for (float t = 0f; t < 40f; t += 0.9f)
                    {
                        var P = MassFrameParams.Default(RadiusOf(n), n);
                        P.Sigma = sigma; P.T = t; P.Tread = t * 0.4f;
                        MassLayout.Evaluate(P, s);
                        float rp = MassLayout.RPlace(n, P.R);
                        for (int j = 0; j < MassLayoutConst.LobeMax; j++)
                        {
                            Lobe L = s.Lobes[j];
                            float ext = (float)Math.Sqrt(L.Cx * L.Cx + L.Cz * L.Cz) + Math.Max(L.Rx, L.Rz);
                            Assert.True(ext <= rp * (1.6f + 0.4f * sigma), "n=" + n + " lobe " + j + " reaches " + ext + " for r " + rp);
                            Assert.InRange(L.Ry, 0.02f, 1.6f * MassLayoutConst.HeightFrac * rp);
                        }
                    }
        }

        [Fact]
        public void TheInnerHalfOfTheLobesIsSparse()
        {
            // "no solid mass in the middle": the places deep inside a lobe (normalised radius < 0.45) stay a small fraction
            var s = new HeapSlots(4242u);
            foreach (int n in new[] { 50, 200 })
            {
                int deep = 0, total = 0;
                for (float t = 0f; t < 24f; t += 3.1f)
                {
                    var P = MassFrameParams.Default(RadiusOf(n), n);
                    P.T = t;
                    int count = MassLayout.Evaluate(P, s);
                    for (int i = 0; i < count; i++)
                    {
                        SlotOut o = s.Out[i];
                        if (o.Role != MassLayout.RoleBody) continue;
                        Lobe L = s.Lobes[o.Lobe];
                        float x = o.Pos.X - L.Cx, z = o.Pos.Z - L.Cz;
                        float lx = L.C * x + L.S * z, lz = -L.S * x + L.C * z;      // back to the lobe frame
                        float rad = (float)Math.Sqrt((lx / L.Rx) * (lx / L.Rx) + (o.Pos.Y / L.Ry) * (o.Pos.Y / L.Ry) + (lz / L.Rz) * (lz / L.Rz));
                        total++;
                        if (rad < 0.45f) deep++;
                    }
                }
                _out.WriteLine("n=" + n + ": " + (100.0 * deep / total).ToString("0.0") + " % of the bodies deeper than 0.45 of their lobe");
                Assert.True((double)deep / total < 0.45, "deep fraction " + (double)deep / total);
            }
        }

        [Fact]
        public void EngulfKeepsAHeapAtTheFeetAndASheathAroundTheTarget()
        {
            var s = new HeapSlots(77u);
            foreach (int n in new[] { 50, 200 })
            {
                var P = MassFrameParams.Default(RadiusOf(n), n);
                P.TargetDist = 3f; P.TargetTurns = 0.1f; P.T = 5f; P.Engulf = 1f;
                V3 tgt = P.TargetLocal();
                int count = MassLayout.Evaluate(P, s);
                int near = 0, foot = 0, sheath = 0, beside = 0;
                float maxH = 0f, maxSheathR = 0f;
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    float dx = o.Pos.X - tgt.X, dz = o.Pos.Z - tgt.Z;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d < 1.5f) near++;
                    if (d < 1.3f && o.Pos.Y < 0.6f) foot++;
                    if (o.Pos.Y > 0.9f && d < 1.5f)
                    {
                        sheath++;
                        if (d > maxSheathR) maxSheathR = d;
                        if (o.Pos.Y > maxH) maxH = o.Pos.Y;
                    }
                    if (d > 2.5f) beside++;
                    Assert.True(o.Pos.Y <= MassLayoutConst.EngulfHeight * (1f + MassLayoutConst.EngulfTopWobble) + 0.1f, "height " + o.Pos.Y);
                }
                _out.WriteLine("n=" + n + ": " + near + "/" + count + " within 1.5 m of the target, " + foot + " at the feet, " + sheath + " above 0.9 m (radius up to "
                               + maxSheathR.ToString("0.00") + ", top " + maxH.ToString("0.00") + "), " + beside + " still beside");
                Assert.True(foot >= (int)(0.35 * count), "feet " + foot + " of " + count);          // at least 40 % stay in a heap at the feet (a few are beside or climbing)
                Assert.True(sheath >= 10 && maxH > 1.2f, "sheath " + sheath);
                float rmax = (MassLayoutConst.EngulfRBottom * (1f + MassLayoutConst.EngulfJitter)) + MassLayoutConst.EngulfBulgeAmp + MassLayoutConst.EngulfOutReach + 0.05f;
                Assert.True(maxSheathR <= rmax, "sheath radius " + maxSheathR + " > " + rmax);
                Assert.True(beside >= (int)(0.05 * count), "the rest of the heap stays beside: " + beside);
            }
            // no engulf: nobody on the target
            var Q = MassFrameParams.Default(RadiusOf(50), 50);
            Q.TargetDist = 6f; Q.TargetTurns = 0f; Q.T = 5f;
            V3 t2 = Q.TargetLocal();
            int cnt2 = MassLayout.Evaluate(Q, s);
            for (int i = 0; i < cnt2; i++)
            {
                float dx = s.Out[i].Pos.X - t2.X, dz = s.Out[i].Pos.Z - t2.Z;
                Assert.True(dx * dx + dz * dz > 0.5f * 0.5f);
            }
        }

        [Fact]
        public void SheathWormsAreAlwaysDrawnAndTheBaseLiesDownTangentially()
        {
            var s = new HeapSlots(77u);
            foreach (int n in new[] { 50, 200 })
            {
                var P = MassFrameParams.Default(RadiusOf(n), n);
                P.TargetDist = 3f; P.TargetTurns = 0.1f; P.T = 5f; P.Engulf = 1f;
                V3 tgt = P.TargetLocal();
                int count = MassLayout.Evaluate(P, s);
                int sheath = 0, baseN = 0, radial = 0;
                double sum = 0, sumY = 0;
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    float dx = o.Pos.X - tgt.X, dz = o.Pos.Z - tgt.Z;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (o.Pos.Y > 0.9f && d < 1.5f) { sheath++; Assert.Equal(0f, o.Prio); }
                    if (o.Role == MassLayout.RoleBody && o.Lobe == MassLayoutConst.EngulfLobe && d > 0.2f && d < 1.2f && o.Pos.Y < 0.3f)
                    {
                        float dot = Math.Abs((o.Fwd.X * dx + o.Fwd.Z * dz) / d);
                        sum += dot; baseN++;
                        if (dot > 0.8f) radial++;
                        sumY += Math.Abs(o.Fwd.Y);
                    }
                }
                _out.WriteLine("n=" + n + ": " + sheath + " sheath worms with priority 0; base: " + baseN + " worms, mean |radial| " + (sum / Math.Max(1, baseN)).ToString("0.00") + ", " + radial + " radial");
                Assert.True(sheath >= 10);
                Assert.True(baseN >= 5);
                Assert.True(sum / baseN < 0.5, "petals: mean radial component " + sum / baseN);
                Assert.True(sumY / baseN < 0.35, "base worms not lying down: mean |pitch component| " + sumY / baseN);
                Assert.True(radial <= 0.3 * baseN, "radial " + radial + " of " + baseN);
            }
        }

        [Fact]
        public void PseudopodIsAnArmOfWormsTowardTheTarget()
        {
            var s = new HeapSlots(19u);
            foreach (int n in new[] { 50, 200 })
            {
                var P = MassFrameParams.Default(RadiusOf(n), n);
                float dist = P.R + 2.2f;
                P.TargetDist = dist; P.TargetTurns = 0.25f; P.PseudoRamp = 1f; P.T = 3f;      // the target is on +Z
                int count = MassLayout.Evaluate(P, s);
                int inArm = 0;
                float tip = 0f, maxWidth = 0f;
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    if (o.Pos.Z > P.R + 0.2f && Math.Abs(o.Pos.X) < 0.5f && o.Pos.Y < 0.7f)
                    {
                        inArm++;
                        if (o.Pos.Z > tip) tip = o.Pos.Z;
                        if (Math.Abs(o.Pos.X) > maxWidth) maxWidth = Math.Abs(o.Pos.X);
                    }
                }
                _out.WriteLine("n=" + n + ": " + inArm + " worms in the arm beyond the rim, tip at " + tip.ToString("0.00") + " m (target " + dist.ToString("0.00") + "), half width " + maxWidth.ToString("0.00"));
                Assert.True(inArm >= 8, "arm worms " + inArm);
                Assert.True(tip <= dist - 0.2f, "tip " + tip + " target " + dist);
                var P0 = P; P0.PseudoRamp = 0f;
                MassLayout.Evaluate(P0, s);
                int none = 0;
                for (int i = 0; i < count; i++) if (s.Out[i].Pos.Z > P.R + 0.2f && Math.Abs(s.Out[i].Pos.X) < 0.5f && s.Out[i].Pos.Y < 0.7f) none++;
                Assert.True(none < inArm, "without the arm: " + none);
            }
        }

        [Fact]
        public void RibbonKeepsFluxAndBodiesInsideTheCorridor()
        {
            var s = new HeapSlots(99u);
            const float w = 2.0f;
            for (float t = 0f; t < 10f; t += 0.9f)
            {
                var P = MassFrameParams.Default(RadiusOf(100), 100);
                P.StreamK = 1f; P.RibbonW = w; P.RibbonL = 6f; P.Sigma = 1f; P.T = t; P.Tread = t * 0.4f; P.Boil = 0f;
                int count = MassLayout.Evaluate(P, s);
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    if (o.Role == MassLayout.RoleBorder) continue;
                    Assert.True(Math.Abs(o.Pos.Z) <= w * 0.5f + 0.1f, "role " + o.Role + " z " + o.Pos.Z);
                }
            }
        }

        /// <summary>Runs the caller's integrators (tread, boil, odometers) the way WritheMassVisual will, and calls back every frame.</summary>
        private static void Simulate(HeapSlots s, int n, float seconds, Func<float, float> sigmaOf, Func<float, float> radiusOf, Action<float, MassFrameParams, int> onFrame, float yawRate = 0f, Func<float, float[]> frameOf = null)
        {
            const float dt = 1f / 60f;
            float tread = 0.2f, boil = 0.3f, odoF = 0f, odoB = 0f;
            for (int f = 0; f * dt < seconds; f++)
            {
                float time = f * dt;
                float sigma = sigmaOf(time), r = radiusOf(time);
                float rp = MassLayout.RPlace(n, r);
                float v = V * sigma;
                float lt = MassLayoutConst.TreadPerimeter * rp * (1f + MassLayoutConst.TreadStretch * sigma) / MassLayoutConst.TreadSurfaceFrac;
                tread += v * dt / lt;
                MassLayout.AdvanceLobeTread(s, v, dt);
                boil += (1f - sigma) * dt / MassLayoutConst.BoilTref;
                odoF += (v + MassLayoutConst.BoilSpeed * (1f - sigma)) * dt * MassLayoutConst.CrawlFramesPerM;
                odoB += (v + MassLayoutConst.BorderSpeed * (1f - sigma)) * dt * MassLayoutConst.CrawlFramesPerM;
                var P = MassFrameParams.Default(r, n);
                P.Sigma = sigma; P.T = time % 120f; P.Tread = tread; P.Boil = boil; P.OdoFlux = odoF; P.OdoBorder = odoB;
                P.YawRate = yawRate; P.Speed = v;
                if (frameOf != null)
                {
                    float[] fr = frameOf(time);          // root x, root z, heading (radians; left of the forward is +90 degrees)
                    P.HasFrame = true; P.RootX = fr[0]; P.RootZ = fr[1];
                    P.FwdX = (float)Math.Cos(fr[2]); P.FwdZ = (float)Math.Sin(fr[2]); P.LeftX = -P.FwdZ; P.LeftZ = P.FwdX;
                    P.FrameYaw = fr[2] / (2f * (float)Math.PI);
                }
                int count = MassLayout.Evaluate(P, s);
                onFrame(time, P, count);
            }
        }

        [Fact]
        public void NoVisibleWormJumpsBetweenTwoFramesWhileSigmaAndRadiusRamp()
        {
            foreach (int n in new[] { 50, 200 })
            {
                var s = new HeapSlots(2024u);
                var prev = new V3[MassLayoutConst.SlotCount];
                var prevVis = new bool[MassLayoutConst.SlotCount];
                var prevFrame = -1f;
                float worst = 0;
                Simulate(s, n, 3f, t => Math.Min(1f, Math.Max(0f, t - 0.5f)), t => RadiusOf(n) * (0.8f + 0.2f * Math.Min(1f, t / 2f)),
                    (time, P, count) =>
                    {
                        for (int i = 0; i < count; i++)
                        {
                            SlotOut o = s.Out[i];
                            if (prevFrame >= 0f && o.Visible && prevVis[i])
                            {
                                float dx = o.Pos.X - prev[i].X, dy = o.Pos.Y - prev[i].Y, dz = o.Pos.Z - prev[i].Z;
                                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                                if (d > worst) worst = d;
                                Assert.True(d <= 0.25f, "n=" + n + " t=" + time + " slot " + i + " role " + o.Role + " jumped " + d);
                            }
                            prev[i] = o.Pos; prevVis[i] = o.Visible;
                        }
                        prevFrame = time;
                    });
                _out.WriteLine("n=" + n + ": largest visible per-frame displacement " + worst.ToString("0.000") + " m");
            }
        }

        [Fact]
        public void PouredWormsAreMotionlessInTheWorld()
        {
            var s = new HeapSlots(8u);
            var prevX = new float[MassLayoutConst.SlotCount];
            var streak = new int[MassLayoutConst.SlotCount];     // consecutive frames with the pour signature (a lone frame can match by coincidence while the pour weight ramps in)
            int measured = 0;
            float worst = 0;
            Simulate(s, 50, 6f, t => 1f, t => RadiusOf(50), (time, P, count) =>
            {
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    bool pour = o.Role == MassLayout.RoleFlux && o.Visible && Math.Abs(o.Pos.Y - MassLayoutConst.FluxLift) < 1e-4f;
                    if (time > 1f && pour && streak[i] >= 3)
                    {
                        float lx = o.Pos.X - s.Lobes[o.Lobe].Sx;                  // slow centre of the lobe (psi = 0 on the move; pour worms are flux, Lobe >= 0): the drift and the swing are not followed
                        float worldV = (lx - prevX[i]) * 60f + V;                 // the lobe moves at V along +X
                        if (Math.Abs(worldV) > worst) worst = Math.Abs(worldV);
                        measured++;
                        Assert.True(Math.Abs(worldV) < 0.05f, "slot " + i + " t=" + time + " x " + prevX[i] + " -> " + o.Pos.X + " world speed " + worldV);
                    }
                    prevX[i] = o.Pos.X - (o.Lobe >= 0 ? s.Lobes[o.Lobe].Sx : 0f); streak[i] = pour ? streak[i] + 1 : 0;
                }
            });
            Assert.True(measured > 100, "poured worms measured: " + measured);
            _out.WriteLine("poured worms: " + measured + " samples, worst world speed " + worst.ToString("0.0000") + " m/s");
        }

        /// <summary>A heap that turns (0.15 turns/s, a turn of 54 degrees per second) with the poured worms turned back (YawRate given) or not: median world speed of the poured worms.</summary>
        private float PouredWorldSpeedWhileTurning(float turnRate, float givenRate)
        {
            var s = new HeapSlots(8u);
            var prevW = new V3[MassLayoutConst.SlotCount];
            var streak = new int[MassLayoutConst.SlotCount];
            var speeds = new List<float>();
            float cx = 0f, cz = 0f;       // root in the world, moving at V along the heading
            Simulate(s, 50, 6f, t => 1f, t => RadiusOf(50), (time, P, count) =>
            {
                float psi = turnRate * time * 2f * (float)Math.PI;
                cx += V * (float)Math.Cos(psi) / 60f; cz += V * (float)Math.Sin(psi) / 60f;
                float c = (float)Math.Cos(psi), sn = (float)Math.Sin(psi);
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    bool pour = o.Role == MassLayout.RoleFlux && o.Visible && o.Anim == MassLayout.AnimIdle && Math.Abs(o.Pos.Y - MassLayoutConst.FluxLift) < 1e-4f;
                    var w = new V3(cx + c * o.Pos.X - sn * o.Pos.Z, 0f, cz + sn * o.Pos.X + c * o.Pos.Z);
                    if (time > 1f && pour && streak[i] >= 3)
                    {
                        float dx = w.X - prevW[i].X, dz = w.Z - prevW[i].Z;
                        speeds.Add((float)Math.Sqrt(dx * dx + dz * dz) * 60f);
                    }
                    prevW[i] = w; streak[i] = pour ? streak[i] + 1 : 0;
                }
            }, givenRate);
            Assert.True(speeds.Count > 50, "poured samples: " + speeds.Count);
            speeds.Sort();
            return speeds[speeds.Count / 2];
        }

        /// <summary>The poured worms anchored in the world (the world frame is given): median and 95th percentile of their world speed on a path that turns.</summary>
        private void AnchoredPouredSpeeds(float turnRate, out float median, out float p95)
        {
            var s = new HeapSlots(8u);
            var prevW = new V3[MassLayoutConst.SlotCount];
            var streak = new int[MassLayoutConst.SlotCount];
            var speeds = new List<float>();
            float w = turnRate * 2f * (float)Math.PI;
            Func<float, float[]> frameOf = time =>
            {
                float psi = w * time;
                float x = w > 1e-5f ? V / w * (float)Math.Sin(psi) : V * time;
                float z = w > 1e-5f ? V / w * (1f - (float)Math.Cos(psi)) : 0f;
                return new[] { x, z, psi };
            };
            Simulate(s, 50, 6f, t => 1f, t => RadiusOf(50), (time, P, count) =>
            {
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    bool pour = o.Role == MassLayout.RoleFlux && o.Visible && o.Anim == MassLayout.AnimIdle && Math.Abs(o.Pos.Y - MassLayoutConst.FluxLift) < 1e-4f;
                    var wp = new V3(P.RootX + P.FwdX * o.Pos.X + P.LeftX * o.Pos.Z, 0f, P.RootZ + P.FwdZ * o.Pos.X + P.LeftZ * o.Pos.Z);
                    if (time > 1f && pour && streak[i] >= 3)
                    {
                        float dx = wp.X - prevW[i].X, dz = wp.Z - prevW[i].Z;
                        speeds.Add((float)Math.Sqrt(dx * dx + dz * dz) * 60f);
                    }
                    prevW[i] = wp; streak[i] = pour ? streak[i] + 1 : 0;
                }
            }, 0f, frameOf);
            Assert.True(speeds.Count > 50, "poured samples: " + speeds.Count);
            speeds.Sort();
            median = speeds[speeds.Count / 2];
            p95 = speeds[(int)(speeds.Count * 0.95f)];
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.05f)]
        [InlineData(0.15f)]
        public void PouredWormsAnchoredInTheWorldLieStillStraightOrTurning(float turnRate)
        {
            AnchoredPouredSpeeds(turnRate, out float median, out float p95);
            _out.WriteLine("turn " + turnRate + " turns/s: poured worms world speed median " + median.ToString("0.0000") + " m/s, 95th percentile " + p95.ToString("0.000") + " m/s");
            Assert.True(median < 0.02f, "median " + median);
            Assert.True(p95 < 0.15f, "95th percentile " + p95);
        }

        [Fact]
        public void PouredWormsHoldStillWhileTheHeapTurns()
        {
            float without = PouredWorldSpeedWhileTurning(0.15f, 0f);
            float with = PouredWorldSpeedWhileTurning(0.15f, 0.15f);
            _out.WriteLine("median world speed of the poured worms while turning 54 deg/s: " + without.ToString("0.000") + " m/s without the correction, " + with.ToString("0.000") + " m/s with it");
            Assert.True(with < 0.5f * without, "the correction must at least halve the drift: " + without + " -> " + with);
            Assert.True(with < 0.3f, "median world speed with the correction: " + with);
        }

        [Fact]
        public void TreadmillMovesTheTopWormsAtAboutTwiceTheHeapSpeed()
        {
            var s = new HeapSlots(8u);
            foreach (int n in new[] { 50, 200 })
            {
                var prev = new V3[MassLayoutConst.SlotCount];
                var prevTop = new bool[MassLayoutConst.SlotCount];
                double sum = 0; int cnt = 0;
                float r = RadiusOf(n), h = 0.8f * r;
                Simulate(s, n, 8f, t => 1f, t => r, (time, P, count) =>
                {
                    for (int i = 0; i < count; i++)
                    {
                        SlotOut o = s.Out[i];
                        bool top = o.Role == MassLayout.RoleFlux && o.Visible && o.Lobe >= 0 && o.Pos.Y > 0.45f * s.Lobes[o.Lobe].Ry;
                        if (time > 1f && top && prevTop[i])
                        {
                            double dx = ((o.Pos.X - s.Lobes[o.Lobe].Cx) - prev[i].X) * 60.0;
                            sum += dx + V; cnt++;
                        }
                        prev[i] = new V3(o.Pos.X - (o.Lobe >= 0 ? s.Lobes[o.Lobe].Cx : 0f), 0f, 0f); prevTop[i] = top;
                    }
                });
                Assert.True(cnt > 200);
                double ratio = sum / cnt / V;
                _out.WriteLine("n=" + n + ": mean world speed of the top flux worms / v = " + ratio.ToString("0.00"));
                Assert.InRange(ratio, 1.6, 2.4);
            }
        }

        [Fact]
        public void ComposeBuildsAnOrthonormalFrameScaledByTheWormScale()
        {
            var s = new HeapSlots(5u);
            var P = MassFrameParams.Default(RadiusOf(50), 50);
            P.T = 4.4f; P.Sigma = 0.6f; P.Tread = 0.5f;
            int count = MassLayout.Evaluate(P, s);
            Matrix4x4 frame = MassLayout.MakeFrame(new Vector3(10f, 2f, -3f), new Vector3(0f, 0f, 1f), new Vector3(0f, 1f, 0f));
            for (int i = 0; i < count; i++)
            {
                Matrix4x4 m;
                MassLayout.ComposeWorld(frame, s.Out[i], out m);
                float sc = s.Out[i].Scale;
                Vector3 c0 = new Vector3(m.m00, m.m10, m.m20), c1 = new Vector3(m.m01, m.m11, m.m21), c2 = new Vector3(m.m02, m.m12, m.m22);
                // fwd/up come from the sine table (not exactly unit): 0.5 % is plenty
                Assert.InRange(c0.magnitude / sc, 0.99f, 1.01f);
                Assert.InRange(c1.magnitude / sc, 0.99f, 1.01f);
                Assert.InRange(c2.magnitude / sc, 0.99f, 1.01f);
                Assert.InRange(Vector3.Dot(c0.normalized, c1.normalized), -0.02f, 0.02f);
                Assert.InRange(Vector3.Dot(c1.normalized, c2.normalized), -0.03f, 0.03f);
                // mesh +X (right) must be Unity's right of the worm: cross(up, forward)
                Vector3 right = Vector3.Cross(c1.normalized, c2.normalized);
                Assert.InRange(Vector3.Dot(right, c0.normalized), 0.99f, 1.01f);
            }
            // a heap facing world +Z: local +X (forward) -> world +Z, local +Z (left) -> world -X
            Assert.Equal(1f, frame.m20, 4);
            Assert.Equal(-1f, frame.m02, 4);
        }

        [Fact]
        public void EmitIsAllocationFreeAndMeasuresTheCostPerWorm()
        {
            var s = new HeapSlots(12345u);
            var P = MassFrameParams.Default(RadiusOf(200), 200);
            P.Sigma = 1f; P.TargetDist = 4f; P.PseudoRamp = 1f; P.HasCam = true; P.CamL = new V3(0.5f, 0.5f, 0.7f);
            var sink = new CountingSink();
            for (int w = 0; w < 200; w++) { P.T = w * 0.01f; MassLayout.Emit(ref sink, P, s, 400, -1f); }   // warm-up (JIT)
            // AppDomain monitoring counts every thread: other test classes run in parallel, so take the minimum over several short runs
            AppDomain.MonitoringIsEnabled = true;
            long allocated = long.MaxValue;
            double ns = 0, usPerHeap = 0;
            const int iterations = 2000;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                long before = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
                var sw = Stopwatch.StartNew();
                long slots = 0;
                for (int it = 0; it < iterations; it++)
                {
                    P.T = (it % 1200) * 0.1f;
                    P.Tread += 0.001f; P.Boil += 0.0005f;
                    MassLayout.Emit(ref sink, P, s, 400, -1f);
                    slots += MassLayout.VisibleCount(P.N, s.Count);
                }
                sw.Stop();
                long d = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - before;
                if (d < allocated) { allocated = d; ns = sw.Elapsed.TotalMilliseconds * 1e6 / slots; usPerHeap = sw.Elapsed.TotalMilliseconds / iterations * 1000.0; }
            }
            _out.WriteLine("Emit n=200 (500 worms evaluated): " + usPerHeap.ToString("0.0") + " us per heap, " + ns.ToString("0")
                           + " ns per evaluated worm, allocated " + allocated + " bytes (best of 8 runs of " + iterations + " calls)");
            Assert.True(allocated < 16 * 1024, "allocated " + allocated);
            Assert.True(sink.Worms > 0);
        }
    }
}
