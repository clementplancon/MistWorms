using System;
using System.Collections.Generic;
using System.Diagnostics;
using MistWorms.Assets;
using MistWorms.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistWorms.Render
{
    internal enum WormAnim
    {
        Crawl = 0,
        Rear = 1,
        Strike = 2,
        Sleep = 3,
        Idle = 4,
        Coil = 5,
    }

    /// <summary>Mesh detail of a worm: LOD1 has 240 triangles and exists for crawl, idle and coil only.</summary>
    internal enum WormLod
    {
        Lod0 = 0,
        Lod1 = 1,
    }

    /// <summary>Result of <see cref="WormRenderer.Sink.ClassifySphere"/> against the view frustum and render distance.</summary>
    internal enum SphereVis
    {
        Outside = 0,
        Inside = 1,
        Partial = 2,
    }

    /// <summary>A producer of instances (colony, mass, dev preview). Called once per frame from <see cref="WormRenderer"/>.</summary>
    internal interface IWormRenderSource
    {
        void CollectInstances(WormRenderer.Sink sink);
    }

    /// <summary>
    /// Client-side flipbook renderer (spec §12.1). Sources push TRS matrices per (lod, animation, frame); each frame the
    /// renderer culls them (distance, frustum) and draws every batch with Graphics.RenderMeshInstanced in chunks of at
    /// most 1023. A batch has one list and a fixed shadow mode (LOD0 casts shadows, LOD1 does not). No allocation in the per-frame path once buffers
    /// have reached their working size. Per-instance tint is not supported in phase 2: RenderMeshInstanced cannot
    /// take a per-instance MaterialPropertyBlock for a shader without instanced properties.
    /// </summary>
    // Runs after GameCamera.LateUpdate (default order) so culling sees the final camera pose of the frame.
    [DefaultExecutionOrder(32000)]
    internal sealed class WormRenderer : MonoBehaviour
    {
        public const string CrawlName = "worm_crawl";
        public const string RearName = "worm_rear";
        public const string StrikeName = "worm_strike";
        public const string SleepName = "worm_sleep";
        public const string IdleName = "worm_idle";
        public const string CoilName = "worm_coil";
        public const string Lod1Suffix = "_lod1";
        public const string NestName = "nest_idle";

        private const int AnimCount = 6;   // WormAnim values
        private const int LodCount = 2;
        private const int MaxInstancesPerCall = InstanceList.MaxInstancesPerCall;

        /// <summary>RenderMeshInstanced call budget; above it <see cref="BatchMerge"/> folds odd frames into even ones.</summary>
        public const int DrawCallBudget = 64;

        public struct FrameStats
        {
            public int Instances;   // drawn after culling
            public int Culled;
            public int DrawCalls;   // RenderMeshInstanced / RenderMesh calls, shadow passes not counted
            public int Dropped;     // refused because a list hit MaxInstancesPerList
            public float CpuMs;     // total: collection + culling + merge + submission
            public float CollectMs; // sources + merge guard
            public float SubmitMs;  // RenderMeshInstanced calls
            public int Batches;     // non-empty lists drawn
            public int Triangles;   // sum of instances x triangles of their mesh (shadow passes not counted)
            public int Merged;      // lists folded by BatchMerge
            public float HeapMs;    // sum of the per-mass collection times, reported by the mass source
            public int HeapDrawn;   // worms drawn by the mass source
            public int HeapLod0, HeapLod1, HeapLod2; // masses per LOD
        }

        private static WormRenderer s_instance;

        /// <summary>Stats of the last rendered frame (for the future /perf route).</summary>
        public static FrameStats Last { get; private set; }

        /// <summary>Frame counter at which <see cref="Last"/> was produced; lets callers wait for a fresh frame.</summary>
        public static int LastFrame { get; private set; }

        /// <summary>The renderer if it already exists; unlike <see cref="Instance"/> never creates one (safe in OnDestroy at shutdown).</summary>
        public static WormRenderer Existing => s_instance;

        public static WormRenderer Instance
        {
            get
            {
                if (s_instance == null)
                {
                    var go = new GameObject("MistWorms.WormRenderer") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(go);
                    s_instance = go.AddComponent<WormRenderer>();
                }
                return s_instance;
            }
        }

        private readonly List<IWormRenderSource> _sources = new List<IWormRenderSource>();
        private readonly List<Batch> _batches = new List<Batch>();
        private readonly Batch[][][] _anim = new Batch[LodCount][][];   // [lod][anim][frame]
        private readonly float[] _animFps = new float[AnimCount];      // LOD0 timing (LOD1 callers derive frames from it)
        private readonly bool[] _animLoop = new bool[AnimCount];
        private Batch _nest;
        private InstanceList[][] _mergeGroups;
        private InstanceList[] _fixedLists;
        private Sink _sink;
        private bool _batchesBuilt;
        private bool _warnedFallback;

        private readonly Plane[] _planes = new Plane[6];
        private Vector3 _camPos;
        private float _renderDistance;
        private float _shadowDistance;
        private RenderParams _rp;
        private FrameStats _stats;

        // ------------------------------------------------------------------ registration

        public void Register(IWormRenderSource source)
        {
            if (source != null && !_sources.Contains(source)) _sources.Add(source);
        }

        public void Unregister(IWormRenderSource source) => _sources.Remove(source);

        public int SourceCount => _sources.Count;

        // ------------------------------------------------------------------ frame

        private void Awake()
        {
            _anim[0] = new Batch[AnimCount][];
            _anim[1] = new Batch[AnimCount][];
            _sink = new Sink(this);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        private void LateUpdate()
        {
            long start = Stopwatch.GetTimestamp();
            _stats = default;
            try
            {
                if (_sources.Count > 0 && EnsureBatches())
                {
                    Camera cam = Utils.GetMainCamera();
                    if (cam != null)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        Collect(cam);
                        long t1 = Stopwatch.GetTimestamp();
                        Draw();
                        long t2 = Stopwatch.GetTimestamp();
                        _stats.CollectMs = (float)((t1 - t0) * 1000.0 / Stopwatch.Frequency);
                        _stats.SubmitMs = (float)((t2 - t1) * 1000.0 / Stopwatch.Frequency);
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.LogException("WormRenderer.LateUpdate", e);
            }
            _stats.CpuMs = (float)((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
            Last = _stats;
            LastFrame = Time.frameCount;
        }

        private void Collect(Camera cam)
        {
            GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            _camPos = cam.transform.position;
            _renderDistance = MWConfig.Performance.RenderDistance.Value;
            _shadowDistance = MWConfig.Performance.ShadowDistance.Value;

            for (int i = 0; i < _batches.Count; i++) _batches[i].List.Reset();

            for (int i = _sources.Count - 1; i >= 0; i--)
            {
                IWormRenderSource source = _sources[i];
                try
                {
                    source.CollectInstances(_sink);
                }
                catch (Exception e)
                {
                    // A broken producer would otherwise log every frame; drop it.
                    Plugin.LogException("WormRenderer source " + source.GetType().Name, e);
                    _sources.RemoveAt(i);
                }
            }

            _stats.Merged = BatchMerge.Apply(_mergeGroups, _fixedLists, DrawCallBudget, out int lost);
            _stats.Dropped += lost;
        }

        private void Add(Batch batch, in Matrix4x4 m)
        {
            Vector3 c = m.MultiplyPoint3x4(batch.LocalCenter);
            float sx = new Vector3(m.m00, m.m10, m.m20).sqrMagnitude;
            float sy = new Vector3(m.m01, m.m11, m.m21).sqrMagnitude;
            float sz = new Vector3(m.m02, m.m12, m.m22).sqrMagnitude;
            float r = batch.Radius * Mathf.Sqrt(Mathf.Max(sx, Mathf.Max(sy, sz)));

            float sqr = (c - _camPos).sqrMagnitude;
            float reach = _renderDistance + r;
            if (sqr > reach * reach) { _stats.Culled++; return; }
            for (int i = 0; i < 6; i++)
            {
                if (_planes[i].GetDistanceToPoint(c) < -r) { _stats.Culled++; return; }
            }

            if (!batch.List.Add(m, c, r)) _stats.Dropped++;
        }

        // The caller already proved the instance is visible (ClassifySphere == Inside): list and bounds only.
        private void AddUnculled(Batch batch, in Matrix4x4 m, float scaleMax)
        {
            Vector3 c = m.MultiplyPoint3x4(batch.LocalCenter);
            if (!batch.List.Add(m, c, batch.Radius * scaleMax)) _stats.Dropped++;
        }

        private void Draw()
        {
            for (int i = 0; i < _batches.Count; i++)
            {
                Batch b = _batches[i];
                DrawList(b, b.List, b.CastShadows && _shadowDistance > 0f ? ShadowCastingMode.On : ShadowCastingMode.Off);
            }
        }

        private void DrawList(Batch b, InstanceList list, ShadowCastingMode shadows)
        {
            int count = list.Count;
            if (count == 0) return;

            _rp = new RenderParams(b.Material)
            {
                layer = 0, // Default: lit and shadowed like world geometry; the layer is not hit-tested (no collider)
                shadowCastingMode = shadows,
                receiveShadows = true,
                worldBounds = list.Bounds(),
            };

            if (b.Instancing)
            {
                for (int start = 0; start < count; start += MaxInstancesPerCall)
                {
                    int n = Mathf.Min(MaxInstancesPerCall, count - start);
                    Graphics.RenderMeshInstanced(_rp, b.Mesh, 0, list.Matrices, n, start);
                    _stats.DrawCalls++;
                }
            }
            else
            {
                if (!_warnedFallback)
                {
                    _warnedFallback = true;
                    Plugin.Log.LogWarning("[Render] instancing unsupported for " + b.Material.name + ": drawing one instance per call");
                }
                for (int i = 0; i < count; i++)
                {
                    Graphics.RenderMesh(_rp, b.Mesh, 0, list.Matrices[i]);
                    _stats.DrawCalls++;
                }
            }
            _stats.Instances += count;
            _stats.Batches++;
            _stats.Triangles += count * b.Tris;
        }

        // ------------------------------------------------------------------ batches

        private bool EnsureBatches()
        {
            if (_batchesBuilt) return true;
            if (!AssetCatalog.IsReady || !MaterialFactory.EnsureBuilt()) return false;

            _animFps[(int)WormAnim.Crawl] = Build(WormAnim.Crawl, CrawlName);
            _animFps[(int)WormAnim.Rear] = Build(WormAnim.Rear, RearName);
            _animFps[(int)WormAnim.Strike] = Build(WormAnim.Strike, StrikeName);
            _animFps[(int)WormAnim.Sleep] = Build(WormAnim.Sleep, SleepName);
            _animFps[(int)WormAnim.Idle] = Build(WormAnim.Idle, IdleName);
            _animFps[(int)WormAnim.Coil] = Build(WormAnim.Coil, CoilName);
            BuildLod1(WormAnim.Crawl, CrawlName);
            BuildLod1(WormAnim.Idle, IdleName);
            BuildLod1(WormAnim.Coil, CoilName);

            Mesh nest = AssetCatalog.Mesh(NestName) ?? throw new InvalidOperationException("missing mesh " + NestName);
            _nest = NewBatch(nest, MaterialFactory.Nest, MaterialFactory.NestInstancing, SingleTris(NestName), true);

            BuildMergeSets();
            _batchesBuilt = true;
            return true;
        }

        private static int SingleTris(string name)
        {
            int[] t = AssetCatalog.FrameTriangles(name);
            return t != null && t.Length > 0 ? t[0] : 0;
        }

        private float Build(WormAnim anim, string name)
        {
            Mesh[] frames = AssetCatalog.Frames(name) ?? throw new InvalidOperationException("missing animation " + name);
            AnimationInfo info = AssetCatalog.Animation(name) ?? throw new InvalidOperationException("missing animation info " + name);
            int[] tris = AssetCatalog.FrameTriangles(name);
            var batches = new Batch[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                batches[i] = NewBatch(frames[i], MaterialFactory.Worm, MaterialFactory.WormInstancing, tris != null ? tris[i] : 0, true);
            _anim[0][(int)anim] = batches;
            _animLoop[(int)anim] = info.Loop;
            return info.Fps;
        }

        private void BuildLod1(WormAnim anim, string name)
        {
            string lodName = name + Lod1Suffix;
            Mesh[] frames = AssetCatalog.Frames(lodName) ?? throw new InvalidOperationException("missing animation " + lodName);
            int[] tris = AssetCatalog.FrameTriangles(lodName);
            var batches = new Batch[frames.Length];
            for (int i = 0; i < frames.Length; i++)
                batches[i] = NewBatch(frames[i], MaterialFactory.Worm, MaterialFactory.WormInstancing, tris != null ? tris[i] : 0, false);
            _anim[1][(int)anim] = batches;
        }

        // Degradation order of the draw call guard (phase 4B-3): cheapest to coarsen first.
        private void BuildMergeSets()
        {
            var order = new[]
            {
                new KeyValuePair<int, WormAnim>(1, WormAnim.Idle), new KeyValuePair<int, WormAnim>(1, WormAnim.Coil), new KeyValuePair<int, WormAnim>(1, WormAnim.Crawl),
                new KeyValuePair<int, WormAnim>(0, WormAnim.Idle), new KeyValuePair<int, WormAnim>(0, WormAnim.Coil), new KeyValuePair<int, WormAnim>(0, WormAnim.Sleep),
                new KeyValuePair<int, WormAnim>(0, WormAnim.Rear), new KeyValuePair<int, WormAnim>(0, WormAnim.Crawl),
            };
            var mergeable = new HashSet<InstanceList>();
            _mergeGroups = new InstanceList[order.Length][];
            for (int g = 0; g < order.Length; g++)
            {
                Batch[] frames = _anim[order[g].Key][(int)order[g].Value];
                var lists = new InstanceList[frames.Length];
                for (int f = 0; f < lists.Length; f++) { lists[f] = frames[f].List; mergeable.Add(lists[f]); }
                _mergeGroups[g] = lists;
            }
            var fixedLists = new List<InstanceList>();
            for (int i = 0; i < _batches.Count; i++)
                if (!mergeable.Contains(_batches[i].List)) fixedLists.Add(_batches[i].List);
            _fixedLists = fixedLists.ToArray();
        }

        private Batch NewBatch(Mesh mesh, Material material, bool instancing, int tris, bool castShadows)
        {
            Bounds b = mesh.bounds;
            var batch = new Batch
            {
                Mesh = mesh,
                Material = material,
                Instancing = instancing,
                LocalCenter = b.center,
                Radius = b.extents.magnitude,
                Tris = tris,
                CastShadows = castShadows,
            };
            _batches.Add(batch);
            return batch;
        }

        private sealed class Batch
        {
            public Mesh Mesh;
            public Material Material;
            public bool Instancing;
            public Vector3 LocalCenter;
            public float Radius;
            public int Tris;
            public bool CastShadows;   // fixed per batch: LOD0 casts, LOD1 does not
            public readonly InstanceList List = new InstanceList();
        }

        // ------------------------------------------------------------------ producer API

        /// <summary>What a <see cref="IWormRenderSource"/> pushes its instances into.</summary>
        public sealed class Sink
        {
            private readonly WormRenderer _r;

            internal Sink(WormRenderer renderer) { _r = renderer; }

            public Vector3 CameraPosition => _r._camPos;
            public float RenderDistance => _r._renderDistance;
            public float ShadowDistance => _r._shadowDistance;

            /// <summary>Sphere against the 6 frustum planes and the render distance; Inside lets the caller use the unculled adds.</summary>
            public SphereVis ClassifySphere(Vector3 c, float r)
            {
                float d = (c - _r._camPos).magnitude;
                if (d > _r._renderDistance + r) return SphereVis.Outside;
                bool inside = d + r <= _r._renderDistance;
                for (int i = 0; i < 6; i++)
                {
                    float pd = _r._planes[i].GetDistanceToPoint(c);
                    if (pd < -r) return SphereVis.Outside;
                    if (pd < r) inside = false;
                }
                return inside ? SphereVis.Inside : SphereVis.Partial;
            }

            // LOD1 only exists for crawl, idle and coil; anything else falls back to LOD0.
            private Batch[] Frames(WormAnim anim, WormLod lod)
            {
                Batch[] frames = lod == WormLod.Lod1 ? _r._anim[1][(int)anim] : null;
                return frames ?? _r._anim[0][(int)anim];
            }

            /// <summary>LOD0 draw with per-instance culling.</summary>
            public void AddWorm(WormAnim anim, int frame, in Matrix4x4 trs) => AddWorm(anim, WormLod.Lod0, frame, trs);

            /// <summary>Per-instance culling. <paramref name="frame"/> is in the numbering of the effective LOD (see <see cref="FrameCount(WormAnim, WormLod)"/>).</summary>
            public void AddWorm(WormAnim anim, WormLod lod, int frame, in Matrix4x4 trs)
            {
                Batch[] frames = Frames(anim, lod);
                if (frames == null || frames.Length == 0) return;
                _r.Add(frames[Flipbook.Wrap(frame, frames.Length)], trs);
            }

            /// <summary>
            /// Adds <paramref name="count"/> matrices already culled by the caller (the mass worker) to the list of (anim, lod, frame) in one block copy;
            /// <paramref name="b"/> are their world bounds. <paramref name="frame"/> is the effective, already wrapped frame of the effective LOD.
            /// </summary>
            public void AddWormsBulk(WormAnim anim, WormLod lod, int frame, Matrix4x4[] src, int count, Bounds b)
            {
                Batch[] frames = Frames(anim, lod);
                if (frames == null || frames.Length == 0 || count <= 0) return;
                _r._stats.Dropped += frames[Flipbook.Wrap(frame, frames.Length)].List.AddRange(src, count, b.min, b.max);
            }

            /// <summary>The 6 frustum planes of the frame as (nx, ny, nz, d) x 6 in <paramref name="dst"/> (24 floats): dot(n, p) + d is the distance to the plane.</summary>
            public void CopyFrustumPlanes(float[] dst)
            {
                for (int i = 0; i < 6; i++)
                {
                    Plane pl = _r._planes[i];
                    dst[i * 4] = pl.normal.x; dst[i * 4 + 1] = pl.normal.y; dst[i * 4 + 2] = pl.normal.z; dst[i * 4 + 3] = pl.distance;
                }
            }

            /// <summary>Frames and the largest bounding radius (plus the offset of the mesh centre) over the frames of an animation at a LOD, for the worker tables.</summary>
            public void BucketInfo(WormAnim anim, WormLod lod, out int frames, out float radius)
            {
                Batch[] fr = Frames(anim, lod);
                frames = fr != null ? fr.Length : 0;
                radius = 0f;
                if (fr == null) return;
                for (int i = 0; i < fr.Length; i++)
                {
                    float r = fr[i].Radius + fr[i].LocalCenter.magnitude;
                    if (r > radius) radius = r;
                }
            }

            /// <summary>No frustum or distance test (the caller classified a sphere around it as Inside); <paramref name="scaleMax"/> bounds its scale.</summary>
            public void AddWormUnculled(WormAnim anim, WormLod lod, int frame, in Matrix4x4 trs, float scaleMax)
            {
                Batch[] frames = Frames(anim, lod);
                if (frames == null || frames.Length == 0) return;
                _r.AddUnculled(frames[Flipbook.Wrap(frame, frames.Length)], trs, scaleMax);
            }

            public void AddWorm(string animation, int frame, in Matrix4x4 trs) => AddWorm(Parse(animation), frame, trs);

            public void AddNest(in Matrix4x4 trs)
            {
                if (_r._nest != null) _r.Add(_r._nest, trs);
            }

            /// <summary>Per-mass figures a mass source adds to this frame's stats.</summary>
            public void ReportHeap(float ms, int drawn, int lod)
            {
                _r._stats.HeapMs += ms;
                _r._stats.HeapDrawn += drawn;
                if (lod <= 0) _r._stats.HeapLod0++; else if (lod == 1) _r._stats.HeapLod1++; else _r._stats.HeapLod2++;
            }

            public int FrameCount(WormAnim anim) => FrameCount(anim, WormLod.Lod0);

            /// <summary>Frames of the effective LOD (LOD0 when the animation has no LOD1).</summary>
            public int FrameCount(WormAnim anim, WormLod lod)
            {
                Batch[] frames = Frames(anim, lod);
                return frames != null ? frames.Length : 0;
            }

            /// <summary>Length of one pass of the animation, in seconds.</summary>
            public float Duration(WormAnim anim)
            {
                float fps = _r._animFps[(int)anim];
                return fps > 0f ? FrameCount(anim) / fps : 0f;
            }

            /// <summary>
            /// Frame of <paramref name="anim"/>: for loops <paramref name="time"/> is any clock (usually Time.time) and
            /// <paramref name="phase"/> a per-instance frame offset; for one-shots it is the time since the start.
            /// </summary>
            public int FrameAt(WormAnim anim, float time, float phase = 0f) =>
                Flipbook.FrameAt(FrameCount(anim), _r._animFps[(int)anim], _r._animLoop[(int)anim], time, phase);

            public bool IsLoop(WormAnim anim) => _r._animLoop[(int)anim];

            public static WormAnim Parse(string animation)
            {
                switch (animation)
                {
                    case CrawlName: return WormAnim.Crawl;
                    case RearName: return WormAnim.Rear;
                    case StrikeName: return WormAnim.Strike;
                    case SleepName: return WormAnim.Sleep;
                    case IdleName: return WormAnim.Idle;
                    case CoilName: return WormAnim.Coil;
                    default: throw new ArgumentException("unknown worm animation '" + animation + "'");
                }
            }
        }
    }
}
