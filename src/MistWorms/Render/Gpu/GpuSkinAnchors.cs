using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// Choice of the anchors on the surface of an engulfed body (pure maths, unit tested): at most <see cref="WormSimConst.MaxAnchors"/> vertices thinned to an even
    /// spacing (Poisson disk, deterministic: a fixed permutation of the vertices) and the surface they stand for. The choice is made once per target: the same vertices are
    /// followed from one pose to the next (skinned on the GPU), so the anchors follow the animation. The rest of the static data is built by <see cref="GpuSkinSet"/>.
    /// </summary>
    internal static class GpuSkinAnchors
    {
        /// <summary>Picks anchors among <paramref name="count"/> positions; returns how many (<paramref name="picks"/> holds the vertex indices) and the surface they stand for.</summary>
        public static int Select(IList<Vector3> positions, int count, int max, int[] picks, out float area)
        {
            area = 0f;
            if (count <= 0) return 0;
            if (count <= max)
            {
                for (int i = 0; i < count; i++) picks[i] = i;
                area = count * 1.44f * 0.03f * 0.03f;
                return count;
            }
            int stride = 7919;
            while (count % stride == 0) stride += 2;
            float s = 0.04f;
            int found = 0;
            var grid = new Dictionary<long, List<int>>();
            var accepted = new int[count];
            for (int attempt = 0; attempt < 10; attempt++)
            {
                grid.Clear();
                found = 0;
                float inv = 1f / s;
                for (int k = 0; k < count; k++)
                {
                    int v = (int)(((long)k * stride) % count);
                    Vector3 p = positions[v];
                    int cx = Mathf.FloorToInt(p.x * inv), cy = Mathf.FloorToInt(p.y * inv), cz = Mathf.FloorToInt(p.z * inv);
                    bool free = true;
                    for (int dx = -1; dx <= 1 && free; dx++)
                        for (int dy = -1; dy <= 1 && free; dy++)
                            for (int dz = -1; dz <= 1 && free; dz++)
                                if (grid.TryGetValue(Key(cx + dx, cy + dy, cz + dz), out List<int> cell))
                                    foreach (int o in cell)
                                        if ((positions[o] - p).sqrMagnitude < s * s) { free = false; break; }
                    if (!free) continue;
                    long key = Key(cx, cy, cz);
                    if (!grid.TryGetValue(key, out List<int> list)) grid[key] = list = new List<int>(2);
                    list.Add(v);
                    accepted[found++] = v;
                }
                if (found > max) s *= 1.2f;
                else if (found < max * 0.8f && s > 0.012f) s *= 0.85f;
                else break;
            }
            int n = Math.Min(found, max);
            for (int i = 0; i < n; i++) picks[i] = accepted[i];
            area = found * 1.44f * s * s;      // random sequential adsorption: a disc of diameter s takes 1.44 s2 of surface (all that were accepted count: the anchors are a part of them)
            return n;
        }

        private static long Key(int x, int y, int z) => ((long)(x + 4096) << 40) ^ ((long)(y + 4096) << 20) ^ (long)(z + 4096);


        /// <summary>The limbs of the synthetic body: 0 torso, 1 head, 2 and 3 thighs, 4 and 5 calves, 6 and 7 arms (the label of an anchor, also its bone).</summary>
        public static readonly string[] SyntheticLimbs = { "torso", "head", "left thigh", "right thigh", "left calf", "right calf", "left arm", "right arm" };

        private static readonly float[][] SyntheticSegments =
        {
            new[] { 0f, 0.95f, 0f, 0f, 1.45f, 0f, 0.17f }, new[] { 0f, 1.5f, 0f, 0f, 1.75f, 0f, 0.10f },
            new[] { -0.1f, 0.5f, 0f, -0.1f, 0.95f, 0f, 0.09f }, new[] { 0.1f, 0.5f, 0f, 0.1f, 0.95f, 0f, 0.09f },
            new[] { -0.1f, 0.05f, 0f, -0.1f, 0.5f, 0f, 0.07f }, new[] { 0.1f, 0.05f, 0f, 0.1f, 0.5f, 0f, 0.07f },
            new[] { -0.26f, 1.1f, 0f, -0.26f, 1.4f, 0f, 0.055f }, new[] { 0.26f, 1.1f, 0f, 0.26f, 1.4f, 0f, 0.055f },
        };

        /// <summary>
        /// A body made of eight capsules (torso, head, thighs, calves, arms; 1.75 m, feet at <paramref name="foot"/>) sampled as the surface of a mesh would be, as a skinned set: one
        /// bone per limb (identity matrices = the pose of the selection), weight 1. <paramref name="limbs"/> gets the limb of each anchor. For the tests and the editor check of the skin
        /// engulf, without a game.
        /// </summary>
        public static GpuSkinSet SyntheticSkin(Vector3 foot, float height, int[] limbs)
        {
            var pos = new List<Vector3>();
            var nrm = new List<Vector3>();
            var lab = new List<int>();
            for (int g = 0; g < SyntheticSegments.Length; g++)
            {
                float[] sgm = SyntheticSegments[g];
                Vector3 p0 = foot + new Vector3(sgm[0], sgm[1], sgm[2]), p1 = foot + new Vector3(sgm[3], sgm[4], sgm[5]);
                float r = sgm[6];
                float len = (p1 - p0).magnitude;
                Vector3 ax = (p1 - p0) / len;
                Vector3 e1 = Vector3.Cross(ax, Vector3.right).normalized, e2 = Vector3.Cross(ax, e1);
                int rings = Mathf.Max(2, Mathf.CeilToInt(len / 0.02f)), around = Mathf.Max(6, Mathf.CeilToInt(2f * Mathf.PI * r / 0.02f));
                for (int i = 0; i <= rings; i++)
                    for (int k = 0; k < around; k++)
                    {
                        float a = k / (float)around * Mathf.PI * 2f + (i & 1) * 0.5f * Mathf.PI * 2f / around;
                        Vector3 n = e1 * Mathf.Cos(a) + e2 * Mathf.Sin(a);
                        pos.Add(p0 + ax * (i / (float)rings * len) + n * r);
                        nrm.Add(n);
                        lab.Add(g);
                    }
            }
            var picks = new int[WormSimConst.MaxAnchors];
            int count = Select(pos, pos.Count, WormSimConst.MaxAnchors, picks, out float area);
            var set = new GpuSkinSet { Count = count, Area = area, BoneCount = SyntheticSegments.Length };
            var pose = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int v = picks[i];
                pose[i] = pos[v];
                set.Verts[i] = new GpuSkinVertex
                {
                    PosRel = new Vector4(pos[v].x, pos[v].y, pos[v].z, Mathf.Clamp01((pos[v].y - foot.y) / height)),
                    Normal = new Vector4(nrm[v].x, nrm[v].y, nrm[v].z, 0f),
                    Bone0 = (uint)lab[v], Weights = new Vector4(1f, 0f, 0f, 0f),
                };
                if (limbs != null) limbs[i] = lab[v];
            }
            set.Finish(pose);
            return set;
        }

        /// <summary>Identity matrices for the bones of the synthetic body (its pose at the selection).</summary>
        public static Matrix4x4[] RestBones() => Fill(new Matrix4x4[WormSimConst.MaxBones]);

        private static Matrix4x4[] Fill(Matrix4x4[] m)
        {
            for (int i = 0; i < m.Length; i++) m[i] = Matrix4x4.identity;
            return m;
        }
    }
}
