#if MW_DEV
using System;
using System.Collections;
using System.Collections.Generic;
using MistWorms.Render.Gpu;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistWorms.Dev
{
    /// <summary>
    /// Dev tooling of the GPU masses (visual v2, pass B1): the "phantom" mass. It is not a game mass: no health, no colony, no ZDO; it only feeds
    /// <see cref="GpuWormHost"/> with the fields a real mass will provide in B2 (centre, heading, speed, radius, engulf, target capsule) so that the simulation and the drawing
    /// can be seen and measured on their own. Shared by the console (<c>mw_gpumass</c>), the bridge (<c>/gpumass</c>, <c>/state</c>) and the scenario V14.
    /// </summary>
    internal static class GpuMassDev
    {
        public enum GhostMode { Still, Walk, Engulf, EngulfPlayer }

        public const int MaxUnits = 240;                   // 2400 worms: the cap of a mass
        public const float WalkSpeed = 1.2f;
        public const float EngulfRamp = 0.5f;                // seconds from 0 to the target fraction
        private const float MannequinHeight = 1.8f, MannequinRadius = 0.3f;

        public static GpuMass Mass { get; private set; }
        public static GhostMode Mode { get; private set; }
        public static int Units { get; private set; }
        public static Vector3 WalkTarget { get; private set; }
        public static bool Arrived { get; private set; }
        public static float EngulfTarget = 1f;
        public static CapsuleCollider Target { get; private set; }

        private static GpuEnvCapture s_env;
        public static GpuEnvCapture Env => s_env;
        private static GameObject s_mannequin;

        /// <summary>Radius of a mass of n game units (the plan: 0.26 m for 10 units, exponent 0.36: 0.46 m for 50, 0.77 m for 200).</summary>
        public static float RadiusOf(int n) => 0.26f * Mathf.Pow(Mathf.Max(n, 1) / 10f, 0.36f);

        public static bool Ready => GpuWormHost.Ready;

        private static bool EnsureHost(out string error)
        {
            error = null;
            if (!GpuWormBundle.InitHost()) { error = GpuWormHost.Error ?? GpuWormBundle.Error ?? "GPU worms unavailable"; return false; }
            if (s_env == null) s_env = GpuWormHost.Environment as GpuEnvCapture ?? new GpuEnvCapture();
            GpuWormHost.Environment = s_env;
            GpuWormHost.PreTick = Drive;
            return true;
        }

        /// <summary>
        /// A phantom mass of <paramref name="units"/> game units (10 worms each) in front of the player (or at <paramref name="at"/>). <see cref="GhostMode.Walk"/> walks it <paramref name="distance"/> metres
        /// ahead, <see cref="GhostMode.Engulf"/> wraps a mannequin capsule standing there, <see cref="GhostMode.EngulfPlayer"/> wraps the player. 0 units clears it.
        /// </summary>
        public static bool Spawn(int units, GhostMode mode, float distance, out string how, Vector3? at = null)
        {
            how = null;
            Clear();
            if (units <= 0) { how = "cleared"; return true; }
            if (!EnsureHost(out string error)) { how = "GPU worms unavailable: " + error; return false; }
            Player player = Player.m_localPlayer;
            if (player == null) { how = "no local player"; return false; }

            units = Mathf.Clamp(units, 1, MaxUnits);
            float r = RadiusOf(units);
            Vector3 fwd = player.transform.forward; fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 start = at ?? player.transform.position + fwd * Mathf.Max(3f, r + 2.5f);
            if (mode == GhostMode.EngulfPlayer) start = player.transform.position;
            start.y = GroundAt(start, player.transform.position.y);

            if (mode == GhostMode.Engulf)
            {
                s_mannequin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                s_mannequin.name = "MW_GpuMannequin";
                s_mannequin.transform.localScale = new Vector3(MannequinRadius * 2f, MannequinHeight * 0.5f, MannequinRadius * 2f);
                s_mannequin.transform.position = start + Vector3.up * (MannequinHeight * 0.5f);
                Target = s_mannequin.GetComponent<CapsuleCollider>();
                int layer = LayerMask.NameToLayer(VanillaNames.LayerCharacterNoenv);
                if (layer >= 0) s_mannequin.layer = layer;
            }
            else if (mode == GhostMode.EngulfPlayer)
            {
                Target = player.GetComponent<CapsuleCollider>();
                if (Target == null) { how = "the player has no capsule collider"; return false; }
            }

            Mass = GpuWormHost.CreateMass(units * 10, start, r, (uint)(units * 2654435761u + 17u));
            if (Mass == null) { how = "the GPU pool is full"; return false; }
            Units = units;
            Mode = mode;
            Mass.Heading = fwd;
            Mass.Speed = 0f;
            Mass.AdhesiveCollider = Target;
            Mass.Mode = Target != null ? GpuMassMode.Engulf : GpuMassMode.Dome;
            Mass.Engulf = 0f;
            WalkTarget = mode == GhostMode.Walk ? start + fwd * Mathf.Max(distance, 0.5f) : start;
            if (mode == GhostMode.Walk) WalkTarget = new Vector3(WalkTarget.x, GroundAt(WalkTarget, start.y), WalkTarget.z);
            Arrived = mode != GhostMode.Walk;
            GpuWormHost.ResetStats();
            how = $"{units} units ({Mass.WormCount} worms, radius {r:0.00} m) {mode} at {start.x:0.0},{start.z:0.0}" + (mode == GhostMode.Walk ? $", walking to {WalkTarget.x:0.0},{WalkTarget.z:0.0}" : string.Empty);
            return true;
        }

        public static void Clear()
        {
            if (Mass != null) GpuWormHost.RemoveMass(Mass);
            Mass = null;
            Target = null;
            if (s_mannequin != null) UnityEngine.Object.Destroy(s_mannequin);
            s_mannequin = null;
            Units = 0;
        }

        /// <summary>Sends the phantom walking to <paramref name="target"/> (world position on the ground).</summary>
        public static void WalkTo(Vector3 target)
        {
            if (Mass == null) return;
            WalkTarget = target;
            Arrived = false;
            Mode = GhostMode.Walk;
        }

        /// <summary>
        /// Height of the centre of a walking mass: the mean of the height grid the simulation sees within its radius (so the dome rises with a rock it climbs and sinks with
        /// a hollow), the terrain when the grid is not there yet.
        /// </summary>
        private static float SmoothGround(GpuMass m, Vector3 c)
        {
            float terrain = GroundAt(c, m.Center.y);
            if (!m.Heights.IsCreated || m.PrimCount < 0 || m.GridSpacing <= 0f) return terrain;
            int hn = WormSimConst.HeightN;
            float sum = 0f;
            int count = 0;
            float r2 = m.Radius * m.Radius;
            for (int z = 0; z < hn; z++)
                for (int x = 0; x < hn; x++)
                {
                    float wx = m.GridMin.x + x * m.GridSpacing - c.x, wz = m.GridMin.y + z * m.GridSpacing - c.z;
                    if (wx * wx + wz * wz > r2) continue;
                    sum += m.Heights[z * hn + x];
                    count++;
                }
            return count > 0 ? Mathf.Max(terrain, sum / count + m.Origin.y) : terrain;
        }

        private static float GroundAt(Vector3 p, float fallback)
        {
            ZoneSystem zones = ZoneSystem.instance;
            return zones != null ? zones.GetGroundHeight(p) : fallback;
        }

        /// <summary>The owner side of the mass, once per frame (called by <see cref="GpuWormHost.Tick"/>).</summary>
        private static void Drive()
        {
            GpuMass m = Mass;
            if (m == null) return;
            float dt = Time.deltaTime;
            switch (Mode)
            {
                case GhostMode.Walk:
                {
                    Vector3 to = WalkTarget - m.Center;
                    to.y = 0f;
                    float dist = to.magnitude;
                    if (dist < 0.05f) { Arrived = true; m.Speed = Mathf.MoveTowards(m.Speed, 0f, 4f * dt); }
                    else
                    {
                        Vector3 dir = to / dist;
                        m.Heading = Vector3.Slerp(m.Heading, dir, Mathf.Clamp01(6f * dt));
                        float want = Mathf.Min(WalkSpeed, dist * 2f + 0.1f);
                        m.Speed = Mathf.MoveTowards(m.Speed, want, 2f * dt);
                        Vector3 step = dir * Mathf.Min(m.Speed * dt, dist);
                        Vector3 c = m.Center + step;
                        c.y = GroundAt(c, m.Center.y);
                        m.Center = c;
                    }
                    break;
                }
                case GhostMode.Engulf:
                case GhostMode.EngulfPlayer:
                {
                    if (Target == null) break;
                    Bounds b = Target.bounds;
                    m.Center = new Vector3(b.center.x, b.min.y, b.center.z);
                    m.Engulf = Mathf.MoveTowards(m.Engulf, EngulfTarget, dt / EngulfRamp);
                    m.ShellThickness = 0.02f;           // one to two worms of thickness on the body; the rest of the worms is the pile on the ground
                    m.Height = b.size.y;
                    break;
                }
            }
        }

        // ---------------------------------------------------------------- state

        public static Dictionary<string, object> StateJson() => new Dictionary<string, object>
        {
            { "ready", GpuWormHost.Ready },
            { "error", GpuWormHost.Ready ? null : (GpuWormHost.Error ?? GpuWormBundle.Error) },
            { "masses", GpuWormHost.Ready ? GpuWormHost.MassCount : 0 },
            { "wormsInUse", GpuWormHost.WormsInUse },
            { "wormsDrawn", GpuWormHost.DrawnWorms },
            { "visibleMasses", GpuWormHost.VisibleMasses },
            { "drawCalls", GpuWormHost.DrawCalls },
            { "triangles", GpuWormHost.Triangles },
            { "shadowTriangles", GpuWormHost.ShadowTriangles },
            { "subSteps", GpuWormHost.LastSubSteps },
            { "stepIndex", (long)GpuWormHost.StepIndex },
            { "frames", GpuWormHost.Frames },
            { "mainThreadMsAvg", GpuWormHost.AvgMs },
            { "mainThreadMsLast", GpuWormHost.LastMs },
            { "mainThreadMsMax", GpuWormHost.MaxMs },
            { "envCaptures", s_env != null ? s_env.Captures : 0 },
            { "envPrimitives", s_env != null ? s_env.LastPrimCount : 0 },
            { "ghost", Mass == null ? null : new Dictionary<string, object>
                {
                    { "units", Units }, { "worms", Mass.WormCount }, { "radius", Mass.Radius }, { "mode", Mode.ToString() },
                    { "center", Mass.Center }, { "speed", Mass.Speed }, { "engulf", Mass.Engulf }, { "arrived", Arrived },
                    { "frozen", Mass.Frozen || !Mass.Simulated }, { "primitives", Mass.PrimCount }, { "origin", Mass.Origin },
                } },
        };

        // ---------------------------------------------------------------- readback

        /// <summary>The nodes of a mass read back from the GPU (<see cref="AsyncGPUReadback"/>), with the environment the simulation had when the request was made.</summary>
        public sealed class NodeSnapshot
        {
            public Vector4[] Nodes;                // local frame
            public float[] Heights;                // local y, HeightN x HeightN
            public Vector2 GridMin;                // world x, z of the sample (0, 0)
            public float Spacing;
            public Vector3 Origin;
            public int Worms, BodyCount, AnchorCount;
            public GpuAnchor[] Anchors;            // skinned on the GPU, local frame
            public Vector4[] State;                // per worm: x mode (0 pile, 1 transit, 2 settled, 3 falling), y anchor of the path, z time in the mode
            public GpuSleevePlan Plan;
            public float SurfaceArea;
            public GpuMassParams Params;
            public float Time;
            public uint MassSeed;
            public Vector3 Right, BodyCenter;
            public GpuPrim[] Prims;
            public bool Done, Error;
            public int Pending;

            internal void Complete() { if (--Pending == 0) Done = true; }

            /// <summary>Height of the ground the simulation used under a node (the same bilinear sample as the compute shader), local y.</summary>
            public float GroundAt(Vector4 node)
            {
                int hn = WormSimConst.HeightN;
                float gx = Mathf.Clamp((node.x + Origin.x - GridMin.x) / Spacing, 0f, 14.999f);
                float gz = Mathf.Clamp((node.z + Origin.z - GridMin.y) / Spacing, 0f, 14.999f);
                int ix = (int)gx, iz = (int)gz;
                float fx = gx - ix, fz = gz - iz;
                float a = Heights[iz * hn + ix] + fx * (Heights[iz * hn + ix + 1] - Heights[iz * hn + ix]);
                float c = Heights[(iz + 1) * hn + ix] + fx * (Heights[(iz + 1) * hn + ix + 1] - Heights[(iz + 1) * hn + ix]);
                return a + fz * (c - a);
            }

            /// <summary>Nearest anchor to a local position: its index (-1 without anchors), the distance to it and the signed distance to its tangent plane (positive outside).</summary>
            public int NearestAnchor(Vector3 p, out float dist, out float plane)
            {
                dist = float.MaxValue;
                plane = 0f;
                int best = -1;
                for (int i = 0; i < AnchorCount; i++)
                {
                    Vector3 d = p - new Vector3(Anchors[i].A.x, Anchors[i].A.y, Anchors[i].A.z);
                    float l2 = d.sqrMagnitude;
                    if (l2 < dist * dist || best < 0)
                    {
                        dist = Mathf.Sqrt(l2);
                        best = i;
                        plane = Vector3.Dot(d, new Vector3(Anchors[i].B.x, Anchors[i].B.y, Anchors[i].B.z));
                    }
                }
                return best;
            }

            /// <summary>The share of the height bands (12) that hold at least two nodes within 4 cm of the baked surface.</summary>
            public float CoverageSkin(float footY, float height, out int attached)
            {
                const int bands = 12;
                var mark = new int[bands];
                attached = 0;
                for (int i = 0; i < Nodes.Length; i++)
                {
                    Vector3 n = new Vector3(Nodes[i].x, Nodes[i].y, Nodes[i].z);
                    if (n.y + Origin.y > footY + height + 0.5f) continue;
                    int a = NearestAnchor(n, out float dist, out float plane);
                    if (a < 0 || dist > 0.1f || Mathf.Abs(plane) > 0.04f) continue;
                    attached++;
                    mark[Mathf.Clamp((int)((n.y + Origin.y - footY) / height * bands), 0, bands - 1)]++;
                }
                int covered = 0;
                for (int b = 0; b < bands; b++) if (mark[b] >= 2) covered++;
                return covered / (float)bands;
            }

            /// <summary>
            /// The sleeve as measured on what the GPU holds (see <see cref="GpuSkinMetrics"/>): the worms by mode, the share of the settled ones on the skin, and where (regions of the
            /// anchor at the middle of the worm: left and right leg, left and right arm, torso and head; by height and by the lateral offset from the axis of the body).
            /// </summary>
            public GpuSkinMetrics.Result SkinSleeve()
            {
                var region = new int[WormSimConst.MaxAnchors];
                GpuSkinMetrics.Regions(Anchors, AnchorCount, Right, BodyCenter - Origin, region);
                return GpuSkinMetrics.Measure(Nodes, State, Anchors, AnchorCount, region, Worms, AnchorCount > 0 ? Mathf.Sqrt(SurfaceArea / AnchorCount) : 0f);
            }

            /// <summary>Lowest and highest point of the anchors (world) and their centre in x and z.</summary>
            public void AnchorExtent(out float minY, out float maxY, out Vector2 centre)
            {
                minY = float.MaxValue;
                maxY = float.MinValue;
                float sx = 0f, sz = 0f;
                for (int a = 0; a < AnchorCount; a++)
                {
                    minY = Mathf.Min(minY, Anchors[a].A.y + Origin.y);
                    maxY = Mathf.Max(maxY, Anchors[a].A.y + Origin.y);
                    sx += Anchors[a].A.x + Origin.x;
                    sz += Anchors[a].A.z + Origin.z;
                }
                centre = AnchorCount > 0 ? new Vector2(sx / AnchorCount, sz / AnchorCount) : Vector2.zero;
            }

            /// <summary>The lowest node above the ground the simulation used (negative: that much below the contact radius is wrong; the contact radius itself is counted in).</summary>
            public float LowestAboveGround(out int below)
            {
                float lowest = float.MaxValue;
                below = 0;
                for (int i = 0; i < Nodes.Length; i++)
                {
                    float margin = Nodes[i].y - GroundAt(Nodes[i]) - WormSimConst.Radius;
                    if (margin < lowest) lowest = margin;
                    if (margin < -0.001f) below++;
                }
                return lowest;
            }

            /// <summary>
            /// Fraction of the height of the wrapped body (12 bands from <paramref name="footY"/> up) that has at least two nodes within 5 cm of the surface of one of the adhesive
            /// capsules (the bone capsules of a humanoid, or the single capsule of the collider).
            /// </summary>
            public float Coverage(float footY, float height, out int attached)
            {
                const int bands = 12;
                var mark = new int[bands];
                attached = 0;
                for (int i = 0; i < Nodes.Length; i++)
                {
                    Vector3 n = new Vector3(Nodes[i].x, Nodes[i].y, Nodes[i].z);
                    bool near = false;
                    for (int k = 0; k < BodyCount && !near; k++) near = WormSimRef.PrimDist(Prims[k], n, out _) < 0.05f;
                    if (!near) continue;
                    attached++;
                    mark[Mathf.Clamp((int)((n.y + Origin.y - footY) / height * bands), 0, bands - 1)]++;
                }
                int covered = 0;
                for (int b = 0; b < bands; b++) if (mark[b] >= 2) covered++;
                return covered / (float)bands;
            }
        }

        /// <summary>Starts the readback of the nodes of <paramref name="m"/>; poll <see cref="NodeSnapshot.Done"/>.</summary>
        public static NodeSnapshot RequestNodes(GpuMass m)
        {
            int hn = WormSimConst.HeightN * WormSimConst.HeightN;
            var snap = new NodeSnapshot
            {
                Nodes = new Vector4[m.WormCount * WormSimConst.Nodes], Heights = new float[hn], GridMin = m.GridMin, Spacing = m.GridSpacing, Origin = m.Origin, Worms = m.ActiveCount,
                Prims = new GpuPrim[m.PrimCount], BodyCount = m.AdhesivePrim >= 0 ? m.AdhesiveCount : 0,
            };
            for (int i = 0; i < m.PrimCount; i++) snap.Prims[i] = m.Prims[i];
            snap.AnchorCount = m.AnchorCount;
            snap.Anchors = new GpuAnchor[Mathf.Max(m.AnchorCount, 1)];
            snap.State = new Vector4[m.WormCount];
            snap.Params = GpuWormHost.BuildParams(m, true);
            snap.Plan = m.SleevePlan;
            snap.SurfaceArea = m.SurfaceArea;
            snap.MassSeed = m.Seed;
            if (Target != null) { snap.Right = Target.transform.right; snap.BodyCenter = Target.bounds.center; }
            for (int i = 0; i < hn; i++) snap.Heights[i] = m.Heights[i];
            snap.Pending = m.AnchorCount > 0 ? 5 : 4;
            // The ground the simulation used, as the GPU holds it when the nodes are read (the grid is recaptured 8 times a second, and the readbacks come a few frames after the request):
            // the height grid and the parameters of the mass (grid rectangle, origin) are read in the same batch as the nodes, so that a node is compared with the ground it was solved against.
            AsyncGPUReadback.Request(GpuWormHost.HeightBuffer, hn * 4, m.Slot * hn * 4, req =>
            {
                if (req.hasError) snap.Error = true;
                else req.GetData<float>().CopyTo(snap.Heights);
                snap.Complete();
            });
            AsyncGPUReadback.Request(GpuWormHost.MassesBuffer, WormSimConst.MassStride, m.Slot * WormSimConst.MassStride, req =>
            {
                if (req.hasError) snap.Error = true;
                else
                {
                    GpuMassParams pr = req.GetData<GpuMassParams>()[0];
                    snap.Params = pr;
                    snap.GridMin = new Vector2(pr.GridRect.x + pr.Origin.x, pr.GridRect.y + pr.Origin.z);
                    snap.Spacing = pr.GridRect.z;
                    snap.Origin = new Vector3(pr.Origin.x, pr.Origin.y, pr.Origin.z);
                }
                snap.Complete();
            });
            AsyncGPUReadback.Request(GpuWormHost.PosBuffer, m.WormCount * WormSimConst.Nodes * 16, m.FirstWorm * WormSimConst.Nodes * 16, req =>
            {
                if (req.hasError) snap.Error = true;
                else req.GetData<Vector4>().CopyTo(snap.Nodes);
                snap.Time = (float)GpuWormHost.StepIndex * WormSimConst.Dt;
                snap.Complete();
            });
            AsyncGPUReadback.Request(GpuWormHost.SkinStateBuffer, m.WormCount * 16, m.FirstWorm * 16, req =>
            {
                if (req.hasError) snap.Error = true;
                else req.GetData<Vector4>().CopyTo(snap.State);
                snap.Complete();
            });
            if (m.AnchorCount > 0)
                AsyncGPUReadback.Request(GpuWormHost.AnchorsBuffer, m.AnchorCount * 32, m.Slot * WormSimConst.MaxAnchors * 32, req =>
                {
                    if (req.hasError) snap.Error = true;
                    else { var d = req.GetData<GpuAnchor>(); for (int i = 0; i < snap.AnchorCount; i++) snap.Anchors[i] = d[i]; }
                    snap.Complete();
                });
            return snap;
        }

        // ---------------------------------------------------------------- parity with the CPU reference

        public sealed class ParityResult
        {
            public bool Done;
            public string Error;
            public float InitMm, MaxMm;
            public int Worms, Steps;
            public string Stages, Env;
        }

        /// <summary>
        /// 64 worms (seed 77) in a dome of radius 2.2 m with a box, a capsule and a sphere, on sloping ground, 30 steps on the GPU; read back and compared with
        /// <see cref="WormSimRef"/> started from the same nodes. The environment is synthetic and written by hand (the parity must not depend on the world).
        /// The phantom mass is removed first and the automatic stepping is paused for the duration.
        /// </summary>
        public static IEnumerator RunParity(ParityResult res)
        {
            res.Worms = 64;
            res.Steps = 30;
            if (!EnsureHost(out string error)) { res.Error = error; res.Done = true; yield break; }
            Clear();
            bool autoStep = GpuWormHost.AutoStep;
            GpuWormHost.AutoStep = false;
            GpuMass m = null;
            try
            {
                Player player = Player.m_localPlayer;
                Vector3 center = player != null ? player.transform.position : Vector3.zero;
                m = GpuWormHost.CreateMass(64, center, 2.2f, 77u);
                if (m == null) { res.Error = "no room in the GPU pool"; res.Done = true; yield break; }
                m.ManualEnvironment = true;
                m.DomeHeight = 0.02f;                // a layer of 5 cm: the worms lie on the ground instead of falling from a metre (the rounding of a fall is amplified by the impacts)
                m.ForceSimulated = true;
                m.Heading = new Vector3(0.6f, 0f, 0.8f);
                m.Speed = 0.6f;
                m.Mode = GpuMassMode.Dome;
                m.Engulf = 0.6f;
                m.AdhesivePrim = 1;

                int hn = WormSimConst.HeightN;
                float ext = m.GridExtent, sp = ext / (hn - 1);
                Vector2 min = new Vector2(center.x - ext * 0.5f, center.z - ext * 0.5f);
                for (int z = 0; z < hn; z++)
                    for (int x = 0; x < hn; x++)
                    {
                        float lx = min.x + x * sp - m.Origin.x, lz = min.y + z * sp - m.Origin.z;
                        m.Heights[z * hn + x] = 0.08f * lx + 0.03f * Mathf.Sin(lz * 5f);
                    }
                m.Prims[0] = GpuPrim.Box(new Vector3(0.35f * 3.333f, 0.15f, 0.0f), new Vector3(0.15f, 0.2f, 0.12f), Quaternion.Euler(0f, 30f, 0f));
                m.Prims[1] = GpuPrim.Capsule(new Vector3(-0.1f * 3.333f, 0.3f, 0.1f * 3.333f), new Vector3(-0.1f * 3.333f, 1.2f, 0.1f * 3.333f), 0.12f, 1f, 0.6f);
                m.Prims[2] = GpuPrim.Sphere(new Vector3(0.0f, 0.1f, -0.4f * 3.333f), 0.1f);
                GpuWormHost.UploadEnvironment(m, 3, min, sp);
                GpuWormHost.InitNow(m);

                NodeSnapshot init = RequestNodes(m);
                while (!init.Done) yield return null;
                if (init.Error) { res.Error = "readback failed"; res.Done = true; yield break; }

                var r = new WormSimRef(64);
                r.Masses[m.Slot] = GpuWormHost.BuildParams(m, true);
                for (int i = 0; i < hn * hn; i++) r.Height[m.Slot * hn * hn + i] = m.Heights[i];
                for (int i = 0; i < 3; i++) r.Prims[m.Slot * WormSimConst.MaxPrims + i] = m.Prims[i];
                for (int i = 0; i < 64; i++) r.Worms[i] = WormSimRef.MakeWorm((uint)m.Slot, WormSimRef.Hash(unchecked(77u * 0x9E3779B1u + (uint)i)));
                r.Init(0, 64);
                float initDiff = 0f;
                for (int i = 0; i < init.Nodes.Length; i++)
                {
                    Vector3 d = new Vector3(init.Nodes[i].x - r.Pos[i].x, init.Nodes[i].y - r.Pos[i].y, init.Nodes[i].z - r.Pos[i].z);
                    initDiff = Mathf.Max(initDiff, d.magnitude);
                    r.Pos[i] = init.Nodes[i];
                    r.Prev[i] = init.Nodes[i];
                }
                res.InitMm = initDiff * 1000f;
                r.StepIndex = GpuWormHost.StepIndex;

                // what the GPU holds of the environment must be what the reference was given (masses, height grid, primitives)
                var gm = new float[WormSimConst.MassStride / 4];
                var gh = new float[hn * hn];
                var gp = new float[WormSimConst.MaxPrims * 16];
                int envDone = 0;
                AsyncGPUReadback.Request(GpuWormHost.MassesBuffer, WormSimConst.MassStride, m.Slot * WormSimConst.MassStride, q => { if (!q.hasError) q.GetData<float>().CopyTo(gm); envDone++; });
                AsyncGPUReadback.Request(GpuWormHost.HeightBuffer, hn * hn * 4, m.Slot * hn * hn * 4, q => { if (!q.hasError) q.GetData<float>().CopyTo(gh); envDone++; });
                AsyncGPUReadback.Request(GpuWormHost.PrimsBuffer, WormSimConst.MaxPrims * 64, m.Slot * WormSimConst.MaxPrims * 64, q => { if (!q.hasError) q.GetData<float>().CopyTo(gp); envDone++; });
                while (envDone < 3) yield return null;
                GpuMassParams pr = r.Masses[m.Slot];
                var want = new float[32];
                Vector4[] rows = { pr.Origin, pr.Center, pr.Heading, pr.Shape, pr.Motion, pr.Sleeve };
                for (int i = 0; i < 6; i++) { want[i * 4] = rows[i].x; want[i * 4 + 1] = rows[i].y; want[i * 4 + 2] = rows[i].z; want[i * 4 + 3] = rows[i].w; }
                want[28] = pr.GridRect.x; want[29] = pr.GridRect.y; want[30] = pr.GridRect.z; want[31] = pr.GridRect.w;
                int badMass = 0, badHeight = 0, badPrim = 0;
                for (int i = 0; i < 24; i++) if (gm[i] != want[i]) badMass++;
                for (int i = 28; i < 32; i++) if (gm[i] != want[i]) badMass++;
                if (BitConverter.ToUInt32(BitConverter.GetBytes(gm[24]), 0) != pr.FirstWorm || BitConverter.ToUInt32(BitConverter.GetBytes(gm[25]), 0) != pr.WormCount || BitConverter.ToUInt32(BitConverter.GetBytes(gm[26]), 0) != pr.PrimCount) badMass++;
                for (int i = 0; i < hn * hn; i++) if (gh[i] != r.Height[m.Slot * hn * hn + i]) badHeight++;
                for (int k = 0; k < 3; k++)
                {
                    GpuPrim rp = r.Prims[m.Slot * WormSimConst.MaxPrims + k];
                    Vector4[] pv = { rp.A, rp.B, rp.C, rp.D };
                    for (int j = 0; j < 4; j++)
                        if (gp[k * 16 + j * 4] != pv[j].x || gp[k * 16 + j * 4 + 1] != pv[j].y || gp[k * 16 + j * 4 + 2] != pv[j].z || gp[k * 16 + j * 4 + 3] != pv[j].w) badPrim++;
                }
                res.Env = $"mismatching values: mass params {badMass}, height grid {badHeight}, primitives {badPrim}; simulated flag {pr.Origin.w}, step index {GpuWormHost.StepIndex}";

                int done = 0;
                var stages = new System.Text.StringBuilder();
                float max = 0f;
                foreach (int target in new[] { 1, 10, 30 })
                {
                    GpuWormHost.StepNow(target - done);
                    r.Step(target - done);
                    done = target;
                    NodeSnapshot snap = RequestNodes(m);
                    while (!snap.Done) yield return null;
                    if (snap.Error) { res.Error = "readback failed"; res.Done = true; yield break; }
                    max = 0f;
                    for (int i = 0; i < snap.Nodes.Length; i++)
                    {
                        Vector3 d = new Vector3(snap.Nodes[i].x - r.Pos[i].x, snap.Nodes[i].y - r.Pos[i].y, snap.Nodes[i].z - r.Pos[i].z);
                        max = Mathf.Max(max, d.magnitude);
                    }
                    stages.Append($"{target} steps {max * 1000f:0.000} mm; ");
                }
                res.Stages = stages.ToString();
                res.MaxMm = max * 1000f;
                res.Done = true;
            }
            finally
            {
                if (m != null) GpuWormHost.RemoveMass(m);
                GpuWormHost.AutoStep = autoStep;
                res.Done = true;
            }
        }
    }
}
#endif
