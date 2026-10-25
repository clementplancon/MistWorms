using System;
using MistWorms.Render.Gpu;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>
    /// The CPU reference of the GPU worm rope simulation (visual v2, pass B1): the properties the compute shader must have too. The GPU is compared with this
    /// reference by the parity scenario V14 (in game) and by SimCheck (editor).
    /// </summary>
    public class WormSimRefTests
    {
        private readonly ITestOutputHelper _out;
        public WormSimRefTests(ITestOutputHelper output) { _out = output; }

        private const int K = WormSimConst.Nodes;

        private static float RadiusOf(int worms) => (float)Math.Pow(worms * 7.6e-5 / 0.5 / 0.94, 1.0 / 3.0);

        /// <summary>One mass at the origin on flat ground (local frame = world frame), <paramref name="worms"/> worms, no primitive yet.</summary>
        private static WormSimRef Build(int worms, float radius, uint seed = 5u, float speed = 0f)
        {
            var sim = new WormSimRef(worms);
            float ext = 2f * (radius + 0.6f), sp = ext / (WormSimConst.HeightN - 1);
            var m = new GpuMassParams
            {
                Origin = new Vector4(0f, 0f, 0f, 1f),
                Center = new Vector4(0f, 0f, 0f, radius),
                Heading = new Vector4(0f, 0f, 1f, speed),
                Shape = new Vector4(radius, Math.Max(0.45f * radius, 0.05f), radius, 0f),
                Motion = new Vector4(0.8f, 1f, 0.02f, 0f),
                Sleeve = new Vector4(0f, 0.04f, 0f, 0.6f),
                FirstWorm = 0, WormCount = (uint)worms, PrimCount = 0, MassIndex = 0,
                GridRect = new Vector4(-ext * 0.5f, -ext * 0.5f, sp, 1f / sp),
            };
            sim.Masses[0] = m;
            for (int i = 0; i < worms; i++) sim.Worms[i] = WormSimRef.MakeWorm(0u, WormSimRef.Hash(unchecked(seed * 0x9E3779B1u + (uint)i)));
            return sim;
        }

        private static void SetPrims(WormSimRef sim, params GpuPrim[] prims)
        {
            for (int i = 0; i < prims.Length; i++) sim.Prims[i] = prims[i];
            GpuMassParams m = sim.Masses[0];
            m.PrimCount = (uint)prims.Length;
            sim.Masses[0] = m;
        }

        private static void SetSleeve(WormSimRef sim, float shell, float engulf)
        {
            GpuMassParams m = sim.Masses[0];
            m.Sleeve = new Vector4(1f, shell, 0f, 0.6f);
            m.Motion = new Vector4(m.Motion.x, m.Motion.y, m.Motion.z, engulf);
            sim.Masses[0] = m;
        }

        private static Vector3 P(WormSimRef sim, int w, int i) => new Vector3(sim.Pos[w * K + i].x, sim.Pos[w * K + i].y, sim.Pos[w * K + i].z);

        [Fact]
        public void LengthsAreConservedOnOpenGround()
        {
            const int n = 300;
            WormSimRef sim = Build(n, RadiusOf(n));
            sim.Init(0, n);
            sim.Step(120);
            float worst = 0f;
            for (int w = 0; w < n; w++)
                for (int i = 0; i < K - 1; i++)
                    worst = Math.Max(worst, Math.Abs((P(sim, w, i + 1) - P(sim, w, i)).magnitude / WormSimConst.SegLen - 1f));
            _out.WriteLine($"largest segment error {worst * 100f:0.0} %");
            Assert.True(worst < 0.08f, $"segment error {worst}");
            for (int w = 0; w < n; w++)
            {
                float total = 0f;
                for (int i = 0; i < K - 1; i++) total += (P(sim, w, i + 1) - P(sim, w, i)).magnitude;
                Assert.InRange(total, 0.92f * WormSimConst.WormLength, 1.08f * WormSimConst.WormLength);
            }
        }

        [Fact]
        public void NoNodeGoesUnderTheGround()
        {
            const int n = 200;
            WormSimRef sim = Build(n, RadiusOf(n));
            // sloping, bumpy ground
            int hn = WormSimConst.HeightN;
            Vector4 g = sim.Masses[0].GridRect;
            for (int z = 0; z < hn; z++)
                for (int x = 0; x < hn; x++)
                    sim.Height[z * hn + x] = 0.15f * (g.x + x * g.z) + 0.05f * (float)Math.Sin((g.y + z * g.z) * 4f);
            sim.Init(0, n);
            for (int step = 0; step < 150; step++)
            {
                sim.Step();
                if (step % 10 != 0) continue;
                for (int w = 0; w < n; w++)
                    for (int i = 0; i < K; i++)
                    {
                        Vector3 p = P(sim, w, i);
                        float gx = Mathf.Clamp((p.x - g.x) * g.w, 0f, 14.999f), gz = Mathf.Clamp((p.z - g.y) * g.w, 0f, 14.999f);
                        int ix = (int)gx, iz = (int)gz;
                        float fx = gx - ix, fz = gz - iz;
                        float a = sim.Height[iz * hn + ix] + fx * (sim.Height[iz * hn + ix + 1] - sim.Height[iz * hn + ix]);
                        float c = sim.Height[(iz + 1) * hn + ix] + fx * (sim.Height[(iz + 1) * hn + ix + 1] - sim.Height[(iz + 1) * hn + ix]);
                        float h = a + fz * (c - a);
                        Assert.True(p.y >= h + WormSimConst.Radius - 1e-4f, $"step {step} worm {w} node {i}: y {p.y} under the ground {h}");
                    }
            }
        }

        [Fact]
        public void WormsStickToAnAdhesiveCapsule()
        {
            const int n = 300;
            float r = RadiusOf(n);
            const float cr = 0.3f, height = 1.8f;
            WormSimRef sim = Build(n, r);
            SetPrims(sim, GpuPrim.Capsule(new Vector3(0f, cr, 0f), new Vector3(0f, height - cr, 0f), cr, 1f, 1f));
            SetSleeve(sim, 0.05f, 1f);
            sim.Init(0, n);
            sim.Step(200);

            const int bands = 12;
            var mark = new int[bands];
            int attached = 0;
            for (int w = 0; w < n; w++)
                for (int i = 0; i < K; i++)
                {
                    Vector3 p = P(sim, w, i);
                    float rad = new Vector2(p.x, p.z).magnitude;
                    float axisY = Mathf.Clamp(p.y, cr, height - cr);
                    float dist = new Vector3(p.x, p.y - axisY, p.z).magnitude;
                    Assert.True(dist > cr - 0.02f, $"worm {w} node {i} inside the capsule: distance to the axis {dist}");
                    if (rad < cr + 0.05f) { mark[Mathf.Clamp((int)(p.y / height * bands), 0, bands - 1)]++; attached++; }
                }
            int covered = 0;
            for (int b = 0; b < bands; b++) if (mark[b] >= 2) covered++;
            _out.WriteLine($"{attached} nodes on the capsule, {covered} of {bands} bands covered");
            Assert.True(covered >= 0.6f * bands, $"coverage {covered}/{bands}");

            // with half the height engulfed, nothing is attached above it
            WormSimRef half = Build(n, r);
            SetPrims(half, GpuPrim.Capsule(new Vector3(0f, cr, 0f), new Vector3(0f, height - cr, 0f), cr, 1f, 0.5f));
            SetSleeve(half, 0.05f, 0.5f);
            half.Init(0, n);
            half.Step(200);
            float top = 0f;
            for (int w = 0; w < n; w++)
                for (int i = 0; i < K; i++)
                {
                    Vector3 p = P(half, w, i);
                    if (new Vector2(p.x, p.z).magnitude < cr + 0.02f) top = Math.Max(top, p.y);
                }
            _out.WriteLine($"half engulf: highest node on the capsule {top:0.00} m");
            Assert.True(top < 0.5f * height + 0.2f, $"a node is attached at {top} m with half the height covered");
        }

        [Fact]
        public void TheMassFlowsAlongACorridor()
        {
            const int n = 100;
            float r = RadiusOf(n);
            WormSimRef sim = Build(n, r, 11u, 0.6f);
            // two walls along z, 0.30 m apart (corridor half width 0.15 m), 8 m long
            const float half = 0.15f;
            SetPrims(sim,
                GpuPrim.Box(new Vector3(-half - 0.1f, 0.5f, 3f), new Vector3(0.1f, 0.5f, 4f), Quaternion.identity),
                GpuPrim.Box(new Vector3(half + 0.1f, 0.5f, 3f), new Vector3(0.1f, 0.5f, 4f), Quaternion.identity));
            GpuMassParams m = sim.Masses[0];
            m.Shape = new Vector4(r * 1.3f, 0.45f * r, half, 0.2f * r);      // the dome is squeezed to the corridor
            m.GridRect = new Vector4(-2f, -2f, 8f / 15f, 15f / 8f);
            sim.Masses[0] = m;
            sim.Init(0, n);
            sim.Step(30);

            float travelled = 0f;
            for (int step = 0; step < 360; step++)
            {
                GpuMassParams mm = sim.Masses[0];
                float z = mm.Center.z + 0.6f * WormSimConst.Dt;
                mm.Center = new Vector4(0f, 0f, z, mm.Center.w);
                sim.Masses[0] = mm;
                travelled = z;
                sim.Step();
                if (step % 20 == 0)
                    for (int w = 0; w < n; w++)
                        for (int i = 0; i < K; i++)
                        {
                            Vector3 p = P(sim, w, i);
                            if (p.z > 0.2f && p.z < 5.5f)
                                Assert.True(Math.Abs(p.x) <= half + WormSimConst.Radius * 2f + 0.02f, $"step {step}: node of worm {w} left the corridor (x {p.x}, z {p.z})");
                        }
            }
            float mean = 0f;
            for (int w = 0; w < n; w++)
            {
                float c = 0f;
                for (int i = 0; i < K; i++) c += P(sim, w, i).z;
                mean += c / K;
            }
            mean /= n;
            _out.WriteLine($"the centre moved {travelled:0.00} m, the mean of the worms {mean:0.00} m");
            Assert.True(mean > 0.6f * travelled, $"the worms did not follow: mean z {mean} for a centre at {travelled}");
        }

        [Fact]
        public void TheResultDoesNotDependOnTheOrderOfTheInsertions()
        {
            const int n = 150;
            float r = RadiusOf(n) * 0.6f;       // dense: the cells overflow, so the order of the insertions matters if the grid does not keep a canonical set
            WormSimRef a = Build(n, r), b = Build(n, r);
            b.ReverseInsert = true;
            a.Init(0, n); b.Init(0, n);
            a.Step(40); b.Step(40);
            for (int i = 0; i < n * K; i++) Assert.True(a.Pos[i] == b.Pos[i], $"node {i}");
        }

        [Fact]
        public void ADifferenceOfAMicrometreAtTheStartStaysSmall()
        {
            // what makes the GPU parity within a millimetre possible: the creep is kinematic (no follower force feeding the Verlet velocity), so the rounding of the
            // GPU is not amplified into a different trajectory in 30 steps
            const int n = 64;
            float r = 1f;
            WormSimRef a = Build(n, r, 77u, 0.6f), b = Build(n, r, 77u, 0.6f);
            a.Init(0, n); b.Init(0, n);
            for (int i = 0; i < n * K; i++)
            {
                float d = 1e-6f * ((i * 7919) % 13 - 6) / 6f;
                b.Pos[i] = b.Prev[i] = new Vector4(b.Pos[i].x + d, b.Pos[i].y, b.Pos[i].z - d, 1f);
            }
            a.Step(30); b.Step(30);
            float max = 0f;
            for (int i = 0; i < n * K; i++) max = Math.Max(max, (new Vector3(a.Pos[i].x, a.Pos[i].y, a.Pos[i].z) - new Vector3(b.Pos[i].x, b.Pos[i].y, b.Pos[i].z)).magnitude);
            _out.WriteLine($"after 30 steps: {max * 1000f:0.000} mm");
            Assert.True(max < 0.005f, $"difference {max * 1000f} mm");
        }

        [Theory]
        [InlineData(0.12f)]
        [InlineData(0.8f)]
        public void TheWormsFlowOverAndAroundAnObstacleKnownByItsDepthMaps(float blockHeight)
        {
            const int n = 900;
            float r = RadiusOf(n);
            WormSimRef sim = Build(n, r);
            GpuMassParams m = sim.Masses[0];
            // a block 0.8 m high and 0.8 x 0.7 m wide, half inside the footprint of the mass
            var min = new Vector3(0.35f, 0f, -0.35f);
            var max = new Vector3(1.15f, blockHeight, 0.35f);
            m.Side = new Vector4(-0.5f, 0.3f, 1f / 0.3f, 1f);
            sim.Masses[0] = m;
            GpuSolidMaps.FillBox(sim.Sides, 0, m, min, max);
            sim.Init(0, n);
            sim.Step(360);
            int inside = 0, above = 0, west = 0;
            for (int w = 0; w < n; w++)
                for (int i = 0; i < K; i++)
                {
                    Vector3 p = P(sim, w, i);
                    if (p.x > min.x + 0.01f && p.x < max.x - 0.01f && p.z > min.z + 0.01f && p.z < max.z - 0.01f && p.y < max.y - 0.01f) inside++;
                    if (p.x > min.x && p.x < max.x && p.z > min.z && p.z < max.z && p.y >= max.y) above++;
                    if (p.x > min.x - 0.2f && p.x <= min.x && Math.Abs(p.z) < 0.35f) west++;
                }
            _out.WriteLine($"{inside} nodes inside the block, {above} on top of it, {west} at its west foot, of {n * K}");
            Assert.True(inside < 0.01f * n * K, $"{inside} nodes inside the block");
            // a block lower than the dome is covered (the worms leave it by the top), a higher one is gone round and its foot is piled up
            if (blockHeight < 0.3f) Assert.True(above > 0.01f * n * K, "the worms cover the top of the low block");
            else Assert.True(west > 0.01f * n * K, "the worms pile at the foot of the tall block");
        }

        [Fact]
        public void TwoRunsGiveTheSameBits()
        {
            const int n = 100;
            WormSimRef a = Build(n, RadiusOf(n), 3u, 0.5f), b = Build(n, RadiusOf(n), 3u, 0.5f);
            a.Init(0, n); b.Init(0, n);
            a.Step(60); b.Step(60);
            for (int i = 0; i < n * K; i++) Assert.True(a.Pos[i] == b.Pos[i], $"node {i}");
        }

        [Fact]
        public void SeveralMassesDoNotInteract()
        {
            // a mass alone, then the same mass next to a second one in the same buffers: identical result
            const int n = 60;
            float r = RadiusOf(n);
            WormSimRef alone = Build(n, r);
            alone.Init(0, n);
            alone.Step(50);

            var both = new WormSimRef(2 * n);
            both.Masses[0] = alone.Masses[0];
            GpuMassParams m1 = alone.Masses[0];
            m1.Origin = new Vector4(100f, 0f, 0f, 1f);
            m1.FirstWorm = (uint)n; m1.MassIndex = 1;
            both.Masses[1] = m1;
            int hn = WormSimConst.HeightN * WormSimConst.HeightN;
            for (int i = 0; i < hn; i++) { both.Height[i] = alone.Height[i]; both.Height[hn + i] = alone.Height[i]; }
            for (int i = 0; i < n; i++)
            {
                both.Worms[i] = alone.Worms[i];
                both.Worms[n + i] = WormSimRef.MakeWorm(1u, WormSimRef.Hash(unchecked(99u * 0x9E3779B1u + (uint)i)));
            }
            both.Init(0, 2 * n);
            both.Step(50);
            for (int i = 0; i < n * K; i++) Assert.True(alone.Pos[i] == both.Pos[i], $"node {i} of the first mass changed");
        }

        [Fact]
        public void PrimitiveDistances()
        {
            var sphere = GpuPrim.Sphere(new Vector3(1f, 0f, 0f), 0.5f);
            Assert.Equal(0.5f, WormSimRef.PrimDist(sphere, new Vector3(2f, 0f, 0f), out Vector3 n), 4);
            Assert.Equal(1f, n.x, 4);
            var cap = GpuPrim.Capsule(new Vector3(0f, 0f, 0f), new Vector3(0f, 2f, 0f), 0.3f);
            Assert.Equal(0.7f, WormSimRef.PrimDist(cap, new Vector3(1f, 1f, 0f), out n), 4);
            Assert.Equal(0.2f, WormSimRef.PrimDist(cap, new Vector3(0f, 2.5f, 0f), out n), 4);
            var box = GpuPrim.Box(new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 1f), Quaternion.identity);
            Assert.Equal(0.5f, WormSimRef.PrimDist(box, new Vector3(1.5f, 0f, 0f), out n), 4);
            Assert.Equal(-0.2f, WormSimRef.PrimDist(box, new Vector3(0.8f, 0f, 0f), out n), 4);
            Assert.Equal(1f, n.x, 4);
        }
    }
}
