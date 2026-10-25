using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>What the sleeve of a mass has to be this frame (see <see cref="GpuSleeve.Plan"/>).</summary>
    internal struct GpuSleevePlan
    {
        public float Count;          // S: worms of the sleeve
        public float PerAnchor;      // worms per anchor (the density of the layer)
        public float TopRel;         // height (0 foot, 1 top) of the highest covered anchor
        public int Eligible;         // anchors under the height engulf x h
        public int Cover;            // anchors the worms of the sleeve can fill (bottom up)
        public int Base;             // anchors of the base of the body (where a worm enters it)
    }

    /// <summary>
    /// How many worms wrap a body, and where. The target area is that of the anchors that are eligible (under the height engulf x h of the body); the layer is
    /// <see cref="Layers"/> worms thick, and a worm covers <see cref="FootprintArea"/> (30 cm x 1.8 cm). So S = min(0.6 V, layers x area / footprint) worms. The coverage fills from the
    /// bottom up: the worm of rank k (a fixed permutation of the worms) sits at the anchor of rank k / (worms per anchor) among the anchors by height, so when the worms run short
    /// (50 units) the covered height is lower than engulf x h.
    /// </summary>
    internal static class GpuSleeve
    {
        public const float Layers = 2f;
        public const float FootprintArea = 0.30f * 0.018f;
        public const float MaxShare = 0.6f;
        public const float BaseHeight = 0.1f;          // anchors under this height (rel) are the base of the body

        public static GpuSleevePlan Plan(int worms, int anchors, float area, float[] sortedRel, int baseCount, float engulf)
        {
            var p = new GpuSleevePlan();
            if (anchors <= 0 || worms <= 0 || area <= 0f) return p;
            float perAnchor = area / anchors;                                   // surface one anchor stands for
            p.PerAnchor = Layers * perAnchor / FootprintArea;
            int lo = 0, hi = anchors;                                            // first anchor above engulf
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (sortedRel[mid] <= engulf) lo = mid + 1; else hi = mid;
            }
            p.Eligible = lo;
            p.Count = Math.Min(MaxShare * worms, Layers * p.Eligible * perAnchor / FootprintArea);
            p.Cover = Math.Min(p.Eligible, (int)Math.Ceiling(p.Count / p.PerAnchor - 1e-4f));
            p.TopRel = p.Cover > 0 ? sortedRel[p.Cover - 1] : 0f;
            p.Base = baseCount;
            return p;
        }

        /// <summary>Rank of the worm <paramref name="index"/> (0 based, in its mass) among <paramref name="worms"/>: the permutation of WormSim.compute (without the fraction).</summary>
        public static int Rank(int index, int worms) => (int)((uint)index * 7919u % (uint)Math.Max(worms, 1));
    }

    /// <summary>
    /// The static part of the skin engulf of a target, built once at the selection of the anchors (pure maths, unit tested): the anchor vertices with their bones and weights, the 8
    /// nearest anchors of each (k-NN), the anchors by height, the paths over the surface (shortest path in the graph of the neighbours, from any anchor to any other), and the
    /// position of each anchor on the body. The per frame part is the matrices of the bones; the anchors are skinned on the GPU.
    /// </summary>
    internal sealed class GpuSkinSet
    {
        public int Count;                  // anchors
        public int BoneCount;              // bones the anchors are skinned with
        public int BaseCount;              // anchors of the base of the body
        public float Area;                 // surface the anchors stand for (m2)
        public readonly GpuSkinVertex[] Verts = new GpuSkinVertex[WormSimConst.MaxAnchors];
        public readonly uint[] Knn = new uint[WormSimConst.MaxAnchors * WormSimConst.NeighbourCount];
        public readonly uint[] Order = new uint[WormSimConst.MaxAnchors];
        public readonly float[] SortedRel = new float[WormSimConst.MaxAnchors];
        public readonly uint[] Hop = new uint[WormSimConst.MaxAnchors * WormSimConst.HopWords];

        /// <summary>
        /// Completes the set once <see cref="Verts"/> hold the rest positions, normals, bones, weights and heights (PosRel.w) of the first <see cref="Count"/> anchors:
        /// <paramref name="pose"/> are their world positions at the selection (the geometry of the neighbours and of the body coordinates).
        /// </summary>
        public void Finish(IList<Vector3> pose)
        {
            int n = Count, kn = WormSimConst.NeighbourCount;
            float cx = 0f, cz = 0f, minY = float.MaxValue;
            for (int i = 0; i < n; i++) { cx += pose[i].x; cz += pose[i].z; minY = Math.Min(minY, pose[i].y); }
            cx /= n; cz /= n;
            for (int i = 0; i < n; i++) Verts[i].Body = new Vector4(pose[i].x - cx, pose[i].y - minY, pose[i].z - cz, 0f);

            // the 8 nearest anchors, nearest first (the anchor itself when there are fewer)
            var bestD = new float[kn];
            var bestI = new int[kn];
            for (int i = 0; i < n; i++)
            {
                int found = 0;
                for (int j = 0; j < n; j++)
                {
                    if (j == i) continue;
                    float d = (pose[j] - pose[i]).sqrMagnitude;
                    if (found == kn && d >= bestD[kn - 1]) continue;
                    int at = found < kn ? found++ : kn - 1;
                    while (at > 0 && bestD[at - 1] > d) { bestD[at] = bestD[at - 1]; bestI[at] = bestI[at - 1]; at--; }
                    bestD[at] = d; bestI[at] = j;
                }
                for (int e = 0; e < kn; e++) Knn[i * kn + e] = (uint)(e < found ? bestI[e] : i);
            }

            // the anchors by height (ties by index), and the base of the body
            var ord = new int[n];
            for (int i = 0; i < n; i++) ord[i] = i;
            Array.Sort(ord, (a, b) =>
            {
                int c = Verts[a].PosRel.w.CompareTo(Verts[b].PosRel.w);
                return c != 0 ? c : a.CompareTo(b);
            });
            BaseCount = 0;
            for (int j = 0; j < n; j++)
            {
                Order[j] = (uint)ord[j];
                SortedRel[j] = Verts[ord[j]].PosRel.w;
                if (SortedRel[j] <= GpuSleeve.BaseHeight) BaseCount++;
            }
            BaseCount = Math.Max(BaseCount, Math.Min(n, 4));

            // every anchor must be reachable from the base along the neighbours (an arm that hangs along the flank may have no neighbour on the body among its 8 nearest):
            // an anchor that is not gets an edge from the nearest anchor that is (it replaces the farthest of its 8 neighbours)
            {
                var reach = new bool[n];
                var rq = new int[n];
                int rh = 0, rt = 0;
                for (int j = 0; j < BaseCount; j++) { int a = (int)Order[j]; reach[a] = true; rq[rt++] = a; }
                while (true)
                {
                    while (rh < rt)
                    {
                        int v = rq[rh++];
                        for (int e = 0; e < kn; e++) { int u = (int)Knn[v * kn + e]; if (!reach[u]) { reach[u] = true; rq[rt++] = u; } }
                    }
                    int miss = -1;
                    for (int i = 0; i < n && miss < 0; i++) if (!reach[i]) miss = i;
                    if (miss < 0) break;
                    int bestV = -1;
                    float bd = float.MaxValue;
                    for (int v = 0; v < n; v++)
                    {
                        if (!reach[v]) continue;
                        float d = (pose[v] - pose[miss]).sqrMagnitude;
                        if (d < bd) { bd = d; bestV = v; }
                    }
                    Knn[bestV * kn + kn - 1] = (uint)miss;
                    reach[miss] = true;
                    rq[rt++] = miss;
                }
            }

            // the paths: for each target a breadth first search from the target over the reversed graph of the neighbours gives each anchor the neighbour that leads to it
            for (int i = 0; i < Hop.Length; i++) Hop[i] = 0xFFFFFFFFu;
            var rev = new List<int>[n];
            for (int i = 0; i < n; i++) rev[i] = new List<int>(kn);
            for (int u = 0; u < n; u++)
                for (int e = 0; e < kn; e++)
                {
                    int v = (int)Knn[u * kn + e];
                    if (v != u) rev[v].Add(u * kn + e);
                }
            var dist = new int[n];
            var queue = new int[n];
            for (int t = 0; t < n; t++)
            {
                for (int i = 0; i < n; i++) dist[i] = -1;
                dist[t] = 0;
                int head = 0, tail = 0;
                queue[tail++] = t;
                while (head < tail)
                {
                    int v = queue[head++];
                    List<int> edges = rev[v];
                    for (int k = 0; k < edges.Count; k++)
                    {
                        int u = edges[k] / kn, e = edges[k] % kn;
                        if (dist[u] >= 0) continue;
                        dist[u] = dist[v] + 1;
                        int word = t * WormSimConst.HopWords + (u >> 2), shift = (u & 3) * 8;
                        Hop[word] = (Hop[word] & ~(0xFFu << shift)) | ((uint)e << shift);
                        queue[tail++] = u;
                    }
                }
            }
        }

        /// <summary>The neighbour of anchor <paramref name="src"/> on the shortest path to <paramref name="target"/> (index 0..7 among its neighbours), or 255 when there is none (or src is the target).</summary>
        public uint HopOf(int target, int src) => (Hop[target * WormSimConst.HopWords + (src >> 2)] >> ((src & 3) * 8)) & 255u;

        public GpuSleevePlan Plan(int worms, float engulf) => GpuSleeve.Plan(worms, Count, Area, SortedRel, BaseCount, engulf);
    }

    /// <summary>The skinning of the anchors, as KSkin does it (exact port: the oracle of the unit tests and of the parity check in the editor).</summary>
    internal static class GpuSkinning
    {
        /// <summary>Position and normal of a vertex: sum of weight x matrix x vertex over its bones. <paramref name="bones"/> are the matrices of the mass (relative to its origin).</summary>
        public static void Skin(in GpuSkinVertex v, Matrix4x4[] bones, int boneBase, out Vector3 pos, out Vector3 nrm)
        {
            float px = 0f, py = 0f, pz = 0f, nx = 0f, ny = 0f, nz = 0f;
            for (int k = 0; k < 4; k++)
            {
                float w = k == 0 ? v.Weights.x : k == 1 ? v.Weights.y : k == 2 ? v.Weights.z : v.Weights.w;
                if (!(w > 0f)) continue;
                uint bi = k == 0 ? v.Bone0 : k == 1 ? v.Bone1 : k == 2 ? v.Bone2 : v.Bone3;
                Matrix4x4 m = bones[boneBase + (int)bi];
                px += (m.m00 * v.PosRel.x + m.m01 * v.PosRel.y + m.m02 * v.PosRel.z + m.m03) * w;
                py += (m.m10 * v.PosRel.x + m.m11 * v.PosRel.y + m.m12 * v.PosRel.z + m.m13) * w;
                pz += (m.m20 * v.PosRel.x + m.m21 * v.PosRel.y + m.m22 * v.PosRel.z + m.m23) * w;
                nx += (m.m00 * v.Normal.x + m.m01 * v.Normal.y + m.m02 * v.Normal.z) * w;
                ny += (m.m10 * v.Normal.x + m.m11 * v.Normal.y + m.m12 * v.Normal.z) * w;
                nz += (m.m20 * v.Normal.x + m.m21 * v.Normal.y + m.m22 * v.Normal.z) * w;
            }
            pos = new Vector3(px, py, pz);
            float l2 = nx * nx + ny * ny + nz * nz;
            if (l2 < 1e-20f) nrm = new Vector3(0f, 1f, 0f);
            else { float l = (float)Math.Sqrt(l2); nrm = new Vector3(nx / l, ny / l, nz / l); }
        }

        /// <summary>The anchors of a set for the given matrices (what KSkin writes), local frame of the mass.</summary>
        public static void SkinAll(GpuSkinSet set, Matrix4x4[] bones, GpuAnchor[] dst, int offset)
        {
            for (int i = 0; i < set.Count; i++)
            {
                Skin(set.Verts[i], bones, 0, out Vector3 p, out Vector3 n);
                dst[offset + i] = new GpuAnchor { A = new Vector4(p.x, p.y, p.z, 0f), B = new Vector4(n.x, n.y, n.z, set.Verts[i].PosRel.w) };
            }
        }
    }
}
