using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// Feeds the skin engulf with the real surface of the engulfed target, only for the target of a mass. The surface is the skin: the anchors are a fixed subset of at most
    /// <see cref="WormSimConst.MaxAnchors"/> vertices (<see cref="GpuSkinAnchors"/>), chosen once per target and per set of meshes. At the selection everything static is read and
    /// uploaded once: the rest positions and normals of the anchor vertices, their four bones and weights, the 8 nearest anchors of each, the paths over the surface
    /// (<see cref="GpuSkinSet"/>). Each frame the only thing the CPU does is upload the matrices of the bones the anchors use (about 53), <c>bone.localToWorldMatrix * bindpose</c>
    /// relative to the origin of the mass; the compute shader (KSkin) skins the anchors: <c>p = sum(w * M * v)</c>. That is exact whatever the scales of the rig (Valheim rigs carry a
    /// scale of about 0.01: <c>SkinnedMeshRenderer.BakeMesh</c> gives results that are 100 times too big or too small depending on <c>useScale</c>). A structure is read from its
    /// <c>MeshFilter</c> (one bone: its transform). Meshes that are not readable are skipped (and logged). Everything is preallocated at the selection: nothing is allocated per frame.
    /// </summary>
    internal sealed class GpuSkinSampler
    {
        private const int MaxSources = 12;

        private readonly List<SkinnedMeshRenderer> _smrFound = new List<SkinnedMeshRenderer>(MaxSources);
        private readonly List<MeshFilter> _mfFound = new List<MeshFilter>(MaxSources);
        private readonly List<SkinnedMeshRenderer> _smrAll = new List<SkinnedMeshRenderer>(32);
        private readonly SkinnedMeshRenderer[] _smr = new SkinnedMeshRenderer[MaxSources];
        private readonly MeshFilter[] _mf = new MeshFilter[MaxSources];
        // per source: the mesh as it is (rest pose, mesh space), the weights of its vertices, the bones and their bind poses, and the matrices of the bones for the selection
        private readonly Vector3[][] _rest = new Vector3[MaxSources][];
        private readonly Vector3[][] _restN = new Vector3[MaxSources][];
        private readonly BoneWeight[][] _weights = new BoneWeight[MaxSources][];
        private readonly Matrix4x4[][] _bindposes = new Matrix4x4[MaxSources][];
        private readonly Transform[][] _bones = new Transform[MaxSources][];
        // the bones the anchors use (indices of the vertices of the set), and the matrices uploaded each frame
        private readonly Transform[] _usedT = new Transform[WormSimConst.MaxBones];
        private readonly Matrix4x4[] _usedBind = new Matrix4x4[WormSimConst.MaxBones];
        private readonly Matrix4x4[] _upload = new Matrix4x4[WormSimConst.MaxBones];
        private int _usedCount;
        private GpuSkinSet _set;
        private GpuMass _uploadedTo;
        private int _sources, _signature;
        private float _area;
        private Transform _root;
        private bool _structure;

        public int Count => _set != null ? _set.Count : 0;
        public int Sources => _sources;
        public float Area => _area;
        public bool Selected => Count > 0;
        public int UsedBones => _usedCount;
        public GpuSkinSet Set => _set;

        // cost of the sampling (main thread)
        public int Samples { get; private set; }
        public double TotalMs { get; private set; }
        public double MaxMs { get; private set; }
        public double LastSelectMs { get; private set; }
        /// <summary>The longest call of <see cref="Sample"/> that started a selection or took its result (main thread): the spike at the start of an engulf.</summary>
        public double SpikeMs { get; private set; }
        /// <summary>The same, for a selection that read the meshes and chose the anchors (first engulf of a mesh), and for one that took them from the cache.</summary>
        public double SpikeUncachedMs { get; private set; }
        public double SpikeCachedMs { get; private set; }
        /// <summary>Where the time of the last selection went, main thread (ms): reading and skinning, mapping the bones and logging, uploading the static data to the GPU.</summary>
        public string SpikeBreakdown { get; private set; } = "none";
        private double _applyMs, _uploadMs;
        public void ResetSpike() { SpikeMs = 0; SpikeUncachedMs = 0; SpikeCachedMs = 0; }
        public string Description { get; private set; } = "none";
        /// <summary>The Animator of the target, looked up once per selection, here and nowhere else: found or not, humanoid avatar or not.</summary>
        public string AnimatorInfo { get; private set; } = "not looked up";
        public float MeshArea { get; private set; }

        public void ResetStats() { Samples = 0; TotalMs = 0; MaxMs = 0; }

        /// <summary>The meshes that make the surface of <paramref name="root"/>: its skinned meshes, else (a structure) its mesh filters.</summary>
        private void Collect(Transform root)
        {
            _smrFound.Clear();
            _mfFound.Clear();
            root.GetComponentsInChildren(false, _smrFound);
            _sources = 0;
            _structure = false;
            foreach (SkinnedMeshRenderer r in _smrFound)
            {
                if (_sources >= MaxSources) break;
                Mesh mesh = r != null ? r.sharedMesh : null;
                if (r == null || !r.enabled || mesh == null || mesh.vertexCount == 0 || !mesh.isReadable || mesh.bindposeCount == 0) continue;      // (SkinnedMeshRenderer.bones allocates: the bones are read at the selection)
                _smr[_sources] = r;
                _mf[_sources] = null;
                _sources++;
            }
            if (_sources > 0) return;
            root.GetComponentsInChildren(false, _mfFound);
            foreach (MeshFilter f in _mfFound)
            {
                if (_sources >= MaxSources) break;
                if (f == null || f.sharedMesh == null || !f.sharedMesh.isReadable || f.sharedMesh.vertexCount == 0) continue;
                MeshRenderer mr = f.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled) continue;
                _smr[_sources] = null;
                _mf[_sources] = f;
                _sources++;
                _structure = true;
            }
        }

        private int Signature()
        {
            int sig = _sources * 31;
            for (int i = 0; i < _sources; i++)
                sig = sig * 31 + (_smr[i] != null ? _smr[i].sharedMesh.vertexCount : _mf[i].sharedMesh.vertexCount);
            return sig;
        }

        /// <summary>
        /// Uploads the matrices of the bones of the anchors of <paramref name="m"/> for this frame (and, the first time or after a new selection, everything static). Selects the anchors
        /// on the skin of <paramref name="root"/> when needed (<paramref name="foot"/> and <paramref name="height"/>: the target, for the height of an anchor over the height of the
        /// target). False when there is no usable mesh (the caller falls back on the capsule).
        /// </summary>
        public bool Sample(GpuMass m, Transform root, float foot, float height)
        {
            long t0 = Stopwatch.GetTimestamp();
            bool ok = false, spike = false;
            if (root != null && height > 0.05f)
            {
                if (_pendingTask != null && root == _root)
                {
                    spike = TakePending();           // the worker is still choosing the anchors (the capsules draw the target meanwhile), or has just finished
                }
                else if (root != _root || _set == null || SourceLost())
                {
                    Collect(root);
                    int sig = Signature();
                    if (root != _root || sig != _signature || _set == null)
                    {
                        _root = root;
                        _signature = sig;
                        Select(foot, height);
                        spike = true;
                    }
                }
                ok = _set != null && _set.Count > 0 && Update(m, height);
            }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (spike)
            {
                if (ms > SpikeMs) SpikeMs = ms;
                if (LastFromCache) { if (ms > SpikeCachedMs) SpikeCachedMs = ms; }
                else if (ms > SpikeUncachedMs) SpikeUncachedMs = ms;
                SpikeBreakdown = $"call {ms:0.00} ms = selection {LastSelectMs:0.00} + bones, cache and log {_applyMs:0.00} + upload to the GPU {_uploadMs:0.00} (+ rest {ms - LastSelectMs - _applyMs - _uploadMs:0.00})";
            }
            Samples++;
            TotalMs += ms;
            if (ms > MaxMs) MaxMs = ms;
            return ok;
        }

        // A source that was destroyed or disabled (an item unequipped): the anchors are chosen again. Nothing is searched in the hierarchy each frame (no allocation).
        private bool SourceLost()
        {
            for (int i = 0; i < _sources; i++)
            {
                if (_smr[i] != null) { if (!_smr[i].enabled) return true; }
                else if (_mf[i] == null) return true;
            }
            return false;
        }

        private const int MaxCandidates = 150000;
        private readonly int[] _infId = new int[12];
        private readonly float[] _infW = new float[12];

        private static void AddInfluence(int[] ids, float[] ws, ref int n, BoneWeight bw, float f)
        {
            AddOne(ids, ws, ref n, bw.boneIndex0, bw.weight0 * f);
            AddOne(ids, ws, ref n, bw.boneIndex1, bw.weight1 * f);
            AddOne(ids, ws, ref n, bw.boneIndex2, bw.weight2 * f);
            AddOne(ids, ws, ref n, bw.boneIndex3, bw.weight3 * f);
        }

        private static void AddOne(int[] ids, float[] ws, ref int n, int id, float w)
        {
            if (!(w > 0f)) return;
            for (int i = 0; i < n; i++) if (ids[i] == id) { ws[i] += w; return; }
            if (n < ids.Length) { ids[n] = id; ws[n] = w; n++; }
        }

        /// <summary>Lattice points (about 3 cm apart) on every triangle of a source, as candidate anchors (world position of the pose of now, the three vertices and the barycentric weights).</summary>
        private static void AddTriangleSamples(int src, int[] tris, Vector3[] world, List<Vector3> all, List<int> srcOf, List<int> vtxOf, List<Vector3Int> triV, List<Vector3> triB)
        {
            const float spacing = 0.03f;
            for (int k = 0; k + 2 < tris.Length; k += 3)
            {
                int i0 = tris[k], i1 = tris[k + 1], i2 = tris[k + 2];
                Vector3 p0 = world[i0], p1 = world[i1], p2 = world[i2];
                float longest = Mathf.Max((p1 - p0).magnitude, Mathf.Max((p2 - p1).magnitude, (p0 - p2).magnitude));
                int m = Mathf.Min(24, Mathf.CeilToInt(longest / spacing));
                if (m < 2) continue;                        // a small triangle: its vertices are enough
                if (all.Count > MaxCandidates) return;
                for (int a = 0; a <= m; a++)
                    for (int b = 0; a + b <= m; b++)
                    {
                        int c = m - a - b;
                        if ((a == m) || (b == m) || (c == m)) continue;          // the vertices are candidates already
                        float fa = a / (float)m, fb = b / (float)m;
                        Vector3 p = p0 * fa + p1 * fb + p2 * (1f - fa - fb);
                        all.Add(p); srcOf.Add(src);
                        int dominant = fa >= fb && fa >= 1f - fa - fb ? i0 : (fb >= 1f - fa - fb ? i1 : i2);
                        vtxOf.Add(dominant);
                        triV.Add(new Vector3Int(i0, i1, i2));
                        triB.Add(new Vector3(fa, fb, 0f));
                    }
            }
        }

        // ------------------------------------------------------------------ caches

        /// <summary>What is read from a mesh asset, once per mesh whatever the number of targets that wear it (a player mesh is read once, not at every engulf).</summary>
        private sealed class MeshData
        {
            public Vector3[] Rest, RestN;
            public BoneWeight[] Weights;
            public Matrix4x4[] Bindposes;
            public int[] Tris;
        }

        private static readonly Dictionary<int, MeshData> s_meshData = new Dictionary<int, MeshData>();

        /// <summary>The anchors chosen for a set of skinned meshes, shared by every target that wears them: the choice is made on the first, the rest only maps the bones to its own transforms.</summary>
        private sealed class CachedSet
        {
            public GpuSkinSet Set;
            public List<int> BoneKeys;
            public float Area, MeshArea, Thinned;
            public int Candidates;
        }

        private static readonly Dictionary<long, CachedSet> s_sets = new Dictionary<long, CachedSet>();

        /// <summary>Number of meshes and of sets in the caches (dev checks).</summary>
        public static int CachedMeshes => s_meshData.Count;
        public static int CachedSets => s_sets.Count;
        /// <summary>True when the last selection came from the cache.</summary>
        public bool LastFromCache { get; private set; }

#if MW_DEV
        /// <summary>Dev (V14): empties the caches of the meshes and of the sets, so that the next selection reads the meshes and chooses the anchors again (the first engulf of a mesh).</summary>
        internal static void DevClearCaches() { s_meshData.Clear(); s_sets.Clear(); }

        /// <summary>
        /// Dev (V14): forgets the selection of this sampler (not the caches): the next call selects again, from the cache when the set is in it. A job still running is dropped
        /// (its identifier is stale). The spike counters are reset.
        /// </summary>
        internal void DevForget()
        {
            _set = null;
            _root = null;
            _signature = 0;
            _uploadedTo = null;
            _pendingTask = null;
            _pendingJob = null;
            _selectId++;
            _usedCount = 0;
            ResetSpike();
        }

        /// <summary>Dev (V14): true while the worker still chooses the anchors.</summary>
        internal bool DevSelectionPending => _pendingTask != null;
#endif

        private static MeshData DataOf(Mesh mesh)
        {
            int id = mesh.GetInstanceID();
            if (s_meshData.TryGetValue(id, out MeshData d)) return d;
            d = new MeshData { Rest = mesh.vertices, RestN = mesh.normals, Weights = mesh.boneWeights, Bindposes = mesh.bindposes, Tris = mesh.triangles };
            if (d.RestN.Length != d.Rest.Length) d.RestN = new Vector3[d.Rest.Length];
            if (d.Weights.Length != d.Rest.Length) { d.Weights = new BoneWeight[d.Rest.Length]; for (int v = 0; v < d.Weights.Length; v++) d.Weights[v].weight0 = 1f; }
            s_meshData[id] = d;
            return d;
        }

        private long SetKey()
        {
            if (_structure) return 0;       // a structure is read in its own scale and rotation: never shared
            long key = 17;
            for (int i = 0; i < _sources; i++) key = key * 1000003L + _smr[i].sharedMesh.GetInstanceID() * 31L + _smr[i].bones.Length;
            return key;
        }

        // The anchors are chosen among the vertices of the pose of now (all the sources together), once per set of meshes; the weights and the bind poses are read once per mesh.
        // The main thread only reads the meshes it has not seen and the 50 or so matrices of the bones; the vertices are skinned and the anchors chosen on a worker thread.
        private void Select(float foot, float height)
        {
            long t0 = Stopwatch.GetTimestamp();
            _set = null;
            _uploadedTo = null;
            Animator anim = _root.GetComponentInChildren<Animator>(true);
            AnimatorInfo = $"Animator {(anim != null ? anim.name + " (" + (anim.isHuman ? "humanoid avatar" : "NOT humanoid") + ")" : "none")}";

            var job = new SelectJob { Foot = foot, Height = height, Sources = _sources, Id = ++_selectId };
            for (int i = 0; i < _sources; i++)
            {
                Mesh mesh = _smr[i] != null ? _smr[i].sharedMesh : _mf[i].sharedMesh;
                MeshData d = DataOf(mesh);
                _bindposes[i] = d.Bindposes;
                if (_smr[i] != null)
                {
                    _bones[i] = _smr[i].bones;
                    if (_bones[i].Length == 0 || _bones[i].Length != d.Bindposes.Length)
                    {
                        Plugin.Log.LogWarning($"[GpuSkin]   {_smr[i].name}: {_bones[i].Length} bones for {d.Bindposes.Length} bind poses, skipped");
                        job.Skip[i] = true;
                        continue;
                    }
                    var bm = new Matrix4x4[_bones[i].Length];
                    for (int b = 0; b < bm.Length; b++) bm[b] = _bones[i][b] != null ? _bones[i][b].localToWorldMatrix * d.Bindposes[b] : Matrix4x4.identity;
                    job.BoneM[i] = bm;
                    job.Weights[i] = d.Weights;
                }
                else job.Local2World[i] = _mf[i].transform.localToWorldMatrix;
                job.IsFilter[i] = _mf[i] != null;
                job.Rest[i] = d.Rest;
                job.RestN[i] = d.RestN;
                job.Tris[i] = d.Tris;
            }
            MeshArea = 0f;

            long key = SetKey();
            if (key != 0 && s_sets.TryGetValue(key, out CachedSet cached))
            {
                ApplySet(cached.Set, cached.BoneKeys, cached.Area, cached.MeshArea, cached.Thinned, cached.Candidates, 0.0, 0);
                LastFromCache = true;
                LastSelectMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
                return;
            }
            LastFromCache = false;
            LastSelectMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            job.CacheKey = key;
            _pendingJob = job;
            _pendingTask = System.Threading.Tasks.Task.Run(() => Compute(job));
        }

        /// <summary>The bones of the anchors of a set, mapped to the transforms of this target.</summary>
        private void ApplySet(GpuSkinSet set, List<int> boneKeys, float area, float meshArea, float thinned, int candidates, double workerMs, int dropped)
        {
            long a0 = Stopwatch.GetTimestamp();
            _usedCount = boneKeys.Count;
            for (int slot = 0; slot < _usedCount; slot++)
            {
                int src = boneKeys[slot] / 4096, bone = boneKeys[slot] % 4096;
                if (_mf[src] != null) { _usedT[slot] = _mf[src].transform; _usedBind[slot] = Matrix4x4.identity; }
                else { _usedT[slot] = _bones[src][bone]; _usedBind[slot] = _bindposes[src][bone]; }
            }
            _area = area;
            MeshArea = meshArea;
            _set = set;
            Description = $"{(_structure ? "structure mesh filters" : "skinned meshes (GPU skinning of the anchors)")}: {_sources} source(s), {candidates} candidates, mesh area {meshArea:0.00} m2, {set.Count} anchors (thinning estimate {thinned:0.00} m2), surface used {area:0.00} m2, " +
                          $"{_usedCount} bones, {(workerMs > 0.0 ? $"{LastSelectMs:0.0} ms on the main thread + {workerMs:0.0} ms on a worker" : "from the cache")}; {AnimatorInfo}";
            Plugin.Log.LogInfo($"[GpuSkin] target {_root.name}: {Description}{(dropped > 0 ? $"; WARNING {dropped} bone influences dropped (more than {WormSimConst.MaxBones} bones)" : string.Empty)}");
            _applyMs = (Stopwatch.GetTimestamp() - a0) * 1000.0 / Stopwatch.Frequency;
        }

        private int _selectId;
        private SelectJob _pendingJob;
        private System.Threading.Tasks.Task _pendingTask;

        /// <summary>Everything the worker thread needs and returns: plain arrays, no Unity object.</summary>
        private sealed class SelectJob
        {
            public int Id, Sources;
            public float Foot, Height;
            public long CacheKey;
            public readonly bool[] Skip = new bool[MaxSources];
            public readonly Matrix4x4[][] BoneM = new Matrix4x4[MaxSources][];
            public readonly Matrix4x4[] Local2World = new Matrix4x4[MaxSources];
            public readonly Vector3[][] World = new Vector3[MaxSources][];
            public readonly int[][] Tris = new int[MaxSources][];
            public readonly Vector3[][] Rest = new Vector3[MaxSources][];
            public readonly Vector3[][] RestN = new Vector3[MaxSources][];
            public readonly BoneWeight[][] Weights = new BoneWeight[MaxSources][];
            public readonly bool[] IsFilter = new bool[MaxSources];
            // result
            public GpuSkinSet Set;
            public readonly List<int> BoneKeys = new List<int>(64);       // src * 4096 + bone, by slot
            public int Dropped, Candidates;
            public float MeshArea, Thinned, Area;
            public double Ms;
            public string Error;
        }

        /// <summary>The work of the selection, on a worker thread (pure maths on arrays).</summary>
        private static void Compute(SelectJob job)
        {
            long t0 = Stopwatch.GetTimestamp();
            try
            {
                var all = new List<Vector3>(8192);
                var srcOf = new List<int>(8192);
                var vtxOf = new List<int>(8192);
                // A candidate is a vertex or a point of a triangle (barycentric): the mesh of a character is dense where it is detailed (hands, face) and sparse elsewhere, so that the
                // vertices alone leave holes of 15 cm and more on the torso and the thighs, where the worms would not be held by any anchor.
                var triV = new List<Vector3Int>(8192);
                var triB = new List<Vector3>(8192);
                float meshArea = 0f;
                for (int i = 0; i < job.Sources; i++)
                {
                    if (job.Skip[i] || job.Rest[i] == null) { job.World[i] = new Vector3[0]; continue; }
                    Vector3[] world = SkinSource(job, i);
                    job.World[i] = world;
                    for (int v = 0; v < world.Length; v++)
                    {
                        all.Add(world[v]); srcOf.Add(i); vtxOf.Add(v); triV.Add(new Vector3Int(v, v, v)); triB.Add(new Vector3(1f, 0f, 0f));
                    }
                    int[] tris = job.Tris[i];
                    float area = 0f;
                    for (int k = 0; k + 2 < tris.Length; k += 3) area += Vector3.Cross(world[tris[k + 1]] - world[tris[k]], world[tris[k + 2]] - world[tris[k]]).magnitude * 0.5f;
                    meshArea += area;
                    AddTriangleSamples(i, tris, world, all, srcOf, vtxOf, triV, triB);
                }
                job.MeshArea = meshArea;
                job.Candidates = all.Count;
                var picks = new int[WormSimConst.MaxAnchors];
                int count = GpuSkinAnchors.Select(all, all.Count, WormSimConst.MaxAnchors, picks, out float thinned);
                job.Thinned = thinned;
                float areaUsed = meshArea > thinned ? meshArea : thinned;      // the surface the worms can cover is the real one (the thinning of the anchors counts only the area it samples)
                job.Area = areaUsed;

                var set = new GpuSkinSet { Count = count, Area = areaUsed };
                var pose = new Vector3[count];
                var map = new Dictionary<int, int>(128);
                var infId = new int[12];
                var infW = new float[12];
                for (int a = 0; a < count; a++)
                {
                    int ci = picks[a];
                    int src = srcOf[ci];
                    Vector3Int tv = triV[ci];
                    Vector3 bc = triB[ci];
                    Vector3 wp = all[ci];
                    pose[a] = wp;
                    float b2 = 1f - bc.x - bc.y;
                    Vector3[] rest = job.Rest[src], restN = job.RestN[src];
                    Vector3 rp = rest[tv.x] * bc.x + rest[tv.y] * bc.y + rest[tv.z] * b2;
                    Vector3 rn = restN[tv.x] * bc.x + restN[tv.y] * bc.y + restN[tv.z] * b2;
                    float rnl = rn.magnitude;
                    rn = rnl > 1e-6f ? rn / rnl : restN[tv.x];
                    var sv = new GpuSkinVertex { PosRel = new Vector4(rp.x, rp.y, rp.z, Mathf.Clamp01((wp.y - job.Foot) / job.Height)), Normal = new Vector4(rn.x, rn.y, rn.z, 0f) };
                    if (job.IsFilter[src])
                    {
                        sv.Bone0 = (uint)Slot(job, map, src, 0);
                        sv.Weights = new Vector4(1f, 0f, 0f, 0f);
                    }
                    else
                    {
                        // the influences of the three vertices of the triangle, mixed by the barycentric weights, the four strongest kept
                        BoneWeight[] weights = job.Weights[src];
                        int ni = 0;
                        AddInfluence(infId, infW, ref ni, weights[tv.x], bc.x);
                        if (bc.y > 0f) AddInfluence(infId, infW, ref ni, weights[tv.y], bc.y);
                        if (b2 > 1e-6f) AddInfluence(infId, infW, ref ni, weights[tv.z], b2);
                        var ids = new int[4];
                        var ws = new float[4];
                        for (int k = 0; k < 4; k++)
                        {
                            int bi = -1;
                            for (int q = 0; q < ni; q++) if (infW[q] > 0f && (bi < 0 || infW[q] > infW[bi])) bi = q;
                            if (bi < 0) break;
                            ids[k] = infId[bi]; ws[k] = infW[bi]; infW[bi] = 0f;
                        }
                        var slots = new uint[4];
                        float sum = 0f;
                        for (int k = 0; k < 4; k++)
                        {
                            if (!(ws[k] > 0f)) { ws[k] = 0f; continue; }
                            int slot = Slot(job, map, src, ids[k]);
                            if (slot < 0) { ws[k] = 0f; job.Dropped++; continue; }
                            slots[k] = (uint)slot;
                            sum += ws[k];
                        }
                        if (sum <= 0f) { ws[0] = 1f; sum = 1f; slots[0] = 0u; }
                        sv.Bone0 = slots[0]; sv.Bone1 = slots[1]; sv.Bone2 = slots[2]; sv.Bone3 = slots[3];
                        sv.Weights = new Vector4(ws[0] / sum, ws[1] / sum, ws[2] / sum, ws[3] / sum);
                    }
                    set.Verts[a] = sv;
                }
                set.BoneCount = job.BoneKeys.Count;
                set.Finish(pose);
                job.Set = set;
            }
            catch (System.Exception e)
            {
                job.Error = e.ToString();
            }
            job.Ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
        }

        /// <summary>World position of every vertex of a source in the pose of now: skinned by the weights with the matrices of the bones, or transformed for a mesh filter.</summary>
        private static Vector3[] SkinSource(SelectJob job, int src)
        {
            Vector3[] rest = job.Rest[src];
            if (job.Skip[src] || rest == null) return new Vector3[0];
            var world = new Vector3[rest.Length];
            if (job.IsFilter[src])
            {
                Matrix4x4 l2w = job.Local2World[src];
                for (int v = 0; v < rest.Length; v++) world[v] = l2w.MultiplyPoint3x4(rest[v]);
                return world;
            }
            BoneWeight[] weights = job.Weights[src];
            Matrix4x4[] bm = job.BoneM[src];
            for (int v = 0; v < rest.Length; v++)
            {
                Vector3 p = rest[v];
                BoneWeight bw = weights[v];
                Vector3 sp = Vector3.zero;
                if (bw.weight0 > 0f) sp += bm[bw.boneIndex0].MultiplyPoint3x4(p) * bw.weight0;
                if (bw.weight1 > 0f) sp += bm[bw.boneIndex1].MultiplyPoint3x4(p) * bw.weight1;
                if (bw.weight2 > 0f) sp += bm[bw.boneIndex2].MultiplyPoint3x4(p) * bw.weight2;
                if (bw.weight3 > 0f) sp += bm[bw.boneIndex3].MultiplyPoint3x4(p) * bw.weight3;
                world[v] = sp;
            }
            return world;
        }

        // The slot of a bone among the bones of the anchors (added when new); -1 when the table is full.
        private static int Slot(SelectJob job, Dictionary<int, int> map, int src, int bone)
        {
            int key = src * 4096 + bone;
            if (map.TryGetValue(key, out int slot)) return slot;
            if (job.BoneKeys.Count >= WormSimConst.MaxBones) return -1;
            slot = job.BoneKeys.Count;
            job.BoneKeys.Add(key);
            map[key] = slot;
            return slot;
        }

        /// <summary>Takes the result of the worker when it is done (main thread). True when the set is there.</summary>
        private bool TakePending()
        {
            if (_pendingTask == null) return _set != null;
            if (!_pendingTask.IsCompleted) return false;
            SelectJob job = _pendingJob;
            _pendingTask = null;
            _pendingJob = null;
            if (job.Id != _selectId) return false;                  // an older selection (the target changed since)
            if (job.Error != null || job.Set == null)
            {
                Plugin.Log.LogWarning("[GpuSkin] selection failed: " + job.Error);
                _signature = 0;
                return false;
            }
            if (job.CacheKey != 0) s_sets[job.CacheKey] = new CachedSet { Set = job.Set, BoneKeys = job.BoneKeys, Area = job.Area, MeshArea = job.MeshArea, Thinned = job.Thinned, Candidates = job.Candidates };
            ApplySet(job.Set, job.BoneKeys, job.Area, job.MeshArea, job.Thinned, job.Candidates, job.Ms, job.Dropped);
            return true;
        }

        // The whole per frame job: the matrices of the bones of the anchors, relative to the origin of the mass.
        private bool Update(GpuMass m, float height)
        {
            if (_uploadedTo != m || m.SkinSet != _set)
            {
                long u0 = Stopwatch.GetTimestamp();
                GpuWormHost.UploadSkin(m, _set);
                _uploadMs = (Stopwatch.GetTimestamp() - u0) * 1000.0 / Stopwatch.Frequency;
                _uploadedTo = m;
            }
            Vector3 o = m.Origin;
            for (int b = 0; b < _usedCount; b++)
            {
                Transform t = _usedT[b];
                Matrix4x4 mm = t != null ? t.localToWorldMatrix * _usedBind[b] : Matrix4x4.identity;
                mm.m03 -= o.x; mm.m13 -= o.y; mm.m23 -= o.z;
                _upload[b] = mm;
            }
            GpuWormHost.UploadBones(m, _upload, _usedCount);
            m.AnchorCount = _set.Count;
            m.TargetHeight = height;
            m.SurfaceArea = _set.Area;
            return true;
        }
    }
}
