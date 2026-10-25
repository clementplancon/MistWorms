using System;
using System.Collections.Generic;
using MistWorms.Render.Gpu;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace MistWorms.Tests
{
    /// <summary>
    /// The skin engulf (visual v2): anchors on the surface of the engulfed body skinned from the matrices of the bones, the size of the sleeve (S = min(0.6 V, 2 x area / footprint) worms,
    /// filled from the bottom up), worms that climb the body by a path of anchors and lie on the surface along their whole length, tangent to it, up or diagonally.
    /// </summary>
    public class SkinEngulfTests
    {
        private readonly ITestOutputHelper _out;
        public SkinEngulfTests(ITestOutputHelper output) { _out = output; }

        private const int K = WormSimConst.Nodes;
        private const float Height = 1.75f;

        private static GpuSkinSet Body(out int[] limbs)
        {
            limbs = new int[WormSimConst.MaxAnchors];
            return GpuSkinAnchors.SyntheticSkin(Vector3.zero, Height, limbs);
        }

        private static GpuMassParams Params(int worms, GpuSkinSet set, float engulf)
        {
            float r = (float)Math.Pow(worms * 7.6e-5 / 0.5 / 0.94, 1.0 / 3.0);
            float ext = 2f * (r + 0.6f), sp = ext / (WormSimConst.HeightN - 1);
            GpuSleevePlan plan = set.Plan(worms, engulf);
            return new GpuMassParams
            {
                Origin = new Vector4(0f, 0f, 0f, 1f), Center = new Vector4(0f, 0f, 0f, r), Heading = new Vector4(0f, 0f, 1f, 0f),
                Shape = new Vector4(r, 0.45f * r, r, 0f), Motion = new Vector4(0.8f, 1f, 0.02f, engulf), Sleeve = new Vector4(0f, 0.02f, 0f, 0.6f),
                FirstWorm = 0, WormCount = (uint)worms, GridRect = new Vector4(-ext * 0.5f, -ext * 0.5f, sp, 1f / sp),
                Skin = new Vector4(set.Count, engulf, Height, set.Area),
                Plan = new Vector4(plan.Count, plan.PerAnchor, plan.TopRel, plan.Cover), Plan2 = new Vector4(plan.Base, 0f, 0f, 0f),
            };
        }

        private static WormSimRef Sim(int worms, GpuSkinSet set, float engulf, uint seed)
        {
            var sim = new WormSimRef(worms);
            sim.Masses[0] = Params(worms, set, engulf);
            sim.SetSkin(0, set);
            sim.SkinAnchors(0);
            for (int i = 0; i < worms; i++) sim.Worms[i] = WormSimRef.MakeWorm(0u, WormSimRef.Hash(unchecked(seed * 0x9E3779B1u + (uint)i)));
            sim.Init(0, worms);
            return sim;
        }

        private static Vector3 P(Vector4 v) => new Vector3(v.x, v.y, v.z);
        private static Matrix4x4 Translation(float x, float y, float z)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m03 = x; m.m13 = y; m.m23 = z;
            return m;
        }

        // a rotation about the x axis through a point (Quaternion.Euler is an engine call: not available outside the game)
        private static Matrix4x4 RotateAboutX(float degrees, Vector3 pivot)
        {
            float a = degrees * (float)Math.PI / 180f, c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            Matrix4x4 m = Matrix4x4.identity;
            m.m11 = c; m.m12 = -s; m.m21 = s; m.m22 = c;
            m.m13 = pivot.y - (c * pivot.y - s * pivot.z);
            m.m23 = pivot.z - (s * pivot.y + c * pivot.z);
            return m;
        }


        // ---------------------------------------------------------------- the anchors

        [Fact]
        public void TheAnchorsAreEvenlySpreadOnTheSurface()
        {
            GpuSkinSet set = Body(out _);
            var anchors = new GpuAnchor[WormSimConst.MaxAnchors];
            GpuSkinning.SkinAll(set, GpuSkinAnchors.RestBones(), anchors, 0);
            _out.WriteLine($"{set.Count} anchors, area {set.Area:0.00} m2");
            Assert.InRange(set.Count, 300, WormSimConst.MaxAnchors);
            Assert.InRange(set.Area, 1.2f, 3.5f);
            float closest = float.MaxValue;
            for (int i = 0; i < set.Count; i++)
                for (int j = i + 1; j < set.Count; j++)
                    closest = Math.Min(closest, (P(anchors[i].A) - P(anchors[j].A)).magnitude);
            Assert.True(closest > 0.015f, $"two anchors {closest} m apart");
        }

        [Fact]
        public void TheAnchorsAreSkinnedFromTheMatricesOfTheBones()
        {
            GpuSkinSet set = Body(out int[] limbs);
            Matrix4x4[] bones = GpuSkinAnchors.RestBones();
            var rest = new GpuAnchor[WormSimConst.MaxAnchors];
            GpuSkinning.SkinAll(set, bones, rest, 0);
            // the arms swing: a rotation about the shoulder and a translation
            for (int b = 6; b <= 7; b++) bones[b] = RotateAboutX(40f, new Vector3(0f, 1.4f, 0f));
            bones[2] = Translation(0f, 0f, 0.2f);
            var moved = new GpuAnchor[WormSimConst.MaxAnchors];
            GpuSkinning.SkinAll(set, bones, moved, 0);
            for (int i = 0; i < set.Count; i++)
            {
                Vector3 p = P(set.Verts[i].PosRel), n = P(set.Verts[i].Normal);
                Matrix4x4 m = bones[limbs[i]];
                Assert.True((m.MultiplyPoint3x4(p) - P(moved[i].A)).magnitude < 1e-5f, $"anchor {i} position");
                Assert.True((m.MultiplyVector(n) - P(moved[i].B)).magnitude < 1e-5f, $"anchor {i} normal");
                Assert.Equal(set.Verts[i].PosRel.w, moved[i].B.w);
            }
            // a vertex with two bones is the weighted sum
            GpuSkinVertex v = set.Verts[10];
            v.Bone0 = 2; v.Bone1 = 6; v.Weights = new Vector4(0.25f, 0.75f, 0f, 0f);
            GpuSkinning.Skin(v, bones, 0, out Vector3 pos, out _);
            Vector3 want = 0.25f * bones[2].MultiplyPoint3x4(P(v.PosRel)) + 0.75f * bones[6].MultiplyPoint3x4(P(v.PosRel));
            Assert.True((pos - want).magnitude < 1e-5f);
        }

        [Fact]
        public void ThePathsOverTheSurfaceLeadFromTheBaseOfTheBodyToEveryAnchor()
        {
            GpuSkinSet set = Body(out _);
            int n = set.Count, kn = WormSimConst.NeighbourCount;
            Assert.True(set.BaseCount >= 4);
            int unreachable = 0, longest = 0;
            for (int t = 0; t < n; t += 7)
                for (int s0 = 0; s0 < set.BaseCount; s0 += 3)
                {
                    int src = (int)set.Order[s0], steps = 0;
                    while (src != t && steps <= n)
                    {
                        uint hop = set.HopOf(t, src);
                        if (hop >= kn) { unreachable++; break; }
                        src = (int)set.Knn[src * kn + (int)hop];
                        steps++;
                    }
                    if (steps > n) unreachable++;
                    longest = Math.Max(longest, steps);
                }
            _out.WriteLine($"{set.BaseCount} anchors at the base, {unreachable} unreachable pairs, longest path {longest} hops");
            Assert.Equal(0, unreachable);
            Assert.InRange(longest, 1, 80);
        }

        // ---------------------------------------------------------------- the size of the sleeve

        [Theory]
        [InlineData(500, 1.0f)]
        [InlineData(2000, 1.0f)]
        [InlineData(2000, 0.5f)]
        [InlineData(100, 1.0f)]
        [InlineData(2400, 0.3f)]
        public void TheSleeveHoldsTwoLayersOfWormsOnTheEligibleAreaWithoutExceedingSixtyPercentOfTheWorms(int worms, float engulf)
        {
            GpuSkinSet set = Body(out _);
            GpuSleevePlan plan = set.Plan(worms, engulf);
            float apa = set.Area / set.Count;
            int eligible = 0;
            for (int i = 0; i < set.Count; i++) if (set.Verts[i].PosRel.w <= engulf) eligible++;
            float footprint = 0.30f * 0.018f;
            float want = Math.Min(0.6f * worms, 2f * eligible * apa / footprint);
            _out.WriteLine($"V {worms}, engulf {engulf}: {eligible} eligible anchors ({eligible * apa:0.00} m2), S = {plan.Count:0} ({100f * plan.Count / worms:0}% of V), covers {plan.Cover} anchors up to {plan.TopRel:0.00}");
            Assert.Equal(eligible, plan.Eligible);
            Assert.Equal(want, plan.Count, 2);
            Assert.True(plan.Count <= 0.6f * worms + 1e-3f);
            Assert.True(plan.Cover <= plan.Eligible);
            Assert.True(plan.TopRel <= engulf + 1e-6f);
            if (plan.Count < 0.6f * worms - 1e-3f) Assert.Equal(plan.Eligible, plan.Cover);          // the worms suffice: the whole eligible area is covered
            else Assert.True(plan.Cover <= plan.Eligible);                                              // the worms run short: covered from the bottom up, as far as they go
        }

        [Fact]
        public void ThePlayerOfTwoHundredUnitsIsWrappedByAboutFortyPercentOfTheWorms()
        {
            GpuSkinSet set = Body(out _);
            GpuSleevePlan natural = set.Plan(2000, 1f);
            _out.WriteLine($"synthetic body: area {set.Area:0.00} m2, S = {natural.Count:0} of 2000 ({100f * natural.Count / 2000f:0}%)");
            Assert.InRange(natural.Count / 2000f, 0.25f, 0.6f);
            // two layers of worms of 30 x 1.8 cm: 40% of 2000 worms need 2.16 m2 of eligible surface (a clothed player is of that order)
            set.Area = 2.2f;
            GpuSleevePlan plan = set.Plan(2000, 1f);
            _out.WriteLine($"a body of 2.2 m2: S = {plan.Count:0} of 2000 ({100f * plan.Count / 2000f:0}%)");
            Assert.True(plan.Count >= 0.4f * 2000f, $"S = {plan.Count}");
            GpuSleevePlan few = set.Plan(500, 1f);
            Assert.Equal(300f, few.Count, 2);
            Assert.True(few.TopRel < 0.8f, $"50 units cover only up to {few.TopRel:0.00} of the height");
            Assert.True(few.Cover < few.Eligible);
        }

        [Fact]
        public void TheSleeveFillsFromTheBottomUpAndTheWormsEnterInOrder()
        {
            GpuSkinSet set = Body(out _);
            foreach (int worms in new[] { 500, 2000 })
            {
                var covered = new HashSet<int>();
                float previousTop = 0f, previousMean = -1f;
                for (float engulf = 0.1f; engulf <= 1.001f; engulf += 0.1f)
                {
                    GpuSleevePlan plan = set.Plan(worms, engulf);
                    var now = new HashSet<int>();
                    float sum = 0f, sumNew = 0f;
                    int added = 0;
                    for (int i = 0; i < worms; i++)
                    {
                        float rank = GpuSleeve.Rank(i, worms) + 0.5f;
                        if (rank >= plan.Count) continue;
                        now.Add(i);
                        int slot = Math.Min((int)(rank / plan.PerAnchor), plan.Cover - 1);
                        float rel = set.SortedRel[slot];
                        Assert.True(rel <= engulf + 1e-6f, $"V {worms} engulf {engulf:0.0}: a worm sits at {rel:0.00}");
                        sum += rel;
                        if (!covered.Contains(i)) { sumNew += rel; added++; }
                    }
                    foreach (int i in covered) Assert.Contains(i, now);                    // nobody leaves while the engulf rises
                    if (added > 0 && previousMean >= 0f) Assert.True(sumNew / added >= previousMean - 0.02f, $"the newcomers (mean {sumNew / added:0.00}) sit higher than the earlier ones ({previousMean:0.00})");
                    if (added > 0) previousMean = sumNew / added;
                    Assert.True(plan.TopRel >= previousTop - 1e-6f);
                    previousTop = plan.TopRel;
                    covered = now;
                }
                _out.WriteLine($"V {worms}: {covered.Count} worms in the sleeve at the end, top at {previousTop:0.00}");
                if (worms == 500) Assert.True(previousTop < 0.8f, "the covered height stays low when the worms are few");
            }
        }

        // ---------------------------------------------------------------- the projection on the surface

        [Fact]
        public void ANodeNearTheSkinIsProjectedOnTheTangentPlaneOfItsNearestAnchor()
        {
            GpuSkinSet set = Body(out _);
            WormSimRef sim = Sim(60, set, 1f, 3u);
            int hits = 0;
            var rng = new System.Random(5);
            for (int trial = 0; trial < 200; trial++)
            {
                int a = rng.Next(set.Count);
                GpuAnchor an = sim.Anchors[a];
                Vector3 pos = P(an.A), n = P(an.B);
                // somewhere above the anchor (up to 5 cm of the plane, 3 cm to the side) or behind the surface
                Vector3 t1 = Vector3.Cross(n, Vector3.up).normalized;
                if (t1.sqrMagnitude < 0.5f) t1 = Vector3.right;
                Vector3 x = pos + n * (float)(rng.NextDouble() * 0.12 - 0.04) + t1 * (float)(rng.NextDouble() * 0.03);
                float att = 0f;
                float off = trial % 2 == 0 ? WormSimConst.SkinOffset : WormSimConst.SkinOffset + WormSimConst.LayerGap;     // the first or the second layer
                Vector3 y = sim.SkinSurface(0, (uint)a, x, true, off, ref att);
                // the nearest anchor of the result is the one it was projected on (or a neighbour of it in the same plane within a few mm)
                uint near = sim.NearestAnchor(0, (uint)a, x);
                GpuAnchor used = sim.Anchors[(int)near];
                float planeDist = Vector3.Dot(y - P(used.A), P(used.B));
                Assert.InRange(planeDist, off - 1e-4f, off + 1e-4f);
                Assert.Equal(1f, att);
                // the move is along the normal of that anchor
                Vector3 move = y - x;
                Assert.True(move.magnitude < 1e-6f || Mathf.Abs(Vector3.Dot(move.normalized, P(used.B))) > 0.999f);
                hits++;

                // not attracted (a worm that hangs): only pushed out when behind the surface
                float att2 = 0f;
                Vector3 z = sim.SkinSurface(0, (uint)a, x, false, off, ref att2);
                float dz = Vector3.Dot(z - P(used.A), P(used.B));
                Assert.True(dz >= Math.Min(Vector3.Dot(x - P(used.A), P(used.B)), WormSimConst.Radius + 0.003f) - 1e-5f);
                Assert.Equal(0f, att2);
            }
            Assert.Equal(200, hits);
            // far from every anchor: untouched
            float att3 = 0f;
            Vector3 far = new Vector3(3f, 3f, 3f);
            Assert.Equal(far, sim.SkinSurface(0, 0, far, true, WormSimConst.SkinOffset, ref att3));
        }

        // One run shared by the tests of the sleeve at rest (700 worms, 7 s of engulf = 1 from the start): the CPU reference is slow.
        private sealed class Run
        {
            public WormSimRef Sim;
            public GpuSkinSet Set;
            public int[] Limbs;
            public int Worms;
            public GpuSkinMetrics.Result Result;
        }

        private static readonly Lazy<Run> s_rest = new Lazy<Run>(() =>
        {
            const int n = 700;
            var run = new Run { Worms = n, Set = Body(out int[] limbs), Limbs = limbs };
            run.Sim = Sim(n, run.Set, 1f, 5u);
            run.Sim.Step(420);
            run.Result = Measure(run.Sim, run.Set, run.Limbs, n);
            return run;
        });

        private static GpuSkinMetrics.Result Measure(WormSimRef sim, GpuSkinSet set, int[] limbs, int worms)
        {
            var anchors = new GpuAnchor[WormSimConst.MaxAnchors];
            Array.Copy(sim.Anchors, anchors, WormSimConst.MaxAnchors);
            var region = new int[WormSimConst.MaxAnchors];
            for (int a = 0; a < set.Count; a++) region[a] = GpuSkinMetrics.RegionOfLimb(limbs[a]);
            var nodes = new Vector4[worms * K];
            Array.Copy(sim.Pos, nodes, nodes.Length);
            var state = new Vector4[worms];
            Array.Copy(sim.SkinState, state, worms);
            return GpuSkinMetrics.Measure(nodes, state, anchors, set.Count, region, worms);
        }

        [Fact]
        public void TheWormsOfTheSleeveLieOnTheSurfaceAlongTheirWholeLengthAndOnTheLegs()
        {
            Run run = s_rest.Value;
            GpuSkinMetrics.Result r = run.Result;
            float planned = run.Sim.Masses[0].Plan.x;
            _out.WriteLine($"S = {planned:0} of {run.Worms}: {r.Settled + r.Transit} worms on the body ({r.Settled} settled, {r.Transit} on their way), {r.Near} of the settled ones with 6 nodes of 8 within 4 cm of the skin ({100f * r.NearShare:0}%)");
            string[] names = { "left leg", "right leg", "left arm", "right arm", "torso and head" };
            for (int l = 0; l < 5; l++) _out.WriteLine($"  {names[l]}: {r.NearRegion[l]} ({100f * r.NearRegion[l] / Math.Max(1, r.Near):0.0}%)");
            Assert.True(r.Settled + r.Transit > 0.6f * planned, $"{r.Settled + r.Transit} worms on the body for S = {planned}");
            Assert.True(r.Settled > 0.4f * planned, $"{r.Settled} worms settled for S = {planned}");
            Assert.True(r.Near >= 0.7f * r.Settled, $"{r.Near} of {r.Settled} on the skin");
            Assert.True(r.NearRegion[0] >= 0.03f * r.Near, "left leg");
            Assert.True(r.NearRegion[1] >= 0.03f * r.Near, "right leg");
        }

        [Fact]
        public void TheWormsLieTangentToTheSurfaceMostlyUpOrDiagonally()
        {
            Run run = s_rest.Value;
            WormSimRef sim = run.Sim;
            int onBody = 0, up = 0, flat = 0, tangent = 0, down = 0, second = 0;
            for (int w = 0; w < run.Worms; w++)
            {
                if ((int)(sim.SkinState[w].x + 0.5f) != 2 || IsPendant(sim, w)) continue;
                Vector3 axis = (P(sim.Pos[w * K + 7]) - P(sim.Pos[w * K])).normalized;
                uint a = sim.NodeAnchor[w * K + 3];
                Vector3 normal = P(sim.Anchors[(int)a].B);
                onBody++;
                if (axis.y > 0.3f) up++;
                else if (Math.Abs(axis.y) < 0.3f) flat++;
                else down++;
                if (Math.Abs(Vector3.Dot(axis, normal)) < 0.35f) tangent++;
                if (WormSimRef.SkinOff(sim.Worms[w].Seed) > WormSimConst.SkinOffset + 1e-4f) second++;
            }
            _out.WriteLine($"{onBody} worms settled and not hanging ({second} in the second layer): {up} point up, {flat} lie across, {down} point down, {tangent} tangent to the surface (|axis . normal| < 0.35)");
            Assert.True(onBody > 100);
            Assert.True(up >= 0.55f * onBody, "most point up or diagonally up");
            Assert.True(tangent >= 0.75f * onBody, "most are tangent to the surface");
            Assert.InRange(second / (float)onBody, 0.35f, 0.65f);
        }

        private static bool IsPendant(WormSimRef sim, int w) => WormSimRef.Hash01(sim.Worms[w].Seed, 19) < WormSimConst.PendantShare;

        [Fact]
        public void TheWormsClimbTheBodyFromItsBaseTheLowOnesFirst()
        {
            const int n = 300;
            GpuSkinSet set = Body(out _);
            WormSimRef sim = Sim(n, set, 1f, 17u);
            var settledAt = new float[n];
            var slotRel = new float[n];
            float plan = sim.Masses[0].Plan.x, perAnchor = sim.Masses[0].Plan.y;
            int cover = (int)(sim.Masses[0].Plan.w + 0.5f);
            for (int w = 0; w < n; w++)
            {
                settledAt[w] = -1f;
                float rk = WormSimRef.SkinRank(w, sim.Masses[0], sim.Worms[w].Seed);
                slotRel[w] = rk < plan ? set.SortedRel[Math.Min((int)(rk / perAnchor), cover - 1)] : -1f;
            }
            float maxHeightFirstSecond = 0f;
            for (int step = 0; step < 60 * 10; step++)
            {
                sim.Step();
                for (int w = 0; w < n; w++)
                {
                    if (settledAt[w] < 0f && (int)(sim.SkinState[w].x + 0.5f) == 2) settledAt[w] = step / 60f;
                    if (step < 60 && slotRel[w] >= 0f) maxHeightFirstSecond = Math.Max(maxHeightFirstSecond, sim.Pos[w * K + 7].y);
                }
            }
            float lowSum = 0f, highSum = 0f;
            int low = 0, high = 0, never = 0;
            for (int w = 0; w < n; w++)
            {
                if (slotRel[w] < 0f) continue;
                if (settledAt[w] < 0f) { never++; continue; }
                if (slotRel[w] < 0.15f) { lowSum += settledAt[w]; low++; }
                else if (slotRel[w] > 0.3f) { highSum += settledAt[w]; high++; }
            }
            _out.WriteLine($"{low} low worms settle after {lowSum / Math.Max(1, low):0.0} s on average, {high} high ones after {highSum / Math.Max(1, high):0.0} s; {never} worms of the sleeve have not settled in 10 s; the highest head in the first second: {maxHeightFirstSecond:0.00} m");
            Assert.True(low > 5 && high > 5);
            Assert.True(highSum / high > lowSum / low + 0.5f, "the worms of the upper body arrive later than those of the legs: they climb");
            Assert.True(never < 0.3f * plan, $"{never} of {plan} worms have not settled");
            Assert.True(maxHeightFirstSecond < 0.8f, "nobody jumps to the top: the worms enter by the base");
        }

        [Fact]
        public void SomeWormsLetGoAndFallThenClimbAgain()
        {
            const int n = 150;
            GpuSkinSet set = Body(out _);
            WormSimRef sim = Sim(n, set, 1f, 23u);
            var phase = new int[n];
            var fellFrom = new float[n];
            int fell = 0, returned = 0, groundWhileFalling = 0, fallingSamples = 0, samples = 0;
            for (int step = 0; step < 60 * 28; step++)
            {
                sim.Step();
                if (step % 3 != 0) continue;
                samples++;
                for (int w = 0; w < n; w++)
                {
                    int mode = (int)(sim.SkinState[w].x + 0.5f);
                    if (mode == 2 && phase[w] != 1) { if (phase[w] == 2) returned++; phase[w] = 1; }
                    if (mode == 3 && phase[w] == 1) { fell++; phase[w] = 2; fellFrom[w] = sim.Pos[w * K + 3].y; }
                    if (mode == 3 && Math.Abs(sim.Pos[w * K + 3].y - fellFrom[w]) > 0.1f) groundWhileFalling++;
                    if (mode == 3) fallingSamples++;
                }
            }
            float inTheAir = fallingSamples / (float)(samples * sim.Masses[0].Plan.x);
            _out.WriteLine($"{fell} worms let go in 28 s, {returned} of them are settled again; {groundWhileFalling} samples of a falling worm more than 10 cm from where it let go; {inTheAir * 100f:0.0}% of the sleeve is falling at any time");
            Assert.True(fell >= 5, "some worms let go");
            Assert.True(groundWhileFalling > 0, "and really fall");
            Assert.True(returned >= 2, "and climb again");
            Assert.InRange(inTheAir, 0.002f, 0.15f);      // a minority (a fall from the legs is short)
        }


        [Fact]
        public void NoNodeStaysInsideTheBody()
        {
            const int n = 500;
            GpuSkinSet set = Body(out _);
            WormSimRef sim = Sim(n, set, 1f, 9u);
            sim.Step(240);
            int inside = 0;
            for (int w = 0; w < n; w++)
                for (int i = 0; i < K; i++)
                {
                    Vector3 p = P(sim.Pos[w * K + i]);
                    float best = float.MaxValue;
                    int bi = 0;
                    for (int a = 0; a < set.Count; a++)
                    {
                        float d = (P(sim.Anchors[a].A) - p).sqrMagnitude;
                        if (d < best) { best = d; bi = a; }
                    }
                    if (best < 0.06f * 0.06f && Vector3.Dot(p - P(sim.Anchors[bi].A), P(sim.Anchors[bi].B)) < -0.012f) inside++;
                }
            _out.WriteLine($"{inside} of {n * K} nodes more than 1.2 cm behind the surface of their nearest anchor");
            Assert.True(inside < 0.01f * n * K, $"{inside} nodes inside the body");
        }

        [Fact]
        public void TheSleeveFollowsABodyThatMoves()
        {
            const int n = 600;
            GpuSkinSet set = Body(out int[] limbs);
            WormSimRef sim = Sim(n, set, 1f, 13u);
            sim.Step(300);
            // the body walks at 1.2 m/s while the legs and the arms swing (translations of the bones)
            for (int step = 0; step < 300; step++)
            {
                float tt = step / 60f;
                for (int b = 0; b < 8; b++)
                {
                    float swing = b >= 2 && b <= 5 ? 0.10f * Mathf.Sin(tt * 4f + (b % 2) * Mathf.PI) : b >= 6 ? 0.12f * Mathf.Sin(tt * 4f + (b % 2) * Mathf.PI + Mathf.PI) : 0f;
                    sim.Bones[b] = Translation(0f, 0f, swing + 1.2f * tt);
                }
                GpuMassParams m = sim.Masses[0];
                m.Center = new Vector4(0f, 0f, 1.2f * tt, m.Center.w);
                sim.Masses[0] = m;
                sim.SkinAnchors(0);
                sim.Step();
            }
            var anchors = new GpuAnchor[WormSimConst.MaxAnchors];
            Array.Copy(sim.Anchors, anchors, WormSimConst.MaxAnchors);
            var region = new int[WormSimConst.MaxAnchors];
            for (int a = 0; a < set.Count; a++) region[a] = GpuSkinMetrics.RegionOfLimb(limbs[a]);
            var nodes = new Vector4[n * K];
            Array.Copy(sim.Pos, nodes, nodes.Length);
            var state = new Vector4[n];
            Array.Copy(sim.SkinState, state, n);
            GpuSkinMetrics.Result r = GpuSkinMetrics.Measure(nodes, state, anchors, set.Count, region, n);
            _out.WriteLine($"walking: {r.Settled} settled, {r.Near} on the skin ({100f * r.NearShare:0}%), transit {r.Transit}, falling {r.Falling}, pile {r.Pile}");
            Assert.True(r.Settled > 100);
            Assert.True(r.NearShare >= 0.6f, $"{r.NearShare * 100f}% on the skin while the body walks");
        }
    }
}
