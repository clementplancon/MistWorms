using System;
using MistWorms.Mass;
using MistWorms.Render.Gpu;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>
    /// The pseudopod on the GPU ropes (the net of worms of a mass that stretches toward its target, port of Arm() of MassLayout): the properties of the CPU reference
    /// <see cref="WormSimRef"/>, which WormSim.compute must have too (the rendering is judged in game, scenarios V10 and V11).
    /// </summary>
    public class WormSimPseudopodTests
    {
        private readonly ITestOutputHelper _out;
        public WormSimPseudopodTests(ITestOutputHelper output) { _out = output; }

        private const int K = WormSimConst.Nodes;
        private const float DirX = 0.6f, DirZ = 0.8f;

        private static float RadiusOf(int worms) => (float)Math.Pow(worms * 7.6e-5 / 0.5 / 0.94, 1.0 / 3.0);

        private static WormSimRef Build(int worms, float radius, Vector4 pseudo, uint seed = 5u)
        {
            var sim = new WormSimRef(worms);
            float ext = 2f * (radius + 3.0f), sp = ext / (WormSimConst.HeightN - 1);
            sim.Masses[0] = new GpuMassParams
            {
                Origin = new Vector4(0f, 0f, 0f, 1f),
                Center = new Vector4(0f, 0f, 0f, radius),
                Heading = new Vector4(0f, 0f, 1f, 0f),
                Shape = new Vector4(radius, Math.Max(0.45f * radius, 0.05f), radius, 0f),
                Motion = new Vector4(0.8f, 1f, 0.02f, 0f),
                Sleeve = new Vector4(0f, 0.04f, 0f, 0.6f),
                FirstWorm = 0, WormCount = (uint)worms, PrimCount = 0, MassIndex = 0,
                GridRect = new Vector4(-ext * 0.5f, -ext * 0.5f, sp, 1f / sp),
                Pseudo = pseudo,
            };
            for (int i = 0; i < worms; i++) sim.Worms[i] = WormSimRef.MakeWorm(0u, WormSimRef.Hash(unchecked(seed * 0x9E3779B1u + (uint)i)));
            return sim;
        }

        private static void SetPseudo(WormSimRef sim, Vector4 pseudo)
        {
            GpuMassParams m = sim.Masses[0];
            m.Pseudo = pseudo;
            sim.Masses[0] = m;
        }

        private static Vector3 P(WormSimRef sim, int w, int i) => new Vector3(sim.Pos[w * K + i].x, sim.Pos[w * K + i].y, sim.Pos[w * K + i].z);

        private static Vector3 Centre(WormSimRef sim, int w)
        {
            Vector3 c = Vector3.zero;
            for (int i = 0; i < K; i++) c += P(sim, w, i);
            return c / K;
        }

        private static float Along(Vector3 p) => p.x * DirX + p.z * DirZ;

        private static float Across(Vector3 p) => -p.x * DirZ + p.z * DirX;

        [Fact]
        public void ConstantsAreTheOnesOfTheCpuLayout()
        {
            Assert.Equal(MassLayoutConst.ArmFrac, WormSimConst.ArmFrac);
            Assert.Equal(MassLayoutConst.RecruitSoft, WormSimConst.ArmSoft);
            Assert.Equal(MassLayoutConst.ArmStart, WormSimConst.ArmStart);
            Assert.Equal(MassLayoutConst.ArmMax, WormSimConst.ArmMax);
            Assert.Equal(MassLayoutConst.ArmMargin, WormSimConst.ArmMargin);
            Assert.Equal(MassLayoutConst.ArmWidth, WormSimConst.ArmWidth);
            Assert.Equal(MassLayoutConst.ArmTaper, WormSimConst.ArmTaper);
            Assert.Equal(MassLayoutConst.ArmThick, WormSimConst.ArmThick);
            Assert.Equal(MassLayoutConst.ArmWave, WormSimConst.ArmWave);
            Assert.Equal(MassLayoutConst.ArmWaveK, WormSimConst.ArmWaveK);
            Assert.Equal(MassLayoutConst.ArmWavePer, WormSimConst.ArmWavePer);
            Assert.Equal(MassLayoutConst.ArmFlow, WormSimConst.ArmFlow);
            Assert.Equal(MassLayoutConst.ArmTipFrac, WormSimConst.ArmTipFrac);
            Assert.Equal(MassLayoutConst.ArmTipPitch * Math.PI / 180.0, WormSimConst.ArmTipPitch, 5);
        }

        [Fact]
        public void TheLengthOfTheNetIsThatOfArm()
        {
            const float r = 0.7f;
            // far target: ArmMax
            Vector4 far = WormSimRef.PseudoParams(DirX, DirZ, 9f, 1f, r);
            Assert.Equal(WormSimConst.ArmMax, far.z, 5);
            Assert.Equal(1f, far.w, 5);
            Assert.Equal(DirX, far.x, 5);
            Assert.Equal(DirZ, far.y, 5);
            // near target: what is left before it, less the margin
            float d = 1.6f;
            Vector4 near = WormSimRef.PseudoParams(DirX, DirZ, d, 1f, r);
            Assert.Equal(d - WormSimConst.ArmMargin - WormSimConst.ArmStart * r, near.z, 5);
            // the ramp scales the length
            Vector4 half = WormSimRef.PseudoParams(DirX, DirZ, 9f, 0.5f, r);
            Assert.Equal(0.5f * WormSimConst.ArmMax, half.z, 5);
        }

        [Fact]
        public void NoNetWhenTheTargetIsOnTheMassOrTooCloseOrTheRampIsZero()
        {
            const float r = 0.7f;
            Assert.Equal(Vector4.zero, WormSimRef.PseudoParams(DirX, DirZ, 0f, 1f, r));
            Assert.Equal(Vector4.zero, WormSimRef.PseudoParams(DirX, DirZ, -1f, 1f, r));
            Assert.Equal(Vector4.zero, WormSimRef.PseudoParams(DirX, DirZ, 0.5f, 1f, r));     // under the margin and the start: nothing left
            Assert.Equal(Vector4.zero, WormSimRef.PseudoParams(DirX, DirZ, 5f, 0f, r));
            var m = new GpuMassParams { Pseudo = Vector4.zero, Sleeve = Vector4.zero };
            for (uint s = 1; s < 500; s++) Assert.False(WormSimRef.ArmIs(m, WormSimRef.Hash(s)));
        }

        [Fact]
        public void TargetDistanceZeroLeavesTheMassExactlyAsWithoutAPseudopod()
        {
            const int n = 150;
            float r = RadiusOf(n);
            WormSimRef off = Build(n, r, Vector4.zero);
            WormSimRef zero = Build(n, r, WormSimRef.PseudoParams(DirX, DirZ, 0f, 1f, r));
            off.Init(0, n);
            zero.Init(0, n);
            off.Step(90);
            zero.Step(90);
            for (int i = 0; i < n * K; i++) Assert.Equal(off.Pos[i], zero.Pos[i]);
            // and the mass stays in its dome
            float far = 0f;
            for (int w = 0; w < n; w++) far = Math.Max(far, Centre(off, w).magnitude);
            Assert.True(far < r + 0.2f, $"worm at {far} m of a mass of radius {r}");
        }

        [Fact]
        public void TheFractionOfWormsInTheNetFollowsTheRampAndNeverShrinksWhenTheRampGrows()
        {
            var m = new GpuMassParams { Sleeve = Vector4.zero };
            int last = 0;
            for (int step = 0; step <= 1000; step++)
            {
                float ramp = step / 1000f;
                m.Pseudo = new Vector4(DirX, DirZ, 1.5f, ramp);
                int count = 0;
                for (uint s = 0; s < 2000; s++) if (WormSimRef.ArmIs(m, WormSimRef.Hash(s * 7u + 3u))) count++;
                Assert.True(count >= last, $"net shrinks at ramp {ramp}: {last} -> {count}");
                last = count;
            }
            // at full ramp: ArmFrac less half the soft band, about 20 %
            Assert.InRange(last / 2000f, 0.17f, 0.23f);
        }

        [Fact]
        public void TheLengthOfTheNetIsContinuousInTheRamp()
        {
            const float r = 0.7f;
            float prev = 0f, worst = 0f;
            for (int i = 0; i <= 2000; i++)
            {
                float ramp = i / 2000f;
                float ln = WormSimRef.PseudoParams(DirX, DirZ, 9f, ramp, r).z;
                worst = Math.Max(worst, Math.Abs(ln - prev));
                Assert.True(ln >= prev - 1e-6f);
                prev = ln;
            }
            Assert.True(worst <= WormSimConst.ArmMax / 2000f + 1e-5f, $"step of the length {worst}");
            // and in the distance of the target
            prev = 0f; worst = 0f;
            for (int i = 0; i <= 2000; i++)
            {
                float ln = WormSimRef.PseudoParams(DirX, DirZ, i * 0.005f, 1f, r).z;
                worst = Math.Max(worst, Math.Abs(ln - prev));
                prev = ln;
            }
            Assert.True(worst <= 0.005f + 1e-5f, $"step of the length with the distance {worst}");
        }

        [Fact]
        public void TheSizeOfAWormFadesToNothingAtBothEndsOfTheConveyorAndIsContinuous()
        {
            Assert.Equal(0f, WormSimRef.ArmFadeOf(0f, false), 6);
            Assert.Equal(0f, WormSimRef.ArmFadeOf(1f, false), 6);
            Assert.Equal(1f, WormSimRef.ArmFadeOf(0.5f, false), 6);
            Assert.Equal(1f, WormSimRef.ArmFadeOf(1f, true), 6);
            float prev = 0f;
            for (int i = 1; i <= 1000; i++)
            {
                float f = WormSimRef.ArmFadeOf(i / 1000f, false);
                Assert.True(Math.Abs(f - prev) < 0.03f, $"jump of the fade at {i / 1000f}");
                prev = f;
            }
        }

        [Fact]
        public void ThePlaceOfAWormRunsAlongTheSegmentInsideTheBandOfTheNet()
        {
            const int n = 400;
            float r = RadiusOf(n);
            Vector4 ps = WormSimRef.PseudoParams(DirX, DirZ, 6f, 1f, r);
            WormSimRef sim = Build(n, r, ps);
            GpuMassParams m = sim.Masses[0];
            float a0 = WormSimConst.ArmStart * r, ln = ps.z;
            int seen = 0, tips = 0;
            float maxAlong = 0f;
            for (int w = 0; w < n; w++)
            {
                uint seed = sim.Worms[w].Seed;
                if (!WormSimRef.ArmIs(m, seed)) continue;
                seen++;
                for (int k = 0; k < 40; k++)
                {
                    float t = k * 0.7f;
                    sim.ArmFrame(0, m, seed, t, out Vector3 hp, out Vector3 f, out float fade, out bool tip);
                    Assert.Equal(1f, f.magnitude, 3);
                    Assert.InRange(fade, 0f, 1f);
                    float al = Along(hp);
                    Assert.InRange(al, a0 - 1e-3f, a0 + ln + 1e-3f);                  // the place is on the segment: the lateral offsets are perpendicular to it
                    maxAlong = Math.Max(maxAlong, al);
                    Assert.True(Math.Abs(Across(hp)) <= WormSimConst.ArmWave + 0.5f * WormSimConst.ArmWidth + 1e-3f, $"lateral {Across(hp)}");
                    Assert.InRange(hp.y, WormSimConst.Radius - 1e-4f, WormSimConst.Radius + WormSimConst.ArmThick + 1e-4f);
                    if (tip)
                    {
                        tips++;
                        Assert.Equal(a0 + ln, al, 3);                                   // a worm that rears up is at the tip
                        Assert.True(f.y > 0.9f, $"tip pitch {f.y}");
                    }
                    else
                    {
                        Assert.Equal(0f, f.y, 4);
                        Assert.True(Vector3.Dot(f, new Vector3(DirX, 0f, DirZ)) > 0.5f, "a worm of the net lies along it");
                    }
                }
            }
            _out.WriteLine($"{seen} worms of the net of {n}, {tips} samples at the tip, farthest place {maxAlong:0.00} of {a0 + ln:0.00}");
            Assert.True(seen > 0.15f * n && seen < 0.25f * n);
            Assert.True(maxAlong > a0 + 0.9f * ln, "the conveyor reaches the tip");
        }

        [Fact]
        public void TheNetOfTheSimulationStretchesToTheLengthOfTheSegment()
        {
            const int n = 600;
            float r = RadiusOf(n);
            Vector4 ps = WormSimRef.PseudoParams(DirX, DirZ, 6f, 1f, r);
            WormSimRef sim = Build(n, r, ps);
            sim.Init(0, n);
            sim.Step(420);
            GpuMassParams m = sim.Masses[0];
            float a0 = WormSimConst.ArmStart * r, ln = ps.z;
            float netMax = 0f, domeMax = 0f, maxAcross = 0f;
            int net = 0, visibleNet = 0;
            for (int w = 0; w < n; w++)
            {
                Vector3 c = Centre(sim, w);
                if (WormSimRef.ArmIs(m, sim.Worms[w].Seed))
                {
                    net++;
                    if (sim.Pos[w * K + 3].w > 0.5f)
                    {
                        visibleNet++;
                        netMax = Math.Max(netMax, Along(c));
                        maxAcross = Math.Max(maxAcross, Math.Abs(Across(c)));
                    }
                }
                else domeMax = Math.Max(domeMax, new Vector3(c.x, 0f, c.z).magnitude);
            }
            _out.WriteLine($"net of {net} worms ({visibleNet} visible), farthest centre {netMax:0.00} m (start {a0:0.00} + length {ln:0.00}), lateral {maxAcross:0.00}, dome {domeMax:0.00} (radius {r:0.00})");
            Assert.True(visibleNet > 0.5f * net, "most of the net is at full size");
            Assert.True(netMax > a0 + 0.85f * ln, $"the net stops short: {netMax}");
            Assert.True(netMax < a0 + ln + 0.25f, $"the net goes past its tip: {netMax}");
            Assert.True(maxAcross < WormSimConst.ArmWave + 0.5f * WormSimConst.ArmWidth + 0.25f, $"net too wide: {maxAcross}");
            Assert.True(domeMax < r + 0.25f, $"the rest of the mass left its dome: {domeMax}");
            // the lengths of the ropes hold
            for (int w = 0; w < n; w++)
            {
                float total = 0f;
                for (int i = 0; i < K - 1; i++) total += (P(sim, w, i + 1) - P(sim, w, i)).magnitude;
                Assert.InRange(total, 0.9f * WormSimConst.WormLength, 1.1f * WormSimConst.WormLength);
            }
        }

        [Fact]
        public void TheNetGrowsAndRetractsWithoutJumpsOfVisibleWorms()
        {
            const int n = 400;
            float r = RadiusOf(n);
            WormSimRef sim = Build(n, r, Vector4.zero);
            sim.Init(0, n);
            sim.Step(60);
            var before = new Vector3[n];
            float worstGrow = 0f, worstRetract = 0f, ramp = 0f;
            // grow: the ramp of the visual goes 0 -> 1 in 0.4 s
            for (int step = 0; step < 150; step++)
            {
                ramp = Math.Min(1f, step / 24f);
                SetPseudo(sim, WormSimRef.PseudoParams(DirX, DirZ, 6f, ramp, r));
                for (int w = 0; w < n; w++) before[w] = Centre(sim, w);
                sim.Step();
                for (int w = 0; w < n; w++)
                    if (sim.Pos[w * K + 3].w > 0.3f) worstGrow = Math.Max(worstGrow, (Centre(sim, w) - before[w]).magnitude);
            }
            for (int step = 0; step < 150; step++)
            {
                float visual = Math.Max(0f, 1f - step / 24f);                // the ramp of the visual falls in 0.4 s, the bridge smooths it
                ramp = WormSimRef.SmoothRamp(ramp, visual, WormSimConst.Dt);
                SetPseudo(sim, WormSimRef.PseudoParams(DirX, DirZ, 6f, ramp, r));
                for (int w = 0; w < n; w++) before[w] = Centre(sim, w);
                sim.Step();
                for (int w = 0; w < n; w++)
                    if (sim.Pos[w * K + 3].w > 0.3f) worstRetract = Math.Max(worstRetract, (Centre(sim, w) - before[w]).magnitude);
            }
            _out.WriteLine($"largest step of a visible worm: growing {worstGrow * 1000f:0.0} mm, retracting {worstRetract * 1000f:0.0} mm");
            Assert.True(worstGrow < 0.06f, $"visible worm jumps by {worstGrow} m while the net grows");
            Assert.True(worstRetract < 0.15f, $"visible worm jumps by {worstRetract} m while the net retracts");
            // the worms are back at full size and in the dome a few seconds after the net is gone
            sim.Step(240);
            float far = 0f;
            for (int w = 0; w < n; w++)
            {
                far = Math.Max(far, new Vector3(Centre(sim, w).x, 0f, Centre(sim, w).z).magnitude);
                Assert.Equal(1f, sim.Pos[w * K + 3].w, 3);
            }
            Assert.True(far < r + 0.25f, $"worm left outside after the retraction: {far}");
        }

        [Fact]
        public void AWormThrownSidewaysWhileTheNetIsOnIsContainedAtOnce()
        {
            // S08: a node thrown out of the corridor (a push through a wall) was exempt from the containment (leaving / arm) and flew on 30 m away. Only the net corridor is exempt.
            const int n = 400;
            float r = RadiusOf(n);
            Vector4 ps = WormSimRef.PseudoParams(DirX, DirZ, 6f, 1f, r);
            WormSimRef sim = Build(n, r, ps);
            sim.Init(0, n);
            sim.Step(120);
            Vector3 side = new Vector3(-DirZ, 0f, DirX);
            int thrown = 0;
            for (int w = 0; w < n && thrown < 40; w++)
            {
                if (WormSimRef.ArmIs(sim.Masses[0], sim.Worms[w].Seed)) continue;       // a worm of the net: the pull toward its place is the other test
                Vector3 c = Centre(sim, w);
                for (int i = 0; i < K; i++)
                {
                    Vector3 p = P(sim, w, i) + side * (4f + 0.01f * i) - new Vector3(0f, 0f, 0f);
                    sim.Pos[w * K + i] = new Vector4(p.x, p.y, p.z, 1f);
                    sim.Prev[w * K + i] = new Vector4(p.x, p.y, p.z, 1f);
                }
                thrown++;
            }
            Assert.True(thrown > 20);
            sim.Step(6);
            float far = 0f;
            for (int w = 0; w < n; w++) far = Math.Max(far, Math.Abs(Across(Centre(sim, w))));
            _out.WriteLine($"largest lateral distance of a worm 6 steps after {thrown} were thrown 4 m sideways: {far:0.00} m (radius {r:0.00})");
            Assert.True(far < 4.2f, $"a worm thrown sideways flies on: {far} m");
            sim.Step(60);
            far = 0f;
            for (int w = 0; w < n; w++) far = Math.Max(far, Math.Abs(Across(Centre(sim, w))));
            Assert.True(far < r + 0.6f, $"a worm thrown sideways was not taken back: {far} m");
        }

        [Fact]
        public void TheRampGivenToTheGpuGrowsAtOnceAndFallsAtMostAtTheRetractRate()
        {
            Assert.Equal(0.8f, WormSimRef.SmoothRamp(0.3f, 0.8f, WormSimConst.Dt), 6);
            float r = 1f;
            float expected = 1f;
            for (int i = 0; i < 200; i++)
            {
                r = WormSimRef.SmoothRamp(r, 0f, WormSimConst.Dt);
                expected = Math.Max(0f, expected - WormSimConst.ArmRetractRate * WormSimConst.Dt);
                Assert.Equal(expected, r, 5);
            }
            Assert.Equal(0f, r, 6);
            // it never goes under the ramp of the visual
            Assert.Equal(0.9f, WormSimRef.SmoothRamp(0.91f, 0.9f, WormSimConst.Dt), 6);
        }

        [Fact]
        public void TheNetDoesNotDependOnTheOrderOfTheInsertions()
        {
            const int n = 200;
            float r = RadiusOf(n);
            Vector4 ps = WormSimRef.PseudoParams(DirX, DirZ, 6f, 1f, r);
            WormSimRef a = Build(n, r, ps), b = Build(n, r, ps);
            b.ReverseInsert = true;
            a.Init(0, n);
            b.Init(0, n);
            a.Step(120);
            b.Step(120);
            for (int i = 0; i < n * K; i++) Assert.Equal(a.Pos[i], b.Pos[i]);
        }
    }
}
