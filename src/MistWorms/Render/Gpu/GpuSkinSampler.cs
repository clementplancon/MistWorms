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
        private readonly Matrix4x4[][] _boneM = new Matrix4x4[MaxSources][];
        private readonly int[][] _boneStamp = new int[MaxSources][];
        private int _stamp;
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

        private Matrix4x4 BoneMatrix(int src, int bone)
        {
            if (_boneStamp[src][bone] != _stamp)
            {
                Transform t = _bones[src][bone];
                _boneM[src][bone] = t != null ? t.localToWorldMatrix * _bindposes[src][bone] : Matrix4x4.identity;
                _boneStamp[src][bone] = _stamp;
            }
            return _boneM[src][bone];
        }

        /// <summary>World position of a vertex of a source in the pose of now (skinned by the weights, or transformed for a mesh filter): only for the selection.</summary>
        private Vector3 SkinVertex(int src, int v)
        {
            Vector3 p = _rest[src][v];
            if (_mf[src] != null) return _mf[src].transform.TransformPoint(p);
            BoneWeight bw = _weights[src][v];
            Vector3 sp = Vector3.zero;
            if (bw.weight0 > 0f) sp += BoneMatrix(src, bw.boneIndex0).MultiplyPoint3x4(p) * bw.weight0;
            if (bw.weight1 > 0f) sp += BoneMatrix(src, bw.boneIndex1).MultiplyPoint3x4(p) * bw.weight1;
            if (bw.weight2 > 0f) sp += BoneMatrix(src, bw.boneIndex2).MultiplyPoint3x4(p) * bw.weight2;
            if (bw.weight3 > 0f) sp += BoneMatrix(src, bw.boneIndex3).MultiplyPoint3x4(p) * bw.weight3;
            return sp;
        }

        /// <summary>
        /// Uploads the matrices of the bones of the anchors of <paramref name="m"/> for this frame (and, the first time or after a new selection, everything static). Selects the anchors
        /// on the skin of <paramref name="root"/> when needed (<paramref name="foot"/> and <paramref name="height"/>: the target, for the height of an anchor over the height of the
        /// target). False when there is no usable mesh (the caller falls back on the capsule).
        /// </summary>
        public bool Sample(GpuMass m, Transform root, float foot, float height)
        {
            long t0 = Stopwatch.GetTimestamp();
            bool ok = false;
            if (root != null && height > 0.05f)
            {
                if (root != _root || _set == null || SourceLost())
                {
                    Collect(root);
                    int sig = Signature();
                    if (root != _root || sig != _signature || _set == null)
                    {
                        _root = root;
                        _signature = sig;
                        Select(foot, height);
                    }
                }
                ok = _set != null && _set.Count > 0 && Update(m, height);
            }
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
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

        // The slot of a bone among the bones of the anchors (added when new); -1 when the table is full.
        private int BoneSlot(Dictionary<int, int> map, int src, int bone)
        {
            int key = src * 4096 + bone;
            if (map.TryGetValue(key, out int slot)) return slot;
            if (_usedCount >= WormSimConst.MaxBones) return -1;
            slot = _usedCount++;
            map[key] = slot;
            if (_mf[src] != null) { _usedT[slot] = _mf[src].transform; _usedBind[slot] = Matrix4x4.identity; }
            else { _usedT[slot] = _bones[src][bone]; _usedBind[slot] = _bindposes[src][bone]; }
            return slot;
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

        // The anchors are chosen among the vertices of the pose of now (all the sources together), once; the weights and the bind poses are read once.
        private void Select(float foot, float height)
        {
            long t0 = Stopwatch.GetTimestamp();
            _set = null;
            _uploadedTo = null;
            _root.GetComponentsInChildren(true, _smrAll);
            Animator anim = _root.GetComponentInChildren<Animator>(true);
            AnimatorInfo = $"Animator {(anim != null ? anim.name + " (" + (anim.isHuman ? "humanoid avatar" : "NOT humanoid") + ")" : "none")}";
            Plugin.Log.LogInfo($"[GpuSkin] root {_root.name}: {_smrAll.Count} skinned mesh renderer(s) including the inactive ones, {_sources} used; {AnimatorInfo}");
            foreach (SkinnedMeshRenderer r in _smrAll)
            {
                bool used = false;
                for (int i = 0; i < _sources; i++) if (_smr[i] == r) used = true;
                Mesh sm = r.sharedMesh;
                Plugin.Log.LogInfo($"[GpuSkin]   {r.name}: {(used ? "used" : "NOT used")}, enabled {r.enabled}, active {r.gameObject.activeInHierarchy}, vertices {(sm != null ? sm.vertexCount : 0)}, " +
                                   $"readable {(sm != null && sm.isReadable)}, bones {r.bones.Length}, bounds {r.bounds.size.ToString("0.00")}");
            }

            var all = new List<Vector3>(8192);
            var srcOf = new List<int>(8192);
            var vtxOf = new List<int>(8192);
            // A candidate is a vertex or a point of a triangle (barycentric): the mesh of a character is dense where it is detailed (hands, face) and sparse elsewhere, so that the
            // vertices alone leave holes of 15 cm and more on the torso and the thighs, where the worms would not be held by any anchor.
            var triV = new List<Vector3Int>(8192);
            var triB = new List<Vector3>(8192);
            MeshArea = 0f;
            for (int i = 0; i < _sources; i++)
            {
                Mesh mesh = _smr[i] != null ? _smr[i].sharedMesh : _mf[i].sharedMesh;
                _rest[i] = mesh.vertices;
                _restN[i] = mesh.normals.Length == mesh.vertexCount ? mesh.normals : new Vector3[mesh.vertexCount];
                if (_smr[i] != null)
                {
                    _weights[i] = mesh.boneWeights;
                    _bindposes[i] = mesh.bindposes;
                    _bones[i] = _smr[i].bones;
                    if (_bones[i].Length == 0 || _bones[i].Length != _bindposes[i].Length)
                    {
                        Plugin.Log.LogWarning($"[GpuSkin]   {_smr[i].name}: {_bones[i].Length} bones for {_bindposes[i].Length} bind poses, skipped");
                        _rest[i] = new Vector3[0]; _restN[i] = new Vector3[0]; _weights[i] = new BoneWeight[0]; _boneM[i] = new Matrix4x4[0]; _boneStamp[i] = new int[0];
                        continue;
                    }
                    _boneM[i] = new Matrix4x4[_bones[i].Length];
                    _boneStamp[i] = new int[_bones[i].Length];
                    for (int b = 0; b < _boneStamp[i].Length; b++) _boneStamp[i][b] = -1;
                    if (_weights[i].Length != mesh.vertexCount) { _weights[i] = new BoneWeight[mesh.vertexCount]; for (int v = 0; v < mesh.vertexCount; v++) _weights[i][v].weight0 = 1f; }
                }
            }
            _stamp++;
            var worldOf = new Vector3[_sources][];
            for (int i = 0; i < _sources; i++)
            {
                int n = _rest[i].Length;
                worldOf[i] = new Vector3[n];
                if (n == 0) continue;
                for (int v = 0; v < n; v++)
                {
                    Vector3 p = SkinVertex(i, v);
                    worldOf[i][v] = p;
                    all.Add(p); srcOf.Add(i); vtxOf.Add(v); triV.Add(new Vector3Int(v, v, v)); triB.Add(new Vector3(1f, 0f, 0f));
                }
                Mesh mesh = _smr[i] != null ? _smr[i].sharedMesh : _mf[i].sharedMesh;
                int[] tris = mesh.triangles;
                float area = 0f;
                Vector3 mn = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue), mx = -mn;
                for (int k = 0; k + 2 < tris.Length; k += 3) area += Vector3.Cross(worldOf[i][tris[k + 1]] - worldOf[i][tris[k]], worldOf[i][tris[k + 2]] - worldOf[i][tris[k]]).magnitude * 0.5f;
                AddTriangleSamples(i, tris, worldOf[i], all, srcOf, vtxOf, triV, triB);
                foreach (Vector3 w in worldOf[i]) { mn = Vector3.Min(mn, w); mx = Vector3.Max(mx, w); }
                MeshArea += area;
                Plugin.Log.LogInfo($"[GpuSkin]   source {i} {(_smr[i] != null ? _smr[i].name : _mf[i].name)}: {n} vertices skinned, area {area:0.000} m2, world size {(mx - mn).ToString("0.00")}, " +
                                   $"bounds of the renderer {(_smr[i] != null ? _smr[i].bounds.size.ToString("0.00") : "-")}");
            }
            var picks = new int[WormSimConst.MaxAnchors];
            int count = GpuSkinAnchors.Select(all, all.Count, WormSimConst.MaxAnchors, picks, out _area);
            float thinned = _area;
            if (MeshArea > _area) _area = MeshArea;      // the surface the worms can cover is the real one (the thinning of the anchors counts only the area it samples)

            // the static data of the anchors: rest vertex, bones (renumbered among the bones the anchors use), weights, height over the target
            var set = new GpuSkinSet { Count = count, Area = _area };
            var pose = new Vector3[count];
            var map = new Dictionary<int, int>(128);
            _usedCount = 0;
            int dropped = 0;
            for (int a = 0; a < count; a++)
            {
                int ci = picks[a];
                int src = srcOf[ci];
                Vector3Int tv = triV[ci];
                Vector3 bc = triB[ci];
                Vector3 wp = all[ci];
                pose[a] = wp;
                float b2 = 1f - bc.x - bc.y;
                Vector3 rp = _rest[src][tv.x] * bc.x + _rest[src][tv.y] * bc.y + _rest[src][tv.z] * b2;
                Vector3 rn = _restN[src][tv.x] * bc.x + _restN[src][tv.y] * bc.y + _restN[src][tv.z] * b2;
                float rnl = rn.magnitude;
                rn = rnl > 1e-6f ? rn / rnl : _restN[src][tv.x];
                var sv = new GpuSkinVertex { PosRel = new Vector4(rp.x, rp.y, rp.z, Mathf.Clamp01((wp.y - foot) / height)), Normal = new Vector4(rn.x, rn.y, rn.z, 0f) };
                if (_mf[src] != null)
                {
                    sv.Bone0 = (uint)BoneSlot(map, src, 0);
                    sv.Weights = new Vector4(1f, 0f, 0f, 0f);
                }
                else
                {
                    // the influences of the three vertices of the triangle, mixed by the barycentric weights, the four strongest kept
                    int ni = 0;
                    AddInfluence(_infId, _infW, ref ni, _weights[src][tv.x], bc.x);
                    if (bc.y > 0f) AddInfluence(_infId, _infW, ref ni, _weights[src][tv.y], bc.y);
                    if (b2 > 1e-6f) AddInfluence(_infId, _infW, ref ni, _weights[src][tv.z], b2);
                    var ids = new int[4];
                    var ws = new float[4];
                    for (int k = 0; k < 4; k++)
                    {
                        int bi = -1;
                        for (int q = 0; q < ni; q++) if (_infW[q] > 0f && (bi < 0 || _infW[q] > _infW[bi])) bi = q;
                        if (bi < 0) break;
                        ids[k] = _infId[bi]; ws[k] = _infW[bi]; _infW[bi] = 0f;
                    }
                    var slots = new uint[4];
                    float sum = 0f;
                    for (int k = 0; k < 4; k++)
                    {
                        if (!(ws[k] > 0f)) { ws[k] = 0f; continue; }
                        int slot = BoneSlot(map, src, ids[k]);
                        if (slot < 0) { ws[k] = 0f; dropped++; continue; }
                        slots[k] = (uint)slot;
                        sum += ws[k];
                    }
                    if (sum <= 0f) { ws[0] = 1f; sum = 1f; slots[0] = 0u; }
                    sv.Bone0 = slots[0]; sv.Bone1 = slots[1]; sv.Bone2 = slots[2]; sv.Bone3 = slots[3];
                    sv.Weights = new Vector4(ws[0] / sum, ws[1] / sum, ws[2] / sum, ws[3] / sum);
                }
                set.Verts[a] = sv;
            }
            set.BoneCount = _usedCount;
            set.Finish(pose);
            _set = set;
            LastSelectMs = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            Description = $"{(_structure ? "structure mesh filters" : "skinned meshes (GPU skinning of the anchors)")}: {_sources} source(s), {all.Count} vertices, mesh area {MeshArea:0.00} m2, {count} anchors (thinning estimate {thinned:0.00} m2), surface used {_area:0.00} m2, " +
                          $"{_usedCount} bones, selected in {LastSelectMs:0.0} ms; {AnimatorInfo}";
            Plugin.Log.LogInfo($"[GpuSkin] target {_root.name}: {Description}{(dropped > 0 ? $"; WARNING {dropped} bone influences dropped (more than {WormSimConst.MaxBones} bones)" : string.Empty)}");
        }

        // The whole per frame job: the matrices of the bones of the anchors, relative to the origin of the mass.
        private bool Update(GpuMass m, float height)
        {
            if (_uploadedTo != m || m.SkinSet != _set)
            {
                GpuWormHost.UploadSkin(m, _set);
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
