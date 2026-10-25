using System;
using System.Collections.Generic;
using MistWorms.Mass;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>Movement criteria of the plan (pass E) measured out of game on the layout itself: border bias, treadmill ratio, poured worms.</summary>
    public class MassMotionTests
    {
        private readonly ITestOutputHelper _out;
        public MassMotionTests(ITestOutputHelper output) { _out = output; }

        private const float V = 2.4f;
        private const float Dt = 1f / 60f;

        private static MassFrameParams Params(int n, float r, float sigma, float t, float tread, float boil, float odoF, float odoB)
        {
            var P = MassFrameParams.Default(r, n);
            P.Sigma = sigma; P.T = t; P.Tread = tread; P.Boil = boil; P.OdoFlux = odoF; P.OdoBorder = odoB;
            return P;
        }

        /// <summary>Counts of border worms in the front sector (+-60 degrees) and the rear one (beyond 120 degrees), over a walk at the speed ratio sigma.</summary>
        private static void BorderCounts(int n, float sigma, out int front, out int rear)
        {
            float r = MistWorms.Render.MassMath.Radius(n, 0.8f, 0.55f);
            var s = new HeapSlots(4242u);
            front = rear = 0;
            float tread = 0f, boil = 0f, odoF = 0f, odoB = 0f;
            for (int f = 0; f < 600; f++)
            {
                float v = V * sigma;
                float lt = MassLayoutConst.TreadPerimeter * MassLayout.RPlace(n, r) * (1f + MassLayoutConst.TreadStretch * sigma) / MassLayoutConst.TreadSurfaceFrac;
                tread += v * Dt / lt;
                MassLayout.AdvanceLobeTread(s, v, Dt);
                boil += (1f - sigma) * Dt / MassLayoutConst.BoilTref;
                odoF += (v + MassLayoutConst.BoilSpeed * (1f - sigma)) * Dt * MassLayoutConst.CrawlFramesPerM;
                odoB += (v + MassLayoutConst.BorderSpeed * (1f - sigma)) * Dt * MassLayoutConst.CrawlFramesPerM;
                var P = Params(n, r, sigma, f * Dt, tread, boil, odoF, odoB);
                int count = MassLayout.Evaluate(P, s);
                if (f < 120) continue;
                for (int i = 0; i < count; i++)
                {
                    SlotOut o = s.Out[i];
                    if (!o.Visible || o.Role != MassLayout.RoleBorder) continue;
                    float len = (float)Math.Sqrt(o.Pos.X * o.Pos.X + o.Pos.Z * o.Pos.Z);
                    if (len < 0.1f) continue;
                    float c = o.Pos.X / len;
                    if (c > 0.5f) front++; else if (c < -0.5f) rear++;
                }
            }
        }

        [Fact]
        public void BorderWormsAreAtLeastTwiceAsManyInFrontAsBehindWhileWalking()
        {
            foreach (int n in new[] { 50, 200 })
                foreach (float sigma in new[] { 0.7f, 0.85f, 1f })
                {
                    BorderCounts(n, sigma, out int front, out int rear);
                    float ratio = front / (float)Math.Max(1, rear);
                    _out.WriteLine($"n={n} sigma={sigma:0.00}: border front {front} rear {rear} ratio {ratio:0.00}");
                    if (sigma >= 0.85f) Assert.True(ratio >= 2f, $"n={n} sigma={sigma}: ratio {ratio:0.00}");
                }
        }

        /// <summary>
        /// A heap walking at V with the render dividers (bodies 1/6, flux and border 1/2): the worms poured on the ground (idle flux) hold still in the world,
        /// speeds taken over 0.2 s like the in-game measure; the top worms move faster than the heap.
        /// </summary>
        [Fact]
        public void PouredWormsHoldStillAndTheTopRunsAheadWithTheRenderDividers()
        {
            foreach (int n in new[] { 50, 200 })
            {
                float r = MistWorms.Render.MassMath.Radius(n, 0.8f, 0.55f);
                var s = new HeapSlots(777u);
                const float sigma = 1f;
                float tread = 0f, boil = 0f, odoF = 0f, odoB = 0f, rootX = 0f;
                var ring = new List<float[]>();
                var ringOk = new List<byte[]>();       // 0 none, 1 idle flux, 2 top crawl flux
                var poured = new List<float>();
                var top = new List<float>();
                const int Lag = 12;
                for (int f = 0; f < 480; f++)
                {
                    float v = V * sigma;
                    rootX += v * Dt;
                    float lt = MassLayoutConst.TreadPerimeter * MassLayout.RPlace(n, r) * (1f + MassLayoutConst.TreadStretch * sigma) / MassLayoutConst.TreadSurfaceFrac;
                    tread += v * Dt / lt;
                    MassLayout.AdvanceLobeTread(s, v, Dt);
                    odoF += v * Dt * MassLayoutConst.CrawlFramesPerM;
                    odoB += v * Dt * MassLayoutConst.CrawlFramesPerM;
                    var P = Params(n, r, sigma, f * Dt, tread, boil, odoF, odoB);
                    int count = MassLayout.Evaluate(P, s, 1f, f, 6, 2);
                    var xs = new float[count]; var kind = new byte[count];
                    for (int i = 0; i < count; i++)
                    {
                        SlotOut o = s.Out[i];
                        xs[i] = rootX + o.Pos.X;       // the heap faces +X: the world position along its walk
                        if (o.Visible && o.Role == MassLayout.RoleFlux && o.Anim == MassLayout.AnimIdle) kind[i] = 1;
                        else if (o.Visible && o.Role == MassLayout.RoleFlux && o.Anim == MassLayout.AnimCrawl && o.Pos.Y > 0.25f) kind[i] = 2;
                    }
                    if (f >= 120 && ring.Count >= Lag)
                    {
                        float[] oldX = ring[ring.Count - Lag];
                        byte[] oldK = ringOk[ringOk.Count - Lag];
                        for (int i = 0; i < count && i < oldX.Length; i++)
                        {
                            if (kind[i] == 0 || kind[i] != oldK[i]) continue;
                            float sp = Math.Abs(xs[i] - oldX[i]) / (Lag * Dt);
                            if (sp > 4f * V) continue;
                            if (kind[i] == 1) poured.Add(sp); else top.Add(sp);
                        }
                    }
                    ring.Add(xs); ringOk.Add(kind);
                }
                poured.Sort(); top.Sort();
                float pm = poured.Count > 0 ? poured[poured.Count / 2] : -1f, tm = top.Count > 0 ? top[top.Count / 2] : -1f;
                _out.WriteLine($"n={n}: poured median {pm:0.00} m/s over {poured.Count} samples, top flux median {tm:0.00} m/s (ratio {tm / V:0.00}) over {top.Count}");
                Assert.True(poured.Count >= 20, "poured samples " + poured.Count);
                Assert.True(pm <= 0.25f * V, $"n={n}: poured worms move at {pm:0.00} m/s");
                Assert.True(tm / V >= 1.6f && tm / V <= 2.4f, $"n={n}: tread ratio {tm / V:0.00}");
            }
        }

        [Fact]
        public void NoWormLiesBeyondTheHalfWidthOfTheRibbon()
        {
            foreach (int n in new[] { 50, 200 })
            {
                float r = MistWorms.Render.MassMath.Radius(n, 0.8f, 0.55f);
                var s = new HeapSlots(31u);
                float w = 1.9f;
                float worst = 0f;
                for (int f = 0; f < 240; f++)
                {
                    var P = Params(n, r, 1f, f * Dt, f * Dt * 0.1f, 0f, f * 0.5f, f * 0.2f);
                    P.StreamK = 1f; P.RibbonW = w; P.RibbonL = 6f;
                    P.TargetDist = 4f; P.TargetTurns = 0.02f; P.PseudoRamp = 1f;
                    int count = MassLayout.Evaluate(P, s);
                    for (int i = 0; i < count; i++) worst = Math.Max(worst, Math.Abs(s.Out[i].Pos.Z));
                }
                _out.WriteLine($"n={n}: largest |Z| {worst:0.000} m, half width {w * 0.5f:0.000} m");
                Assert.True(worst <= w * 0.5f - 0.08f + 1e-4f, $"n={n}: |Z| {worst}");
            }
        }
    }
}
