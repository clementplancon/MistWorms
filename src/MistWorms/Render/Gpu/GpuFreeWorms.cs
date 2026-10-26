using System;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// Visual v2, pass B2 (plan A7): the free worms (the colony: crawling, idle, asleep, rearing, striking; the transients of the masses) drawn as ropes of the GPU colony instead of flipbook
    /// meshes. The CPU keeps everything about the worm (position, heading, state, phase, frame): <see cref="WormRenderer.Sink"/> hands each instance to <see cref="TryAdd"/> with the
    /// flipbook (animation, frame, matrix) it would have drawn, and the rope takes the pose of that frame: the 8 nodes of the centre line of the frame (the formulas of
    /// <c>assets/blender/make_worm.py</c>, which made the flipbook frames, ported node for node: crawl wave and stretch, idle sweep and undulation, rear pitch, strike lunge, sleep curl and
    /// coil), carried by the matrix. So the identifier, the phase and the state of a worm stay what the CPU says, and the look stays that of the flipbooks. The draw goes through the lists
    /// of <see cref="GpuWormHost"/> (one call per level of detail and one for the shadows, for all the free worms and all the masses). Off with <c>Performance.GpuWorms = Off</c>, without
    /// compute, or when the pool of free ropes is full (that worm stays a flipbook).
    /// </summary>
    internal static class GpuFreeWorms
    {
        public const int K = 8;
        private const int Rings = 18;
        private const float BodyLen = 0.30f;

        /// <summary>Dev: every free worm is a flipbook (the comparison of V15).</summary>
        public static bool ForceFlipbook;
        /// <summary>Dev: worms for which this returns true stay flipbooks (a side by side comparison in one picture).</summary>
        public static Func<Vector3, bool> FlipbookAt;

        public static int Taken { get; private set; }
        public static int Refused { get; private set; }
        public static void ResetStats() { Taken = 0; Refused = 0; }

        private static bool s_built;
        private static readonly int[] PoseCount = { 16, 6, 4, 4, 8, 12 };                    // by WormAnim: Crawl, Rear, Strike, Sleep, Idle, Coil
        private static readonly Vector3[][][] s_nodes = new Vector3[6][][];
        private static readonly float[][][] s_scale = new float[6][][];
        private static float s_zc;

        // make_worm.py constants
        private const float HeadInset = 0.0054f;
        private const float CrawlWaveLen = 0.274f, CrawlAmpHead = 0.016f, CrawlAmpTail = 0.035f, CrawlPeriLen = 0.10f, CrawlStretch = 0.06f;
        private const float IdleYawDeg = 15f, IdleWaveLen = 0.312f, IdleWaveAmp = 0.008f, IdleHeadStart = 0.75f, IdleHeadRamp = 0.12f;
        private const float RearS0 = 0.054f, RearW = 0.116f;
        private const float Flat = 0.92f;
        private const float CoilTurn = 4.0f, CoilSGain = 2.0f, CoilYawDeg = 30f, CoilYawU0 = 0.78f, CoilPitchDeg = 12f, CoilPitchU0 = 0.85f, CoilRise = 0.020f;
        // The tube of the colony is at full radius from 12 % of the length; the flipbook worm thins over its first half (make_worm.py PROFILE_R). The ratio of the two radii at the 8 nodes.
        private static readonly float[] ProfileFix = { 0.40f, 0.57f, 0.86f, 0.98f, 1.00f, 1.02f, 1.00f, 1.00f };
        private static readonly float[] ProfileU = { 0f, 1f / 18f, 0.2f, 0.35f, 0.5f, 0.7f, 0.8f, 0.88f, 17f / 18f, 1f };
        private static readonly float[] ProfileR = { 0f, 0.0032f, 0.0064f, 0.0086f, 0.0090f, 0.0092f, 0.0100f, 0.0105f, 0.0086f, 0f };

        /// <summary>True when the free worms can be drawn by the colony (host ready with its pool of free ropes); the caller (<c>GpuMassBridge.FreeActive</c>) also checks the config.</summary>
        public static bool Enabled => !ForceFlipbook && GpuWormHost.FreeReady;

        // ------------------------------------------------------------------ the pose tables

        private static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

        private static float Rb(float u)
        {
            for (int i = 0; i < ProfileU.Length - 1; i++)
                if (u <= ProfileU[i + 1]) return Mathf.Lerp(ProfileR[i], ProfileR[i + 1], (u - ProfileU[i]) / (ProfileU[i + 1] - ProfileU[i]));
            return ProfileR[ProfileR.Length - 1];
        }

        /// <summary>The centre line (Rings + 1 points) of <c>pose()</c> of make_worm.py for the given parameters, and the stretch of every interval (for the thinning).</summary>
        private static void Line(float rear, float lunge, int crawlK, int idleK, float flat, float sink, float curl, Vector3[] P, float[] strain)
        {
            var w = new float[Rings];
            float wsum = 0f;
            for (int i = 0; i < Rings; i++)
            {
                float sMid = (i + 0.5f) / Rings * BodyLen;
                w[i] = crawlK >= 0 ? 1f + CrawlStretch * Mathf.Sin(2f * Mathf.PI * (sMid / CrawlPeriLen - crawlK / 16f)) : 1f;
                wsum += w[i];
            }
            var ds = new float[Rings];
            var s = new float[Rings + 1];
            for (int i = 0; i < Rings; i++) { ds[i] = BodyLen * w[i] / wsum; s[i + 1] = s[i] + ds[i]; }
            var theta = new float[Rings + 1];
            var psi = new float[Rings + 1];
            for (int i = 0; i <= Rings; i++)
            {
                float u = i / (float)Rings;
                theta[i] = rear * (Mathf.PI / 2f) * Smooth((s[i] - RearS0) / RearW);
                if (idleK >= 0) psi[i] = IdleYawDeg * Mathf.Deg2Rad * Mathf.Sin(2f * Mathf.PI * idleK / 8f) * Smooth((u - IdleHeadStart) / IdleHeadRamp);
            }
            float x = 0f, y = 0f, z = 0f;
            P[0] = Vector3.zero;
            for (int i = 0; i < Rings; i++)
            {
                float pm = 0.5f * (psi[i] + psi[i + 1]), tm = 0.5f * (theta[i] + theta[i + 1]);
                x += Mathf.Sin(pm) * Mathf.Cos(tm) * ds[i];
                z += Mathf.Cos(pm) * Mathf.Cos(tm) * ds[i];
                y += Mathf.Sin(tm) * ds[i];
                P[i + 1] = new Vector3(x, y, z);
            }
            for (int i = 0; i <= Rings; i++)
            {
                float u = i / (float)Rings;
                Vector3 p = P[i];
                p.z += lunge * Smooth((s[i] - 0.116f) / 0.156f);
                if (crawlK >= 0)
                {
                    float amp = CrawlAmpHead + (CrawlAmpTail - CrawlAmpHead) * (1f - u);
                    p.x += amp * Mathf.Sin(2f * Mathf.PI * (s[i] / CrawlWaveLen + crawlK / 16f));
                }
                if (idleK >= 0) p.x += IdleWaveAmp * Mathf.Sin(2f * Mathf.PI * (s[i] / IdleWaveLen + idleK / 8f));
                p.x += curl * Mathf.Sin(Mathf.PI * u);
                P[i] = p;
            }
            for (int i = 0; i < Rings; i++) strain[i] = (P[i + 1] - P[i]).magnitude / ds[i];
            for (int i = 0; i <= Rings; i++)
            {
                float u = i / (float)Rings;
                P[i].y += (flat - sink) * Rb(u) * Mathf.Cos(theta[i]);
            }
        }

        /// <summary>The centre line of <c>coil_pose(k)</c>.</summary>
        private static void CoilLine(int k, Vector3[] P, float[] strain)
        {
            float tau = 2f * Mathf.PI * k / 12f;
            float ds = BodyLen / Rings;
            var psi = new float[Rings + 1];
            var theta = new float[Rings + 1];
            float acc = 0f;
            for (int i = 0; i <= Rings; i++)
            {
                float u = i / (float)Rings;
                if (i > 0)
                {
                    float uMid = 0.5f * (u + (i - 1) / (float)Rings);
                    float kappa = CoilTurn / BodyLen * (Mathf.Cos(tau) + CoilSGain * Mathf.Sin(tau) * Mathf.Sin(2f * Mathf.PI * uMid));
                    acc += kappa * ds;
                }
                psi[i] = acc + CoilYawDeg * Mathf.Deg2Rad * Mathf.Sin(2f * tau) * Smooth((u - CoilYawU0) / 0.12f);
                theta[i] = CoilPitchDeg * Mathf.Deg2Rad * Mathf.Sin(2f * tau + 1.2f) * Smooth((u - CoilPitchU0) / 0.10f);
            }
            float x = 0f, y = 0f, z = 0f;
            P[0] = Vector3.zero;
            for (int i = 0; i < Rings; i++)
            {
                float pm = 0.5f * (psi[i] + psi[i + 1]), tm = 0.5f * (theta[i] + theta[i + 1]);
                x += Mathf.Sin(pm) * Mathf.Cos(tm) * ds;
                z += Mathf.Cos(pm) * Mathf.Cos(tm) * ds;
                y += Mathf.Sin(tm) * ds;
                P[i + 1] = new Vector3(x, y, z);
            }
            for (int i = 0; i <= Rings; i++)
            {
                float u = i / (float)Rings;
                P[i].y += CoilRise * Mathf.Sin(Mathf.PI * u) * (0.6f + 0.4f * Mathf.Sin(tau)) + Flat * Rb(u) * Mathf.Cos(theta[i]);
            }
            for (int i = 0; i < Rings; i++) strain[i] = 1f;
        }

        private static void Build()
        {
            s_built = true;
            var P = new Vector3[Rings + 1];
            var strain = new float[Rings];
            // origin of the flipbooks: the same shift in z for every frame (mean of the straight worm), the XZ centroid of each coil frame and the belly of the whole coil loop on the ground
            Line(0f, 0f, -1, -1, Flat, 0f, 0f, P, strain);
            float zc = 0f;
            for (int i = 0; i <= Rings; i++) zc += P[i].z;
            s_zc = zc / (Rings + 1);

            for (int a = 0; a < 6; a++)
            {
                s_nodes[a] = new Vector3[PoseCount[a]][];
                s_scale[a] = new float[PoseCount[a]][];
            }
            // the coil: the lowest point of the belly over the loop is the ground
            float ymin = float.MaxValue;
            for (int k = 0; k < 12; k++)
            {
                CoilLine(k, P, strain);
                for (int i = 0; i <= Rings; i++) ymin = Mathf.Min(ymin, P[i].y - Flat * Rb(i / (float)Rings));
            }
            for (int a = 0; a < 6; a++)
                for (int k = 0; k < PoseCount[a]; k++)
                {
                    var anim = (WormAnim)a;
                    float pulse = 1f;
                    Vector3 shift;
                    switch (anim)
                    {
                        case WormAnim.Crawl: Line(0f, 0f, k, -1, Flat, 0f, 0f, P, strain); shift = new Vector3(0f, 0f, s_zc); break;
                        case WormAnim.Idle: Line(0f, 0f, -1, k, Flat, 0f, 0f, P, strain); shift = new Vector3(0f, 0f, s_zc); break;
                        case WormAnim.Rear: Line(k / 5f, 0f, -1, -1, Flat, 0f, 0f, P, strain); shift = new Vector3(0f, 0f, s_zc); break;
                        case WormAnim.Strike: { float e = k / 3f; Line(0.85f * (1f - e), 0.116f * e, -1, -1, Flat, 0f, 0f, P, strain); shift = new Vector3(0f, 0f, s_zc); break; }
                        case WormAnim.Sleep:
                            // the flipbook is flattened (belly 0.62): a round tube is thinned to the mean of the two
                            pulse = (1f + 0.05f * Mathf.Sin(2f * Mathf.PI * k / 4f)) * 0.80f;
                            Line(0f, 0f, -1, -1, 0.62f, 0.28f, 0.024f, P, strain);
                            shift = new Vector3(0f, 0f, s_zc);
                            break;
                        default:
                        {
                            CoilLine(k, P, strain);
                            float cx = 0f, cz = 0f;
                            for (int i = 0; i <= Rings; i++) { cx += P[i].x; cz += P[i].z; }
                            shift = new Vector3(cx / (Rings + 1), ymin, cz / (Rings + 1));
                            break;
                        }
                    }
                    var nodes = new Vector3[K];
                    var scale = new float[K];
                    for (int n = 0; n < K; n++)
                    {
                        float f = n / (float)(K - 1) * Rings;
                        int i0 = Mathf.Min((int)f, Rings - 1);
                        float t = f - i0;
                        nodes[n] = Vector3.Lerp(P[i0], P[i0 + 1], t) - shift;
                        nodes[n].x = -nodes[n].x;                 // glTF to Unity: the loader of the flipbooks negates x (right handed to left handed)
                        float st = Mathf.Lerp(strain[Mathf.Max(i0 - (t < 0.5f ? 1 : 0), 0)], strain[Mathf.Min(i0 + (t >= 0.5f ? 1 : 0), Rings - 1)], 0.5f);
                        scale[n] = pulse * Mathf.Pow(Mathf.Max(st, 0.2f), -0.5f) * ProfileFix[n];
                    }
                    s_nodes[a][k] = nodes;
                    s_scale[a][k] = scale;
                }
        }

        /// <summary>The 8 nodes (local, tail first) and the radius scales of a frame of a flipbook animation, for the tests and the editor check.</summary>
        public static void Pose(WormAnim anim, int frame, out Vector3[] nodes, out float[] scale)
        {
            if (!s_built) Build();
            int a = (int)anim;
            int k = ((frame % PoseCount[a]) + PoseCount[a]) % PoseCount[a];
            nodes = s_nodes[a][k];
            scale = s_scale[a][k];
        }

        public static int Frames(WormAnim anim) => PoseCount[(int)anim];

        // ------------------------------------------------------------------ the hook of the renderer

        private static readonly Vector3[] s_world = new Vector3[K];
        private static readonly float[] s_sc = new float[K];

        /// <summary>
        /// Takes the instance a flipbook would have drawn (<paramref name="frame"/> in the numbering of the effective LOD, as <see cref="WormRenderer.Sink.AddWorm(WormAnim, WormLod, int, in Matrix4x4)"/>)
        /// as a rope of the colony. False when the instance stays a flipbook (disabled, culled by the renderer's own test is not done here: the caller culls first).
        /// </summary>
        public static bool TryAdd(WormAnim anim, WormLod lod, int frame, in Matrix4x4 trs, Vector3 cam)
        {
            if (!Enabled) return false;
            if (!s_built) Build();
            Vector3 pos = new Vector3(trs.m03, trs.m13, trs.m23);
            if (FlipbookAt != null && FlipbookAt(pos)) return false;
            int a = (int)anim;
            // frame numbering: LOD1 of crawl, idle and coil has half the frames, frame j being pose 2j
            int pose = lod == WormLod.Lod1 && (anim == WormAnim.Crawl || anim == WormAnim.Idle || anim == WormAnim.Coil) ? frame * 2 : frame;
            pose = ((pose % PoseCount[a]) + PoseCount[a]) % PoseCount[a];
            Vector3[] nodes = s_nodes[a][pose];
            float[] scale = s_scale[a][pose];
            float size = new Vector3(trs.m00, trs.m10, trs.m20).magnitude;
            for (int n = 0; n < K; n++)
            {
                s_world[n] = trs.MultiplyPoint3x4(nodes[n]);
                s_sc[n] = scale[n] * size;
            }
            float d = (pos - cam).magnitude;
            int level = d < GpuWormHost.LodDistances[0] ? 0 : d < GpuWormHost.LodDistances[1] ? 1 : 2;
            bool ok = GpuWormHost.FreeAdd(level, s_world, s_sc);
            if (ok) Taken++; else Refused++;
            return ok;
        }
    }
}
