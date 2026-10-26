using System;
using System.Diagnostics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistWorms.Render.Gpu
{
    /// <summary>What the host needs from the shader bundle (loaded by <see cref="GpuWormBundle"/>, or by the editor check of the Unity project).</summary>
    internal sealed class GpuWormAssets
    {
        public ComputeShader Compute;
        /// <summary>The material of MistWorms/WormRope, textures set; the host clones it once per draw.</summary>
        public Material RopeMaterial;
    }

    /// <summary>Source of the environment of the masses (height grid and primitives), driven by the host once per frame and per mass; see <see cref="GpuEnvCapture"/>.</summary>
    internal interface IGpuEnvironment
    {
        /// <summary>Refreshes the environment of the mass when it is due (5 to 10 Hz), or at once when <paramref name="force"/> (before the first step, after a re-centring).</summary>
        void Tick(GpuMass mass, float now, bool force);
    }

    /// <summary>
    /// One mass on the GPU: a segment of worms in the shared buffers and its parameters. The owner (the game later, the dev phantom now) writes the public fields every frame;
    /// the host turns them into <see cref="GpuMassParams"/>. Positions are world positions; the simulation runs in a local frame at <see cref="Origin"/>.
    /// </summary>
    internal sealed class GpuMass
    {
        internal int Slot;
        public int FirstWorm, WormCount;            // the segment of the pool the mass owns (its capacity)
        /// <summary>Worms alive (visual worms of the mass, 10 per game unit): the others of the segment shrink to nothing in 0.5 s and are not drawn. Set with <see cref="GpuWormHost.SetActive"/>.</summary>
        public int ActiveCount;
        /// <summary>Worms in the draw lists: the alive ones, and the dying ones until they are gone.</summary>
        public int DrawEnd;
        public float ShrinkUntil;
        public uint Seed;
        public bool Active;
        /// <summary>The pool of free ropes (flipbook worms drawn as ropes): written by the CPU every frame, never simulated.</summary>
        public bool IsFree;
        public bool NeedsInit;
        public bool EnvForce;
        /// <summary>The owner writes the environment itself (parity check): the host never asks the <see cref="IGpuEnvironment"/> for this mass.</summary>
        public bool ManualEnvironment;

        // owner side
        public Vector3 Center;                 // centre of the base of the dome, on the ground
        public Vector3 Heading = Vector3.forward;
        public float Speed;
        public float Radius = 0.5f;
        public float DomeHeight = 0.45f;       // height of the dome over its radius (the plan: 0.45)
        public float ShapeAlong, ShapeAcross;  // half axes of the footprint along the heading and across it (0 = the dome of radius r): a mass that flows through a passage is a ribbon
        public float Stream;                   // 0 dome .. 1 ribbon (the footprint is the lerp, set by the owner)
        public float Height;                   // height of what the mass wraps (0 = nothing): part of the volume used for the LOD and the culling
        public float Engulf;                   // 0..1, covered fraction of the height of the adhesive capsule
        public GpuMassMode Mode;
        public float ShellThickness = 0.04f;
        public int AdhesiveCount = 1;          // number of adhesive capsules, the first at index AdhesivePrim (a humanoid has up to 8: pelvis, torso, head, thighs, arms)
        public int AdhesivePrim = -1;          // index of the adhesive capsule among the primitives (set by the environment capture)
        public float Conveyor = 0.8f;
        public float CreepScale = 1f;
        public float HomePull = 0.02f;
        public float GroundFriction = 0.6f;
        public bool ForceSimulated;            // simulated whatever the distance to the camera (the parity check)
        public bool Frozen;                    // not simulated (the host also freezes the masses beyond FreezeDistance)
        public Transform Owner;                // the game object of the mass: its own colliders are not obstacles of its worms
        public Collider AdhesiveCollider;      // the engulfed being: its capsule becomes the adhesive primitive (covered fraction = Engulf)

        // host side
        public Vector3 Origin;                 // world position of the local frame
        public float LodDistance;              // distance from the camera to the nearest point of the volume of the mass at the last draw, metres
        public Vector3 VolumeCenter, VolumeExtents;
        public int PrimCount;
        public Vector2 GridMin;                // world x, z of the sample (0, 0) of the height grid
        public float GridSpacing = 1f;
        public float NextEnvTime;
        public bool Simulated;
        public bool Visible;
        public int Lod;

        /// <summary>Environment written by <see cref="IGpuEnvironment"/> in the local frame, then published with <see cref="GpuWormHost.UploadEnvironment"/>.</summary>
        public NativeArray<float> Heights;      // HeightN * HeightN, local y
        public NativeArray<GpuPrim> Prims;      // MaxPrims, local frame
        /// <summary>The static data of the anchors on the surface of the engulfed target, uploaded by the skin sampler (<see cref="GpuWormHost.UploadSkin"/>); the anchors are skinned on the GPU.</summary>
        public GpuSkinSet SkinSet;
        /// <summary>Depth maps of the obstacles (5 maps of 16 x 16, local frame; see SolidPush in WormSim.compute), written by the environment capture.</summary>
        public NativeArray<float> Sides;
        public bool SideEnabled;
        public float SideYMin, SideYStep = 0.3f;     // world height of the first row of the side maps, spacing of the rows
        public int AnchorCount;
        /// <summary>The sleeve of the skin engulf as the last <see cref="GpuWormHost.BuildParams"/> planned it (S, covered height...).</summary>
        public GpuSleevePlan SleevePlan;
        public float TargetHeight, SurfaceArea;

        public float GridExtent => 2f * (Radius + 0.6f);
    }

    /// <summary>
    /// Host of the GPU worm masses (visual v2, pass B1): preallocated buffers, one compute simulation for every mass, fixed step of 1/60 s (3 sub-steps at most),
    /// per mass parameters, and the drawing of the ropes: one <c>Graphics.RenderMeshIndirect</c> per level of detail (and one for the shadows of LOD0) for all the masses.
    /// Nothing is allocated per frame. Without a compute shader or a shader that is supported <see cref="TryInit"/> fails once with a warning and the callers keep the CPU path.
    /// It is not wired into the gameplay yet: only the dev phantom mass uses it.
    /// </summary>
    internal static class GpuWormHost
    {
        public const float FreezeDistance = 35f;
        public const float DrawDistance = 80f;
        public static readonly float[] LodDistances = { 8f, 25f };      // LOD0 below 8 m, LOD1 up to 25 m, LOD2 beyond

        private static readonly int PosId = Shader.PropertyToID("_Pos"), PrevId = Shader.PropertyToID("_Prev"), PredId = Shader.PropertyToID("_Pred"),
            DeltaId = Shader.PropertyToID("_Delta"), GridId = Shader.PropertyToID("_Grid"), WormsId = Shader.PropertyToID("_Worms"),
            MassesId = Shader.PropertyToID("_Masses"), HeightId = Shader.PropertyToID("_Height"), PrimsId = Shader.PropertyToID("_Prims"), AnchorsId = Shader.PropertyToID("_Anchors"), SidesId = Shader.PropertyToID("_Sides"),
            WormCountId = Shader.PropertyToID("_WormCount"), StepIndexId = Shader.PropertyToID("_StepIndex"), InitFirstId = Shader.PropertyToID("_InitFirst"),
            InitCountId = Shader.PropertyToID("_InitCount"), InitScaleId = Shader.PropertyToID("_InitScale"), ShiftId = Shader.PropertyToID("_Shift"),
            AnchorsOutId = Shader.PropertyToID("_AnchorsOut"), SkinVertsId = Shader.PropertyToID("_SkinVerts"), BonesId = Shader.PropertyToID("_Bones"), KnnId = Shader.PropertyToID("_Knn"),
            OrderId = Shader.PropertyToID("_Order"), HopId = Shader.PropertyToID("_Hop"), SkinStateId = Shader.PropertyToID("_SkinState"), NodeAnchorId = Shader.PropertyToID("_NodeAnchor"),
            NodesId = Shader.PropertyToID("_Nodes"), DrawListId = Shader.PropertyToID("_DrawList"), WormDataId = Shader.PropertyToID("_WormData"), MassDataId = Shader.PropertyToID("_MassData");

        private static ComputeShader s_cs;
        private static int kInit, kClear, kPredict, kSeparate, kSolve, kShift, kSkin;
        private static GraphicsBuffer s_pos, s_prev, s_pred, s_delta, s_grid, s_worms, s_masses, s_height, s_prims, s_anchors, s_sides, s_args;
        private static GraphicsBuffer s_skinVerts, s_bones, s_knn, s_order, s_hop, s_skinState, s_nodeAnchor;
        private static readonly GraphicsBuffer[] s_lists = new GraphicsBuffer[4];
        private static NativeArray<GpuMassParams> s_massParams;
        private static NativeArray<GpuWormData> s_wormData;
        private static readonly NativeArray<uint>[] s_listData = new NativeArray<uint>[4];
        private static NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs> s_argData;
        private static readonly int[] s_listCount = new int[4];
        private static readonly Mesh[] s_meshes = new Mesh[GpuWormTube.Lods];
        private static readonly Material[] s_materials = new Material[4];
        private static readonly GpuMass[] s_slots = new GpuMass[WormSimConst.MaxMasses + 1];
        private static readonly Plane[] s_planes = new Plane[6];
        private static GameObject s_driver;
        private static Bounds s_drawBounds;
        private static int s_signature;
        private static float s_accum;
        private static bool s_warned;

        public static bool Ready { get; private set; }
        public static string Error { get; private set; }
        public static IGpuEnvironment Environment;
        public static Func<Camera> CameraProvider;
        /// <summary>Position of the camera the last draw was prepared for.</summary>
        public static Vector3 LastCameraPosition { get; private set; }
        public static Action<string> Log = _ => { };
        public static Action<string> LogWarning = _ => { };
        /// <summary>Called at the start of every <see cref="Tick"/> (inside its measured cost): the owners of the masses update their fields here.</summary>
        public static Action PreTick;

        /// <summary>Simulation and draw switch (the dev tools and the parity check pause the automatic stepping).</summary>
        public static bool AutoStep = true;
        public static bool DrawEnabled = true;
        /// <summary>Steps dispatched since the start: the clock of the creep waves.</summary>
        public static uint StepIndex { get; private set; }
        public static int WormsInUse { get; private set; }

        /// <summary>Resets the clock of the creep waves (the parity check runs the same steps twice).</summary>
        internal static void ResetStepIndex() => StepIndex = 0;

        // ---- main thread cost of Tick (environment capture, upload, dispatches, submission)
        public static double LastMs { get; private set; }
        public static double MaxMs { get; private set; }
        public static int Frames { get; private set; }
        private static double s_sumMs;
        public static double AvgMs => Frames > 0 ? s_sumMs / Frames : 0.0;
        public static int DrawCalls { get; private set; }
        public static int DrawnWorms { get; private set; }
        public static long Triangles { get; private set; }
        public static long ShadowTriangles { get; private set; }
        public static int LastSubSteps { get; private set; }
        public static int VisibleMasses { get; private set; }
        /// <summary>When true each Tick also sums the rises of the managed heap seen around itself (adds its own cost: measure the timing apart).</summary>
        public static bool MeasureAlloc;
        public static long OwnAllocBytes;

        public static GraphicsBuffer PosBuffer => s_pos;
        internal static GraphicsBuffer AnchorsBuffer => s_anchors;
        internal static GraphicsBuffer SkinStateBuffer => s_skinState;
        internal static GraphicsBuffer PrevBuffer => s_prev;
        internal static GraphicsBuffer NodeAnchorBuffer => s_nodeAnchor;
        internal static GraphicsBuffer SidesBuffer => s_sides;
        internal static GraphicsBuffer PrimsBuffer => s_prims;
        internal static GraphicsBuffer HeightBuffer => s_height;
        internal static GraphicsBuffer MassesBuffer => s_masses;

        public static void ResetStats()
        {
            LastMs = 0; MaxMs = 0; Frames = 0; s_sumMs = 0; OwnAllocBytes = 0;
        }

        // ---------------------------------------------------------------- init

        /// <summary>Allocates everything. Never throws: false with <see cref="Error"/> set (a warning is logged once). Idempotent.</summary>
        public static bool TryInit(GpuWormAssets assets, bool createDriver)
        {
            if (Ready) return true;
            if (Error != null) return false;
            try
            {
                if (!SystemInfo.supportsComputeShaders) return Fail("compute shaders are not supported on this device");
                if (assets == null || assets.Compute == null || assets.RopeMaterial == null) return Fail("compute shader or rope material missing from the bundle");
                if (!assets.RopeMaterial.shader.isSupported) return Fail("the WormRope shader is not supported on this device");
                if (!SystemInfo.supportsInstancing) return Fail("GPU instancing is not supported on this device");

                s_cs = assets.Compute;
                kInit = s_cs.FindKernel("KInit"); kClear = s_cs.FindKernel("KClear"); kPredict = s_cs.FindKernel("KPredict");
                kSeparate = s_cs.FindKernel("KSeparate"); kSolve = s_cs.FindKernel("KSolve"); kShift = s_cs.FindKernel("KShift"); kSkin = s_cs.FindKernel("KSkin");

                int worms = WormSimConst.MaxWorms, nodes = worms * WormSimConst.Nodes, masses = WormSimConst.MaxMasses;
                s_pos = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nodes, 16);
                s_prev = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nodes, 16);
                s_pred = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nodes, 16);
                s_delta = new GraphicsBuffer(GraphicsBuffer.Target.Structured, worms * WormSimConst.PointStride, 16);
                s_grid = new GraphicsBuffer(GraphicsBuffer.Target.Structured, WormSimConst.GridCells * WormSimConst.GridSlots, 4);
                s_worms = new GraphicsBuffer(GraphicsBuffer.Target.Structured, worms, 16);
                s_masses = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses + 1, WormSimConst.MassStride);
                s_height = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.HeightN * WormSimConst.HeightN, 4);
                s_prims = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxPrims, 64);
                s_anchors = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxAnchors, 32);
                s_sides = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.SideMaps * WormSimConst.SideMapSize, 4);
                s_skinVerts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxAnchors, 80);
                s_bones = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxBones, 64);
                s_knn = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxAnchors * WormSimConst.NeighbourCount, 4);
                s_order = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxAnchors, 4);
                s_hop = new GraphicsBuffer(GraphicsBuffer.Target.Structured, masses * WormSimConst.MaxAnchors * WormSimConst.HopWords, 4);
                s_skinState = new GraphicsBuffer(GraphicsBuffer.Target.Structured, worms, 16);
                s_nodeAnchor = new GraphicsBuffer(GraphicsBuffer.Target.Structured, nodes, 4);
                s_args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 4, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                s_massParams = new NativeArray<GpuMassParams>(masses + 1, Allocator.Persistent);
                s_wormData = new NativeArray<GpuWormData>(worms, Allocator.Persistent);
                s_argData = new NativeArray<GraphicsBuffer.IndirectDrawIndexedArgs>(4, Allocator.Persistent);
                ClearMasses();

                for (int i = 0; i < 4; i++)
                {
                    s_lists[i] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, worms, 4);
                    s_listData[i] = new NativeArray<uint>(worms, Allocator.Persistent);
                }
                for (int l = 0; l < GpuWormTube.Lods; l++) s_meshes[l] = GpuWormTube.Build(l);
                for (int i = 0; i < 4; i++)
                {
                    s_materials[i] = new Material(assets.RopeMaterial) { name = "MW_WormRope_" + i, enableInstancing = true, hideFlags = HideFlags.DontUnloadUnusedAsset };
                    s_materials[i].SetBuffer(NodesId, s_pos);
                    s_materials[i].SetBuffer(WormDataId, s_worms);
                    s_materials[i].SetBuffer(MassDataId, s_masses);
                    s_materials[i].SetBuffer(DrawListId, s_lists[i]);
                }

                int[] kernels = { kInit, kClear, kPredict, kSeparate, kSolve, kShift, kSkin };
                foreach (int k in kernels)
                {
                    s_cs.SetBuffer(k, PosId, s_pos); s_cs.SetBuffer(k, PrevId, s_prev); s_cs.SetBuffer(k, PredId, s_pred);
                    s_cs.SetBuffer(k, DeltaId, s_delta); s_cs.SetBuffer(k, GridId, s_grid);
                    s_cs.SetBuffer(k, WormsId, s_worms); s_cs.SetBuffer(k, MassesId, s_masses);
                    s_cs.SetBuffer(k, HeightId, s_height); s_cs.SetBuffer(k, PrimsId, s_prims); s_cs.SetBuffer(k, AnchorsId, s_anchors); s_cs.SetBuffer(k, SidesId, s_sides);
                    s_cs.SetBuffer(k, AnchorsOutId, s_anchors); s_cs.SetBuffer(k, SkinVertsId, s_skinVerts); s_cs.SetBuffer(k, BonesId, s_bones); s_cs.SetBuffer(k, KnnId, s_knn);
                    s_cs.SetBuffer(k, OrderId, s_order); s_cs.SetBuffer(k, HopId, s_hop); s_cs.SetBuffer(k, SkinStateId, s_skinState); s_cs.SetBuffer(k, NodeAnchorId, s_nodeAnchor);
                }

                if (createDriver)
                {
                    s_driver = new GameObject("MW_GpuWormHost") { hideFlags = HideFlags.HideAndDontSave };
                    UnityEngine.Object.DontDestroyOnLoad(s_driver);
                    s_driver.AddComponent<GpuWormHostDriver>();
                }
                Ready = true;
                Log($"[GpuWormHost] ready: {WormSimConst.MaxWorms} worms, {WormSimConst.MaxMasses} masses, grid {WormSimConst.GridCells} cells x {WormSimConst.GridSlots}");
                return true;
            }
            catch (Exception e)
            {
                Shutdown();
                return Fail(e.Message);
            }
        }

        private static void ClearMasses()
        {
            for (int i = 0; i < s_massParams.Length; i++) s_massParams[i] = default;
            s_masses.SetData(s_massParams);
        }

        private static bool Fail(string why)
        {
            Error = why;
            if (!s_warned)
            {
                s_warned = true;
                LogWarning("[GpuWormHost] GPU worms unavailable, the CPU path is kept: " + why);
            }
            return false;
        }

        public static void Shutdown()
        {
            s_free = null;
            for (int l = 0; l < 3; l++) if (s_freeStage[l].IsCreated) s_freeStage[l].Dispose();
            for (int i = 0; i < s_slots.Length; i++) if (s_slots[i] != null) RemoveMass(s_slots[i]);
            Ready = false;
            if (s_driver != null) UnityEngine.Object.Destroy(s_driver);
            s_driver = null;
            foreach (GraphicsBuffer b in new[] { s_pos, s_prev, s_pred, s_delta, s_grid, s_worms, s_masses, s_height, s_prims, s_anchors, s_sides, s_args, s_skinVerts, s_bones, s_knn, s_order, s_hop, s_skinState, s_nodeAnchor }) b?.Release();
            s_pos = s_prev = s_pred = s_delta = s_grid = s_worms = s_masses = s_height = s_prims = s_anchors = s_sides = s_args = null;
            s_skinVerts = s_bones = s_knn = s_order = s_hop = s_skinState = s_nodeAnchor = null;
            for (int i = 0; i < 4; i++)
            {
                s_lists[i]?.Release(); s_lists[i] = null;
                if (s_listData[i].IsCreated) s_listData[i].Dispose();
                if (s_materials[i] != null) UnityEngine.Object.Destroy(s_materials[i]);
                s_materials[i] = null;
            }
            for (int l = 0; l < s_meshes.Length; l++) { if (s_meshes[l] != null) UnityEngine.Object.Destroy(s_meshes[l]); s_meshes[l] = null; }
            if (s_massParams.IsCreated) s_massParams.Dispose();
            if (s_wormData.IsCreated) s_wormData.Dispose();
            if (s_argData.IsCreated) s_argData.Dispose();
        }

        // ---------------------------------------------------------------- masses

        public static GpuMass[] Slots => s_slots;

        public static int MassCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < s_slots.Length; i++) if (s_slots[i] != null && !s_slots[i].IsFree) n++;
                return n;
            }
        }

        /// <summary>
        /// Registers a mass of <paramref name="wormCount"/> worms (visual: 10 per game unit, at most 2400) in the shared buffers; null when the pool is full.
        /// The worms are placed by the first <see cref="Tick"/> (or by <see cref="InitNow"/>) once the environment is known.
        /// </summary>
        public static GpuMass CreateMass(int wormCount, Vector3 center, float radius, uint seed) => CreateMass(wormCount, wormCount, center, radius, seed);

        /// <summary>
        /// A mass with <paramref name="active"/> worms alive in a segment of <paramref name="capacity"/>: the count can follow the game count afterwards (<see cref="SetActive"/>)
        /// up to the capacity, a worm born or dying growing or shrinking in 0.5 s.
        /// </summary>
        public static GpuMass CreateMass(int active, int capacity, Vector3 center, float radius, uint seed)
        {
            if (!Ready) return null;
            int wormCount = Mathf.Clamp(capacity, 1, 2400);
            active = Mathf.Clamp(active, 1, wormCount);
            int slot = -1;
            for (int i = 0; i < WormSimConst.MaxMasses; i++) if (s_slots[i] == null) { slot = i; break; }
            if (slot < 0) return null;
            int first = AllocWorms(wormCount);
            if (first < 0) return null;

            var m = new GpuMass
            {
                Slot = slot, FirstWorm = first, WormCount = wormCount, ActiveCount = active, DrawEnd = active, Seed = seed, Active = true, NeedsInit = true, EnvForce = true,
                Center = center, Radius = radius, Origin = new Vector3(Mathf.Round(center.x), Mathf.Round(center.y), Mathf.Round(center.z)),
                Heights = new NativeArray<float>(WormSimConst.HeightN * WormSimConst.HeightN, Allocator.Persistent),
                Prims = new NativeArray<GpuPrim>(WormSimConst.MaxPrims, Allocator.Persistent),
                Sides = new NativeArray<float>(WormSimConst.SideMaps * WormSimConst.SideMapSize, Allocator.Persistent),
            };
            for (int i = 0; i < m.Heights.Length; i++) m.Heights[i] = center.y - m.Origin.y;
            m.GridMin = new Vector2(center.x - m.GridExtent * 0.5f, center.z - m.GridExtent * 0.5f);
            m.GridSpacing = m.GridExtent / (WormSimConst.HeightN - 1);
            s_slots[slot] = m;

            for (int i = 0; i < wormCount; i++)
                s_wormData[first + i] = WormSimRef.MakeWorm((uint)slot, WormSimRef.Hash(unchecked(seed * 0x9E3779B1u + (uint)i)));
            s_worms.SetData(s_wormData, first, first, wormCount);
            UpdateHigh();
            s_signature = 0;
            return m;
        }

        public static void RemoveMass(GpuMass m)
        {
            if (m == null || !m.Active) return;
            m.Active = false;
            s_slots[m.Slot] = null;
            if (s_massParams.IsCreated)
            {
                s_massParams[m.Slot] = default;
                s_masses.SetData(s_massParams, m.Slot, m.Slot, 1);
            }
            if (m.Heights.IsCreated) m.Heights.Dispose();
            if (m.Prims.IsCreated) m.Prims.Dispose();
            if (m.Sides.IsCreated) m.Sides.Dispose();
            UpdateHigh();
            s_signature = 0;
        }

        /// <summary>
        /// Follows the count of the game: more worms are born inside the mass (placed at their home point, radius scale 0, growing to 1 in 0.5 s), fewer are absorbed (shrinking to
        /// nothing in 0.5 s). Within the capacity of the segment; true when the count could be set as asked (false: capped at the capacity).
        /// </summary>
        public static bool SetActive(GpuMass m, int active)
        {
            int cap = m.WormCount;
            bool fits = active <= cap;
            active = Mathf.Clamp(active, 1, cap);
            if (active == m.ActiveCount) return fits;
            if (active > m.ActiveCount && Ready && !m.NeedsInit)
            {
                s_cs.SetInt(InitFirstId, m.FirstWorm + m.ActiveCount);
                s_cs.SetInt(InitCountId, active - m.ActiveCount);
                s_cs.SetFloat(InitScaleId, 0f);
                s_cs.SetInt(StepIndexId, (int)StepIndex);
                s_cs.Dispatch(kInit, (active - m.ActiveCount + 63) / 64, 1, 1);
                s_cs.SetFloat(InitScaleId, 1f);
            }
            if (active < m.ActiveCount) m.ShrinkUntil = Time.realtimeSinceStartup + WormSimConst.BirthTime + 0.1f;
            else m.DrawEnd = active;
            m.ActiveCount = active;
            s_signature = 0;
            return fits;
        }

        // ---------------------------------------------------------------- the free ropes

        private static GpuMass s_free;
        private static readonly NativeArray<Vector4>[] s_freeStage = new NativeArray<Vector4>[3];
        private static readonly int[] s_freeCount = new int[3];
        private static Vector3 s_freeOrigin;
        private static int s_freeSig;

        /// <summary>True when the pool of free ropes exists (the host is ready and <see cref="EnsureFree"/> ran).</summary>
        public static bool FreeReady => Ready && s_free != null;

        /// <summary>Counts of the free ropes drawn last frame, by level of detail (dev checks).</summary>
        public static int FreeDrawn(int lod) => s_freeCount[lod];

        /// <summary>Allocates the segment of the pool the free ropes live in (3 x <see cref="WormSimConst.FreeCapPerLod"/>), once. False when the pool is full.</summary>
        public static bool EnsureFree()
        {
            if (!Ready) return false;
            if (s_free != null) return true;
            int cap = WormSimConst.FreeCapPerLod, total = 3 * cap;
            int first = AllocWorms(total);
            if (first < 0) { LogWarning("[GpuWormHost] no room for the free ropes in the pool: the free worms stay flipbooks"); return false; }
            var m = new GpuMass { Slot = WormSimConst.FreeSlot, FirstWorm = first, WormCount = total, ActiveCount = total, DrawEnd = 0, Active = true, IsFree = true, NeedsInit = false, Radius = 1f };
            s_slots[WormSimConst.FreeSlot] = m;
            s_free = m;
            for (int i = 0; i < total; i++) s_wormData[first + i] = new GpuWormData { Mass = (uint)WormSimConst.FreeSlot, Seed = 0u, Speed = 0f, Phase = 0f };
            s_worms.SetData(s_wormData, first, first, total);
            for (int l = 0; l < 3; l++) s_freeStage[l] = new NativeArray<Vector4>(cap * WormSimConst.Nodes, Allocator.Persistent);
            UpdateHigh();
            s_signature = 0;
            return true;
        }

        /// <summary>Starts a frame of free ropes: the local frame of their nodes is put at the camera (rounded), the counts are cleared.</summary>
        public static void FreeBegin(Vector3 camera)
        {
            if (s_free == null) return;
            s_freeCount[0] = s_freeCount[1] = s_freeCount[2] = 0;
            s_freeOrigin = new Vector3(Mathf.Round(camera.x), Mathf.Round(camera.y), Mathf.Round(camera.z));
            s_free.Origin = s_freeOrigin;
        }

        /// <summary>One free rope: the 8 nodes (world positions, tail first) and their radius scales, at the level of detail <paramref name="lod"/>. False when that level is full.</summary>
        public static bool FreeAdd(int lod, Vector3[] world, float[] scale)
        {
            if (s_free == null) return false;
            int c = s_freeCount[lod];
            if (c >= WormSimConst.FreeCapPerLod) return false;
            NativeArray<Vector4> stage = s_freeStage[lod];
            int at = c * WormSimConst.Nodes;
            for (int n = 0; n < WormSimConst.Nodes; n++)
            {
                Vector3 p = world[n] - s_freeOrigin;
                stage[at + n] = new Vector4(p.x, p.y, p.z, scale[n]);
            }
            s_freeCount[lod] = c + 1;
            return true;
        }

        /// <summary>Ends the frame of free ropes: the nodes go to the pool, in three consecutive ranges (LOD0, LOD1, LOD2) the lists of the draw are made of.</summary>
        public static void FreeEnd()
        {
            if (s_free == null) return;
            int off = 0;
            for (int l = 0; l < 3; l++)
            {
                int c = s_freeCount[l];
                if (c > 0) s_pos.SetData(s_freeStage[l], 0, (s_free.FirstWorm + off) * WormSimConst.Nodes, c * WormSimConst.Nodes);
                off += c;
            }
            int sig = s_freeCount[0] * 1000003 + s_freeCount[1] * 1009 + s_freeCount[2];
            if (sig != s_freeSig) { s_freeSig = sig; }
        }

        private static int AllocWorms(int count)
        {
            int start = 0;
            bool moved = true;
            while (moved)
            {
                moved = false;
                for (int i = 0; i < s_slots.Length; i++)
                {
                    GpuMass o = s_slots[i];
                    if (o == null) continue;
                    if (start < o.FirstWorm + o.WormCount && o.FirstWorm < start + count) { start = o.FirstWorm + o.WormCount; moved = true; }
                }
            }
            return start + count <= WormSimConst.MaxWorms ? start : -1;
        }

        private static void UpdateHigh()
        {
            int high = 0;
            for (int i = 0; i < s_slots.Length; i++) if (s_slots[i] != null) high = Math.Max(high, s_slots[i].FirstWorm + s_slots[i].WormCount);
            WormsInUse = high;
        }

        /// <summary>
        /// Uploads the static data of the skin engulf of a mass once, at the selection of the anchors: the anchor vertices (rest position, normal, bones, weights), the 8 nearest
        /// anchors of each, the anchors by height and the paths over the surface. The anchors themselves are skinned each frame on the GPU from the bones (<see cref="UploadBones"/>).
        /// </summary>
        public static void UploadSkin(GpuMass m, GpuSkinSet set)
        {
            m.SkinSet = set;
            int n = set.Count, a = WormSimConst.MaxAnchors;
            s_skinVerts.SetData(set.Verts, 0, m.Slot * a, n);
            s_knn.SetData(set.Knn, 0, m.Slot * a * WormSimConst.NeighbourCount, n * WormSimConst.NeighbourCount);
            s_order.SetData(set.Order, 0, m.Slot * a, n);
            s_hop.SetData(set.Hop, 0, m.Slot * a * WormSimConst.HopWords, n * WormSimConst.HopWords);
            m.AnchorCount = n;
        }

        /// <summary>The matrices of the bones of the anchors this frame (<c>bone.localToWorldMatrix * bindpose</c>, relative to the origin of the mass): the only per frame upload of the skin.</summary>
        public static void UploadBones(GpuMass m, Matrix4x4[] bones, int count)
        {
            if (count > 0) s_bones.SetData(bones, 0, m.Slot * WormSimConst.MaxBones, count);
        }

        /// <summary>The compute shader skins the anchors of every mass that has some (one dispatch).</summary>
        private static void DispatchSkin()
        {
            for (int i = 0; i < s_slots.Length; i++)
                if (s_slots[i] != null && s_slots[i].AnchorCount > 0 && s_slots[i].Mode == GpuMassMode.Engulf)
                {
                    s_cs.Dispatch(kSkin, WormSimConst.MaxAnchors / 64, WormSimConst.MaxMasses, 1);
                    return;
                }
        }

        /// <summary>Skins the anchors now, whatever the clock (the editor check).</summary>
        public static void SkinNow()
        {
            if (Ready) DispatchSkin();
        }

        /// <summary>Publishes the environment the capture wrote into <see cref="GpuMass.Heights"/> and <see cref="GpuMass.Prims"/> (local frame).</summary>
        public static void UploadEnvironment(GpuMass m, int primCount, Vector2 gridMin, float spacing)
        {
            m.PrimCount = Mathf.Clamp(primCount, 0, WormSimConst.MaxPrims);
            m.GridMin = gridMin;
            m.GridSpacing = spacing;
            int hn = WormSimConst.HeightN * WormSimConst.HeightN;
            s_height.SetData(m.Heights, 0, m.Slot * hn, hn);
            if (m.SideEnabled) s_sides.SetData(m.Sides, 0, m.Slot * WormSimConst.SideMaps * WormSimConst.SideMapSize, WormSimConst.SideMaps * WormSimConst.SideMapSize);
            if (m.PrimCount > 0) s_prims.SetData(m.Prims, 0, m.Slot * WormSimConst.MaxPrims, m.PrimCount);
        }

        /// <summary>Flat ground at world height <paramref name="y"/> and no primitive (editor check and tests of the host).</summary>
        public static void SetFlatEnvironment(GpuMass m, float y)
        {
            for (int i = 0; i < m.Heights.Length; i++) m.Heights[i] = y - m.Origin.y;
            UploadEnvironment(m, 0, new Vector2(m.Center.x - m.GridExtent * 0.5f, m.Center.z - m.GridExtent * 0.5f), m.GridExtent / (WormSimConst.HeightN - 1));
        }

        internal static GpuMassParams BuildParams(GpuMass m, bool simulated)
        {
            Vector3 h = m.Heading;
            h.y = 0f;
            h = h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.forward;
            float stretch = Mathf.Clamp01(m.Speed / 1.5f);
            float r = m.Radius;
            bool skin = m.Mode == GpuMassMode.Engulf && m.AnchorCount > 0 && m.SkinSet != null;
            GpuSleevePlan plan = skin ? m.SkinSet.Plan(m.ActiveCount, Mathf.Clamp01(m.Engulf)) : default;
            // the ranks of the worms are taken modulo the whole segment: with fewer worms alive the plan is scaled up so that the alive ones fill the sleeve as planned
            float rankScale = (float)m.WormCount / Mathf.Max(m.ActiveCount, 1);
            plan.Count *= rankScale; plan.PerAnchor *= rankScale;
            m.SleevePlan = plan;
            bool engulf = !skin && m.Mode == GpuMassMode.Engulf && m.AdhesivePrim >= 0;
            Vector3 c = m.Center - m.Origin;
            return new GpuMassParams
            {
                Origin = new Vector4(m.Origin.x, m.Origin.y, m.Origin.z, simulated ? 1f : 0f),
                Center = new Vector4(c.x, c.y, c.z, r),
                Heading = new Vector4(h.x, 0f, h.z, m.Speed),
                Shape = new Vector4(m.ShapeAlong > 0f ? m.ShapeAlong : r * (1f + 0.3f * stretch), Mathf.Max(m.DomeHeight * r, 0.05f), m.ShapeAcross > 0f ? m.ShapeAcross : r * (1f - 0.1f * stretch), m.ShapeAlong > 0f ? 0f : 0.2f * r * stretch),
                Motion = new Vector4(m.Conveyor, m.CreepScale, m.HomePull, m.Engulf),
                Sleeve = new Vector4(engulf ? (float)Mathf.Max(m.AdhesiveCount, 1) : 0f, m.ShellThickness, Mathf.Max(m.AdhesivePrim, 0), m.GroundFriction),
                FirstWorm = (uint)m.FirstWorm, WormCount = (uint)m.WormCount, PrimCount = (uint)m.PrimCount, MassIndex = (uint)m.Slot,
                GridRect = new Vector4(m.GridMin.x - m.Origin.x, m.GridMin.y - m.Origin.z, m.GridSpacing, 1f / m.GridSpacing),
                Skin = skin ? new Vector4(m.AnchorCount, Mathf.Clamp01(m.Engulf), m.TargetHeight, m.SurfaceArea) : Vector4.zero,
                Side = new Vector4(m.SideYMin - m.Origin.y, m.SideYStep, 1f / m.SideYStep, m.SideEnabled ? 1f : 0f),
                Plan = new Vector4(plan.Count, plan.PerAnchor, plan.TopRel, plan.Cover), Plan2 = new Vector4(plan.Base, m.ActiveCount, 0f, 0f),
            };
        }

        private static void FillParams(GpuMass m, bool simulated) => s_massParams[m.Slot] = BuildParams(m, simulated);

        // ---------------------------------------------------------------- frame

        /// <summary>Places the worms of the mass at once (captures the environment first when a source is set). Used by Tick, the parity check and the editor check.</summary>
        public static void InitNow(GpuMass m)
        {
            if (Environment != null && !m.ManualEnvironment) Environment.Tick(m, Time.realtimeSinceStartup, true);
            m.EnvForce = false;
            FillParams(m, true);
            s_masses.SetData(s_massParams, m.Slot, m.Slot, 1);
            DispatchSkin();
            s_cs.SetInt(StepIndexId, (int)StepIndex);
            s_cs.SetFloat(InitScaleId, 1f);
            s_cs.SetInt(InitFirstId, m.FirstWorm);
            s_cs.SetInt(InitCountId, m.ActiveCount);
            s_cs.Dispatch(kInit, (m.ActiveCount + 63) / 64, 1, 1);
            if (m.ActiveCount < m.WormCount)
            {
                // the spare worms of the segment wait inside the mass with no size, ready to be born
                s_cs.SetInt(InitFirstId, m.FirstWorm + m.ActiveCount);
                s_cs.SetInt(InitCountId, m.WormCount - m.ActiveCount);
                s_cs.SetFloat(InitScaleId, 0f);
                s_cs.Dispatch(kInit, (m.WormCount - m.ActiveCount + 63) / 64, 1, 1);
                s_cs.SetFloat(InitScaleId, 1f);
            }
            m.NeedsInit = false;
            m.Simulated = true;
        }

        /// <summary>Dispatches <paramref name="steps"/> simulation steps now, whatever the clock (the parity check).</summary>
        public static void StepNow(int steps)
        {
            if (!Ready) return;
            s_cs.SetInt(WormCountId, WormsInUse);
            for (int i = 0; i < steps; i++) DispatchStep();
        }

        private static void DispatchStep()
        {
            int n = WormsInUse;
            if (n == 0) return;
            s_cs.SetInt(StepIndexId, (int)StepIndex);
            s_cs.Dispatch(kClear, WormSimConst.GridCells * WormSimConst.GridSlots / 256, 1, 1);
            s_cs.Dispatch(kPredict, (n + 63) / 64, 1, 1);
            s_cs.Dispatch(kSeparate, (n * WormSimConst.PointStride + 63) / 64, 1, 1);
            s_cs.Dispatch(kSolve, (n + 63) / 64, 1, 1);
            StepIndex++;
        }

        /// <summary>The per frame path: environment, parameters, fixed steps, draw lists, draws. No allocation.</summary>
        public static void Tick(float deltaTime)
        {
            if (!Ready) return;
            long heap0 = MeasureAlloc ? UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() : 0L;
            long t0 = Stopwatch.GetTimestamp();
            PreTick?.Invoke();
            Camera cam = CameraProvider != null ? CameraProvider() : Camera.main;
            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            LastCameraPosition = camPos;
            float now = Time.realtimeSinceStartup;

            int simulatedMasses = 0;
            for (int i = 0; i < s_slots.Length; i++)
            {
                GpuMass m = s_slots[i];
                if (m == null) continue;
                if (m.IsFree)
                {
                    s_massParams[m.Slot] = new GpuMassParams { Origin = new Vector4(s_freeOrigin.x, s_freeOrigin.y, s_freeOrigin.z, 0f), FirstWorm = (uint)m.FirstWorm, WormCount = (uint)m.WormCount, MassIndex = (uint)m.Slot };
                    continue;
                }
                if (m.NeedsInit) InitNow(m);

                // re-centre a mass that walked far from its origin (floats keep their millimetre only near the origin)
                Vector3 off = m.Center - m.Origin;
                if (off.x * off.x + off.z * off.z > 128f * 128f)
                {
                    Vector3 no = new Vector3(Mathf.Round(m.Center.x), Mathf.Round(m.Center.y), Mathf.Round(m.Center.z));
                    s_cs.SetInt(InitFirstId, m.FirstWorm);
                    s_cs.SetInt(InitCountId, m.WormCount);
                    s_cs.SetVector(ShiftId, m.Origin - no);
                    s_cs.Dispatch(kShift, (m.WormCount + 63) / 64, 1, 1);
                    m.Origin = no;
                    m.EnvForce = true;
                }

                if (Environment != null && !m.ManualEnvironment) Environment.Tick(m, now, m.EnvForce);
                m.EnvForce = false;

                float dist = (m.Center - camPos).magnitude;
                m.Simulated = m.ForceSimulated || (!m.Frozen && dist < FreezeDistance);
                if (m.Simulated) simulatedMasses++;
                if (m.DrawEnd > m.ActiveCount && now > m.ShrinkUntil) { m.DrawEnd = m.ActiveCount; s_signature = 0; }
                FillParams(m, m.Simulated);
            }
            s_masses.SetData(s_massParams);
            if (simulatedMasses > 0) DispatchSkin();

            int steps = 0;
            if (AutoStep && simulatedMasses > 0)
            {
                s_accum += Mathf.Min(deltaTime, 0.25f);
                steps = Math.Min((int)(s_accum / WormSimConst.Dt), WormSimConst.MaxSubSteps);
                s_accum -= steps * WormSimConst.Dt;
                if (s_accum > WormSimConst.Dt) s_accum = 0f;      // the frame was slower than three steps: the simulation lags behind rather than spiral
                if (steps > 0)
                {
                    s_cs.SetInt(WormCountId, WormsInUse);
                    for (int k = 0; k < steps; k++) DispatchStep();
                }
            }
            LastSubSteps = steps;

            if (DrawEnabled && cam != null) BuildListsAndDraw(cam, camPos);
            else { DrawCalls = 0; DrawnWorms = 0; Triangles = 0; ShadowTriangles = 0; VisibleMasses = 0; }

            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            LastMs = ms;
            if (ms > MaxMs) MaxMs = ms;
            s_sumMs += ms;
            Frames++;
            if (MeasureAlloc)
            {
                long rise = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - heap0;
                if (rise > 0) OwnAllocBytes += rise;
            }
        }

        private static void BuildListsAndDraw(Camera cam, Vector3 camPos)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, s_planes);

            // A signature of what is visible at which LOD: the lists are rebuilt and uploaded only when it changes.
            int sig = 17;
            int visible = 0;
            bool any = false;
            Bounds all = default;
            for (int i = 0; i < s_slots.Length; i++)
            {
                GpuMass m = s_slots[i];
                if (m == null) { sig = sig * 31 + 0; continue; }
                if (m.IsFree) continue;
                float ext = m.Radius + 0.7f;
                // The LOD follows the distance from the camera to the nearest point of the volume of the mass: the layer on the ground, and the engulfed target (Height)
                // when there is one. Not the centre of its base: a mass that wraps a 1.8 m body seen from 1 m is at LOD0, with shadows.
                float hgt = Mathf.Max(m.Height, 0.9f * m.Radius) + 0.3f;
                var volume = new Bounds(m.Center + Vector3.up * (hgt * 0.5f - 0.1f), new Vector3(m.Radius * 2f + 0.2f, hgt, m.Radius * 2f + 0.2f));
                float dist = Mathf.Sqrt(volume.SqrDistance(camPos));
                m.LodDistance = dist;
                m.VolumeCenter = volume.center;
                m.VolumeExtents = volume.extents;
                bool vis = dist < DrawDistance && GeometryUtility.TestPlanesAABB(s_planes, new Bounds(volume.center, volume.size + new Vector3(1.4f, 1.4f, 1.4f)));
                int lod = dist < LodDistances[0] ? 0 : dist < LodDistances[1] ? 1 : 2;
                m.Visible = vis;
                m.Lod = lod;
                sig = sig * 31 + (vis ? 1 + lod : 0) + m.DrawEnd * 7 + m.FirstWorm * 13;
                if (!vis) continue;
                visible++;
                Bounds b = new Bounds(m.Center, new Vector3(ext * 2f + 4f, ext * 2f + 4f, ext * 2f + 4f));
                if (!any) { all = b; any = true; } else all.Encapsulate(b);
            }
            VisibleMasses = visible;
            int freeTotal = s_freeCount[0] + s_freeCount[1] + s_freeCount[2];
            sig = sig * 31 + s_freeSig;
            if (freeTotal > 0)
            {
                var fb = new Bounds(camPos, Vector3.one * (2f * DrawDistance));        // the free worms are anywhere around the camera
                if (!any) { all = fb; any = true; } else all.Encapsulate(fb);
            }
            if (!any)
            {
                DrawCalls = 0; DrawnWorms = 0; Triangles = 0; ShadowTriangles = 0;
                return;
            }
            s_drawBounds = all;

            if (sig != s_signature)
            {
                s_signature = sig;
                for (int l = 0; l < 4; l++) s_listCount[l] = 0;
                for (int i = 0; i < s_slots.Length; i++)
                {
                    GpuMass m = s_slots[i];
                    if (m == null || m.IsFree || !m.Visible) continue;
                    NativeArray<uint> list = s_listData[m.Lod];
                    int c = s_listCount[m.Lod];
                    for (int w = 0; w < m.DrawEnd; w++) list[c + w] = (uint)(m.FirstWorm + w);
                    s_listCount[m.Lod] = c + m.DrawEnd;
                    if (m.Lod == 0)
                    {
                        // shadows of LOD0 only, and for one worm in two (the simulation does not flag the outer layer yet)
                        NativeArray<uint> sh = s_listData[3];
                        int sc = s_listCount[3];
                        for (int w = 0; w < m.DrawEnd; w += 2) sh[sc++] = (uint)(m.FirstWorm + w);
                        s_listCount[3] = sc;
                    }
                }
                if (freeTotal > 0 && s_free != null)
                {
                    int off = 0;
                    for (int l = 0; l < 3; l++)
                    {
                        int fc = s_freeCount[l];
                        NativeArray<uint> list = s_listData[l];
                        int c = s_listCount[l];
                        for (int w = 0; w < fc; w++) list[c + w] = (uint)(s_free.FirstWorm + off + w);
                        s_listCount[l] = c + fc;
                        if (l == 0)
                        {
                            NativeArray<uint> sh = s_listData[3];
                            int sc = s_listCount[3];
                            for (int w = 0; w < fc; w++) sh[sc++] = (uint)(s_free.FirstWorm + w);
                            s_listCount[3] = sc;
                        }
                        off += fc;
                    }
                }
                for (int l = 0; l < 4; l++)
                {
                    if (s_listCount[l] > 0) s_lists[l].SetData(s_listData[l], 0, 0, s_listCount[l]);
                    int lod = l == 3 ? 0 : l;
                    s_argData[l] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = s_meshes[lod].GetIndexCount(0), instanceCount = (uint)s_listCount[l],
                        startIndex = 0, baseVertexIndex = 0, startInstance = 0,
                    };
                }
                s_args.SetData(s_argData);
            }

            int calls = 0, worms = 0;
            long tris = 0, shadowTris = 0;
            for (int l = 0; l < 4; l++)
            {
                if (s_listCount[l] == 0) continue;
                var rp = new RenderParams(s_materials[l])
                {
                    worldBounds = s_drawBounds,
                    shadowCastingMode = l == 3 ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.Off,
                    receiveShadows = l != 3,
                    lightProbeUsage = LightProbeUsage.Off,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                };
                Graphics.RenderMeshIndirect(rp, s_meshes[l == 3 ? 0 : l], s_args, 1, l);
                calls++;
                if (l == 3) shadowTris += (long)s_listCount[l] * GpuWormTube.Triangles(0);
                else { worms += s_listCount[l]; tris += (long)s_listCount[l] * GpuWormTube.Triangles(l); }
            }
            DrawCalls = calls;
            DrawnWorms = worms;
            Triangles = tris;
            ShadowTriangles = shadowTris;
        }

        private sealed class GpuWormHostDriver : MonoBehaviour
        {
            private void Update() => GpuWormHost.Tick(Time.deltaTime);

            private void OnDestroy()
            {
                // the plugin owns the lifetime; a destroyed driver (scene teardown) must not leave the host ticking
            }
        }
    }
}
