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
    }

    /// <summary>A producer of instances (colony, mass, dev preview). Called once per frame from <see cref="WormRenderer"/>.</summary>
    internal interface IWormRenderSource
    {
        void CollectInstances(WormRenderer.Sink sink);
    }

    /// <summary>
    /// Client-side flipbook renderer (spec §12.1). Sources push TRS matrices per (animation, frame); each frame the
    /// renderer culls them (distance, frustum), splits shadow casters from the rest and draws every group with
    /// Graphics.RenderMeshInstanced in chunks of at most 1023. No allocation in the per-frame path once buffers
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
        public const string BoilName = "mass_boil";
        public const string NestName = "nest_idle";

        private const int MaxInstancesPerCall = 1023;
        private const int MaxInstancesPerList = 16384;

        public struct FrameStats
        {
            public int Instances;   // drawn after culling
            public int Culled;
            public int DrawCalls;   // RenderMeshInstanced / RenderMesh calls, shadow passes not counted
            public int Dropped;     // refused because a list hit MaxInstancesPerList
            public float CpuMs;     // collection + culling + submission
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
        private readonly Batch[][] _anim = new Batch[4][];
        private readonly float[] _animFps = new float[4];
        private readonly bool[] _animLoop = new bool[4];
        private Batch[] _dome;
        private float _domeFps;
        private Batch _nest;
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
                        Collect(cam);
                        Draw();
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

            for (int i = 0; i < _batches.Count; i++)
            {
                _batches[i].Near.Reset();
                _batches[i].Far.Reset();
            }

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

            float shadowReach = _shadowDistance + r;
            InstanceList list = _shadowDistance > 0f && sqr <= shadowReach * shadowReach ? batch.Near : batch.Far;
            if (!list.Add(m, c, r)) _stats.Dropped++;
        }

        private void Draw()
        {
            for (int i = 0; i < _batches.Count; i++)
            {
                Batch b = _batches[i];
                DrawList(b, b.Near, ShadowCastingMode.On);
                DrawList(b, b.Far, ShadowCastingMode.Off);
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
        }

        // ------------------------------------------------------------------ batches

        private bool EnsureBatches()
        {
            if (_batchesBuilt) return true;
            if (!AssetCatalog.IsReady || !MaterialFactory.EnsureBuilt()) return false;

            _animFps[(int)WormAnim.Crawl] = Build(WormAnim.Crawl, CrawlName, MaterialFactory.Worm, MaterialFactory.WormInstancing);
            _animFps[(int)WormAnim.Rear] = Build(WormAnim.Rear, RearName, MaterialFactory.Worm, MaterialFactory.WormInstancing);
            _animFps[(int)WormAnim.Strike] = Build(WormAnim.Strike, StrikeName, MaterialFactory.Worm, MaterialFactory.WormInstancing);
            _animFps[(int)WormAnim.Sleep] = Build(WormAnim.Sleep, SleepName, MaterialFactory.Worm, MaterialFactory.WormInstancing);

            Mesh[] boil = AssetCatalog.Frames(BoilName) ?? throw new InvalidOperationException("missing animation " + BoilName);
            _dome = new Batch[boil.Length];
            for (int i = 0; i < boil.Length; i++) _dome[i] = NewBatch(boil[i], MaterialFactory.Mass, MaterialFactory.MassInstancing);
            _domeFps = AssetCatalog.Animation(BoilName).Fps;

            Mesh nest = AssetCatalog.Mesh(NestName) ?? throw new InvalidOperationException("missing mesh " + NestName);
            _nest = NewBatch(nest, MaterialFactory.Nest, MaterialFactory.NestInstancing);

            _batchesBuilt = true;
            return true;
        }

        private float Build(WormAnim anim, string name, Material material, bool instancing)
        {
            Mesh[] frames = AssetCatalog.Frames(name) ?? throw new InvalidOperationException("missing animation " + name);
            AnimationInfo info = AssetCatalog.Animation(name) ?? throw new InvalidOperationException("missing animation info " + name);
            var batches = new Batch[frames.Length];
            for (int i = 0; i < frames.Length; i++) batches[i] = NewBatch(frames[i], material, instancing);
            _anim[(int)anim] = batches;
            _animLoop[(int)anim] = info.Loop;
            return info.Fps;
        }

        private Batch NewBatch(Mesh mesh, Material material, bool instancing)
        {
            Bounds b = mesh.bounds;
            var batch = new Batch
            {
                Mesh = mesh,
                Material = material,
                Instancing = instancing,
                LocalCenter = b.center,
                Radius = b.extents.magnitude,
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
            public readonly InstanceList Near = new InstanceList(); // inside ShadowDistance: casts shadows
            public readonly InstanceList Far = new InstanceList();
        }

        private sealed class InstanceList
        {
            public Matrix4x4[] Matrices = new Matrix4x4[64];
            public int Count;
            private Vector3 _min, _max;

            public void Reset()
            {
                Count = 0;
                _min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
                _max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            }

            public bool Add(in Matrix4x4 m, Vector3 center, float radius)
            {
                if (Count == Matrices.Length)
                {
                    if (Count >= MaxInstancesPerList) return false;
                    Array.Resize(ref Matrices, Count * 2); // amortised: only while the working set grows
                }
                Matrices[Count++] = m;
                if (center.x - radius < _min.x) _min.x = center.x - radius;
                if (center.y - radius < _min.y) _min.y = center.y - radius;
                if (center.z - radius < _min.z) _min.z = center.z - radius;
                if (center.x + radius > _max.x) _max.x = center.x + radius;
                if (center.y + radius > _max.y) _max.y = center.y + radius;
                if (center.z + radius > _max.z) _max.z = center.z + radius;
                return true;
            }

            public Bounds Bounds()
            {
                var b = new Bounds();
                b.SetMinMax(_min, _max);
                return b;
            }
        }

        // ------------------------------------------------------------------ producer API

        /// <summary>What a <see cref="IWormRenderSource"/> pushes its instances into.</summary>
        public sealed class Sink
        {
            private readonly WormRenderer _r;

            internal Sink(WormRenderer renderer) { _r = renderer; }

            public void AddWorm(WormAnim anim, int frame, in Matrix4x4 trs)
            {
                Batch[] frames = _r._anim[(int)anim];
                if (frames == null || frames.Length == 0) return;
                _r.Add(frames[Flipbook.Wrap(frame, frames.Length)], trs);
            }

            public void AddWorm(string animation, int frame, in Matrix4x4 trs) => AddWorm(Parse(animation), frame, trs);

            public void AddDome(int frame, in Matrix4x4 trs)
            {
                Batch[] frames = _r._dome;
                if (frames == null || frames.Length == 0) return;
                _r.Add(frames[Flipbook.Wrap(frame, frames.Length)], trs);
            }

            public void AddNest(in Matrix4x4 trs)
            {
                if (_r._nest != null) _r.Add(_r._nest, trs);
            }

            public int FrameCount(WormAnim anim)
            {
                Batch[] frames = _r._anim[(int)anim];
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

            public int DomeFrameAt(float time, float phase = 0f) =>
                Flipbook.FrameAt(_r._dome != null ? _r._dome.Length : 0, _r._domeFps, true, time, phase);

            public static WormAnim Parse(string animation)
            {
                switch (animation)
                {
                    case CrawlName: return WormAnim.Crawl;
                    case RearName: return WormAnim.Rear;
                    case StrikeName: return WormAnim.Strike;
                    case SleepName: return WormAnim.Sleep;
                    default: throw new ArgumentException("unknown worm animation '" + animation + "'");
                }
            }
        }
    }
}
