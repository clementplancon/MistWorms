using System;
using System.Collections.Generic;
using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// CPU reference of the worm rope simulation: an exact port of WormSim.compute (same formulas, same order, same constants as <see cref="WormSimConst"/>), for a
    /// reduced number of worms. It is the oracle of the unit tests and of the GPU parity scenario (the GPU result read back must match it within a millimetre).
    /// Same layout as the GPU buffers: worm w owns the nodes [w * 8, w * 8 + 8), and the collision points [w * 16, w * 16 + 15) of <see cref="Delta"/>.
    /// </summary>
    internal sealed class WormSimRef
    {
        private const int K = WormSimConst.Nodes;
        private const int SLOTS = WormSimConst.GridSlots;
        private const uint GridMask = WormSimConst.GridCells - 1;

        public readonly GpuWormData[] Worms;
        public readonly GpuMassParams[] Masses;
        public readonly float[] Height;                 // [mass * 144 + z * 12 + x]
        public readonly GpuPrim[] Prims;                // [mass * 24 + i]
        public readonly GpuAnchor[] Anchors;            // [mass * 512 + i], skinned by SkinAnchors from SkinVerts and Bones
        public readonly GpuSkinVertex[] SkinVerts;      // [mass * 512 + i]
        public readonly Matrix4x4[] Bones;              // [mass * 128 + b], relative to the origin of the mass
        public readonly uint[] Knn, Order, Hop;         // see GpuSkinSet
        public readonly Vector4[] SkinState;            // [worm]: x mode, y anchor of the path, z time in the mode
        public readonly uint[] NodeAnchor;              // [worm * 8 + node]
        public readonly float[] Sides;                  // [mass * 1280 + map * 256 + a * 16 + b], see SolidPush
        public readonly Vector4[] Pos, Prev, Pred;      // xyz, w = radius scale
        public readonly Vector4[] Delta;
        public uint StepIndex;
        public int WormCount;
        /// <summary>Tests: visits the worms in the opposite order when filling the grid (the GPU inserts in whatever order its threads run); the result must not change.</summary>
        public bool ReverseInsert;

        private readonly uint[] _grid = new uint[WormSimConst.GridCells * SLOTS];
        private readonly List<int> _touched = new List<int>();
        private readonly float[] _attached = new float[K];

        public WormSimRef(int maxWorms, int maxMasses = WormSimConst.MaxMasses)
        {
            Worms = new GpuWormData[maxWorms];
            Masses = new GpuMassParams[maxMasses];
            Height = new float[maxMasses * WormSimConst.HeightN * WormSimConst.HeightN];
            Prims = new GpuPrim[maxMasses * WormSimConst.MaxPrims];
            Anchors = new GpuAnchor[maxMasses * WormSimConst.MaxAnchors];
            Sides = new float[maxMasses * WormSimConst.SideMaps * WormSimConst.SideMapSize];
            Pos = new Vector4[maxWorms * K];
            Prev = new Vector4[maxWorms * K];
            Pred = new Vector4[maxWorms * K];
            Delta = new Vector4[maxWorms * WormSimConst.PointStride];
            SkinVerts = new GpuSkinVertex[maxMasses * WormSimConst.MaxAnchors];
            Bones = new Matrix4x4[maxMasses * WormSimConst.MaxBones];
            Knn = new uint[maxMasses * WormSimConst.MaxAnchors * WormSimConst.NeighbourCount];
            Order = new uint[maxMasses * WormSimConst.MaxAnchors];
            Hop = new uint[maxMasses * WormSimConst.MaxAnchors * WormSimConst.HopWords];
            SkinState = new Vector4[maxWorms];
            NodeAnchor = new uint[maxWorms * K];
            for (int i = 0; i < Bones.Length; i++) Bones[i] = Matrix4x4.identity;
            for (int i = 0; i < _grid.Length; i++) _grid[i] = WormSimConst.Empty;
        }

        // ---------------------------------------------------------------- helpers (mirrored in WormSim.compute)

        public static uint Hash(uint x)
        {
            unchecked
            {
                x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
                return x;
            }
        }

        /// <summary>A worm of a mass: its creep speed (0.1 to 0.3 m/s) and its phase come from the seed.</summary>
        public static GpuWormData MakeWorm(uint mass, uint seed) =>
            new GpuWormData { Mass = mass, Seed = seed, Speed = 0.1f + 0.2f * Hash01(seed, 6), Phase = Hash01(seed, 7) };

        public static float Hash01(uint seed, uint lane) => (Hash(unchecked(seed * 8u + lane)) & 0xFFFFFFu) * 5.9604645e-8f;

        private static float Cbrt01(float x)
        {
            float y = 0.5f + 0.5f * x;
            for (int i = 0; i < 3; i++) y = y - (y * y * y - x) / (3f * y * y);
            return y;
        }

        private static float L(float a, float b, float t) => a + t * (b - a);

        private static float Sat(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        private static float Smooth(float a, float b, float x)
        {
            float t = Sat((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static float Len(Vector3 v) => (float)Math.Sqrt(v.x * v.x + v.y * v.y + v.z * v.z);

        private static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;

        private static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);

        private static Vector3 Scale(Vector3 v, float s) => new Vector3(v.x * s, v.y * s, v.z * s);

        private static Vector3 Div(Vector3 v, float s) => new Vector3(v.x / s, v.y / s, v.z / s);

        private static Vector3 Norm(Vector3 v, Vector3 fallback)
        {
            float l2 = Dot(v, v);
            return l2 < 1e-12f ? fallback : Div(v, (float)Math.Sqrt(l2));
        }

        private static Vector3 Side(Vector3 fwd) => new Vector3(fwd.z, 0f, -fwd.x);

        private static Vector3 V3(Vector4 v) => new Vector3(v.x, v.y, v.z);

        public static uint HashCell(int cx, int cy, int cz, uint mass)
        {
            unchecked
            {
                uint h = ((uint)cx * 73856093u) ^ ((uint)cy * 19349663u) ^ ((uint)cz * 83492791u) ^ (mass * 2654435761u);
                h ^= h >> 15; h *= 0x2c1b3c6du; h ^= h >> 12;
                return h & GridMask;
            }
        }

        private static Vector3 Rot(Vector4 q, Vector3 v)
        {
            Vector3 qv = new Vector3(q.x, q.y, q.z);
            Vector3 t = Scale(Cross(qv, v), 2f);
            return v + Scale(t, q.w) + Cross(qv, t);
        }

        private static Vector3 RotInv(Vector4 q, Vector3 v) => Rot(new Vector4(-q.x, -q.y, -q.z, q.w), v);

        private float Ground(int mi, in GpuMassParams m, float x, float z)
        {
            int hn = WormSimConst.HeightN;
            float gx = Mathf.Clamp((x - m.GridRect.x) * m.GridRect.w, 0f, 14.999f);
            float gz = Mathf.Clamp((z - m.GridRect.y) * m.GridRect.w, 0f, 14.999f);
            int ix = (int)gx, iz = (int)gz;
            float fx = gx - ix, fz = gz - iz;
            int b = mi * (hn * hn) + iz * hn + ix;
            float a = L(Height[b], Height[b + 1], fx);
            float c = L(Height[b + hn], Height[b + hn + 1], fx);
            return L(a, c, fz);
        }

        /// <summary>Signed distance from p to the surface of a primitive (negative inside) and the outward normal.</summary>
        public static float PrimDist(in GpuPrim pr, Vector3 p, out Vector3 n)
        {
            int type = (int)(pr.D.x + 0.5f);
            n = new Vector3(0f, 1f, 0f);
            Vector3 a = V3(pr.A);
            if (type == 0)
            {
                Vector3 d = p - a;
                float l = Len(d);
                n = l > 1e-6f ? Div(d, l) : new Vector3(0f, 1f, 0f);
                return l - pr.A.w;
            }
            if (type == 1)
            {
                Vector3 ab = V3(pr.B) - a;
                float t = Sat(Dot(p - a, ab) / Math.Max(Dot(ab, ab), 1e-8f));
                Vector3 d = p - (a + Scale(ab, t));
                float l = Len(d);
                n = l > 1e-6f ? Div(d, l) : new Vector3(0f, 1f, 0f);
                return l - pr.A.w;
            }
            Vector3 lp = RotInv(pr.C, p - a);
            Vector3 half = V3(pr.B);
            Vector3 q = new Vector3(Math.Abs(lp.x) - half.x, Math.Abs(lp.y) - half.y, Math.Abs(lp.z) - half.z);
            Vector3 qo = new Vector3(Math.Max(q.x, 0f), Math.Max(q.y, 0f), Math.Max(q.z, 0f));
            float lo = Len(qo);
            Vector3 s = new Vector3(lp.x >= 0f ? 1f : -1f, lp.y >= 0f ? 1f : -1f, lp.z >= 0f ? 1f : -1f);
            Vector3 ln;
            float dist;
            if (lo > 1e-6f)
            {
                ln = Div(new Vector3(s.x * qo.x, s.y * qo.y, s.z * qo.z), lo);
                dist = lo;
            }
            else
            {
                dist = Math.Max(q.x, Math.Max(q.y, q.z));
                if (q.x > q.y && q.x > q.z) ln = new Vector3(s.x, 0f, 0f);
                else if (q.y > q.z) ln = new Vector3(0f, s.y, 0f);
                else ln = new Vector3(0f, 0f, s.z);
            }
            n = Rot(pr.C, ln);
            return dist;
        }

        private float ResolvePrims(int mi, in GpuMassParams m, ref Vector3 x)
        {
            float attached = 0f;
            for (int k = 0; k < (int)m.PrimCount; k++)
            {
                GpuPrim pr = Prims[mi * WormSimConst.MaxPrims + k];
                float d = PrimDist(pr, x, out Vector3 n);
                if (d < WormSimConst.Radius)
                {
                    x += Scale(n, WormSimConst.Radius - d);
                    d = WormSimConst.Radius;
                }
                float adh = pr.D.y;
                float fall = 1f - Sat((d - WormSimConst.Radius) / (WormSimConst.AdhesionRange - WormSimConst.Radius));
                if (adh > 0f && fall > 0f)
                {
                    x -= Scale(n, (d - WormSimConst.Radius) * adh * WormSimConst.AttractK * fall);
                    attached = Math.Max(attached, adh * fall);
                    if (pr.D.x > 0.5f && pr.D.x < 1.5f)
                    {
                        Vector3 a = V3(pr.A);
                        Vector3 ab = V3(pr.B) - a;
                        float len = Len(ab);
                        Vector3 ax = len > 1e-6f ? Div(ab, len) : new Vector3(0f, 1f, 0f);
                        float along = Dot(x - a, ax) + pr.A.w;
                        float top = pr.D.z * (len + 2f * pr.A.w);
                        if (along < top) x += Scale(ax, WormSimConst.Climb * adh * fall * WormSimConst.Dt);
                        else x -= Scale(ax, Math.Min(along - top, 0.01f) * fall);
                    }
                }
            }
            return attached;
        }

        private Vector2 GroundGrad(int mi, in GpuMassParams m, float x, float z)
        {
            float s = m.GridRect.z * 0.5f;
            return new Vector2((Ground(mi, m, x + s, z) - Ground(mi, m, x - s, z)) / (2f * s), (Ground(mi, m, x, z + s) - Ground(mi, m, x, z - s)) / (2f * s));
        }

        private float DomeThick(int mi, in GpuMassParams m, float x, float z, float qh)
        {
            Vector2 gr = GroundGrad(mi, m, x, z);
            float slope = (float)Math.Sqrt(gr.x * gr.x + gr.y * gr.y);
            return Math.Max(m.Shape.y * (float)Math.Sqrt(Math.Max(0f, 1f - qh * qh)) * Sat((1.5f - slope) / 0.9f), 0.03f);
        }

        /// <summary>Whether the worm is on the body; see SleeveParams in WormSim.compute.</summary>
        public bool SleeveParams(int mi, in GpuMassParams m, uint seed, float t, out GpuPrim pr, out float h, out float a, out float thick, out float phi, out float sgn)
        {
            uint kc = (uint)(m.Sleeve.x + 0.5f);
            float tot = 0f;
            for (uint c0 = 0; c0 < kc; c0++)
            {
                GpuPrim q = Prims[mi * WormSimConst.MaxPrims + (int)(m.Sleeve.z + 0.5f) + (int)c0];
                tot += (Len(V3(q.B) - V3(q.A)) + 2f * q.A.w) * q.A.w * (q.D.z + 0.02f);
            }
            float frac0 = Sat(tot * 907f / (Math.Max((float)m.WormCount, 1f) * 0.7f));
            bool member = Hash01(seed, 12) < frac0;
            float pick = Hash01(seed, 4) * tot;
            uint ci = 0;
            float acc = 0f;
            for (uint c1 = 0; c1 < kc; c1++)
            {
                GpuPrim q = Prims[mi * WormSimConst.MaxPrims + (int)(m.Sleeve.z + 0.5f) + (int)c1];
                acc += (Len(V3(q.B) - V3(q.A)) + 2f * q.A.w) * q.A.w * (q.D.z + 0.02f);
                ci = c1;
                if (pick < acc) break;
            }
            pr = Prims[mi * WormSimConst.MaxPrims + (int)(m.Sleeve.z + 0.5f) + (int)ci];
            float len = Len(V3(pr.B) - V3(pr.A));
            a = Hash01(seed, 0) * 6.2831853f;
            float fullTop = pr.D.z * (len + 2f * pr.A.w);
            float rag = 0.86f + 0.14f * (float)Math.Sin(a * 4f + t * 0.9f + fullTop * 3f);
            h = Hash01(seed, 1) * fullTop * rag;
            float nz = 0.5f + 0.2f * (float)Math.Sin(a * 2f + h * 4f + t * 0.4f) + 0.2f * (float)Math.Sin(a * 3f - h * 3f - t * 0.6f + 1.7f) + 0.15f * (float)Math.Sin(a * 5f + t * 0.25f + h * 1.5f);
            if (nz < 0.42f) member = false;
            if (Hash01(seed, 10) < 0.12f)
            {
                float cyc = t * 0.06f + Hash01(seed, 9);
                cyc -= (float)Math.Floor(cyc);
                h *= 1f - 0.9f * Smooth(0.9f, 0.94f, cyc) * (1f - Smooth(0.97f, 1f, cyc));
            }
            thick = 0.012f + m.Sleeve.y * (0.15f + 0.85f * Hash01(seed, 11)) * (0.6f + 0.8f * nz);
            phi = (0.05f + 0.95f * Hash01(seed, 13)) * 0.7f;
            sgn = Hash01(seed, 14) < 0.5f ? 1f : -1f;
            return member;
        }

        public static Vector3 SleeveNode(in GpuPrim pr, float h, float a, float thick, float phi, float sgn, float j)
        {
            Vector3 a0 = V3(pr.A);
            Vector3 ab = V3(pr.B) - a0;
            float len = Len(ab);
            Vector3 ax = len > 1e-6f ? Div(ab, len) : new Vector3(0f, 1f, 0f);
            Vector3 e1 = Norm(Cross(ax, new Vector3(1f, 0f, 0f)), new Vector3(0f, 0f, 1f));
            Vector3 e2 = Cross(ax, e1);
            float off = (j - 3.5f) * WormSimConst.SegLen;
            float hj = h + off * (float)Math.Cos(phi);
            float aj = a + sgn * off * (float)Math.Sin(phi) / (pr.A.w + thick);
            Vector3 rd = Scale(e1, (float)Math.Cos(aj)) + Scale(e2, (float)Math.Sin(aj));
            return a0 + Scale(ax, hj - pr.A.w) + Scale(rd, pr.A.w + thick);
        }

        // ---------------------------------------------------------------- skin engulf (exact port of WormSim.compute)

        /// <summary>Rank of a worm in its mass, in [0, V): a fixed permutation of the worms plus a fraction. The worms of rank below S are the sleeve; see <see cref="GpuSleeve"/>.</summary>
        public static float SkinRank(int w, in GpuMassParams m, uint seed)
        {
            uint v = Math.Max(m.WormCount, 1u);
            return (float)(((uint)w - m.FirstWorm) * 7919u % v) + Hash01(seed, 12);
        }

        /// <summary>Whether the worm may be on the body now, and its anchor (<paramref name="slotA"/>); see SkinEligible in WormSim.compute.</summary>
        public bool SkinEligible(int mi, in GpuMassParams m, uint seed, float t, float rk, uint mode, out uint slotA)
        {
            slotA = 0u;
            uint nc = (uint)(m.Plan.w + 0.5f);
            if (nc == 0u || rk >= m.Plan.x) return false;
            uint j = Math.Min((uint)(rk / m.Plan.y), nc - 1u);
            slotA = Order[mi * WormSimConst.MaxAnchors + (int)j];
            if (mode == 1u) return true;
            GpuSkinVertex sv = SkinVerts[mi * WormSimConst.MaxAnchors + (int)slotA];
            float rag = 0.9f + 0.1f * (float)Math.Sin(sv.Body.x * 17f + sv.Body.z * 13f + t * 0.9f);
            if (sv.PosRel.w > m.Plan.z * rag) return false;
            float nz = 0.5f + 0.22f * (float)Math.Sin(sv.Body.x * 23f + sv.Body.y * 9f + t * 0.2f) + 0.22f * (float)Math.Sin(sv.Body.z * 27f - sv.Body.y * 11f - t * 0.28f + 1.3f);
            return nz >= (mode == 2u ? 0.14f : 0.20f);
        }

        /// <summary>A tenth of the worms let go from time to time and fall.</summary>
        public static bool SkinDrops(uint seed, float t)
        {
            if (Hash01(seed, 10) >= 0.12f) return false;
            float cyc = t * 0.04f + Hash01(seed, 9);
            cyc -= (float)Math.Floor(cyc);
            return cyc >= 0.95f;
        }

        /// <summary>Distance of the nodes of a worm from the skin: SkinOffset, and one layer higher for half of the worms (two layers of worms).</summary>
        public static float SkinOff(uint seed) => Hash01(seed, 33) < 0.5f ? WormSimConst.SkinOffset + WormSimConst.LayerGap : WormSimConst.SkinOffset;

        /// <summary>The anchor nearest to x among anchor cur and its 8 neighbours.</summary>
        public uint NearestAnchor(int mi, uint cur, Vector3 x)
        {
            int basei = mi * WormSimConst.MaxAnchors;
            Vector3 d = x - V3(Anchors[basei + (int)cur].A);
            float best = Dot(d, d);
            uint bi = cur;
            for (int e = 0; e < WormSimConst.NeighbourCount; e++)
            {
                uint nb = Knn[(basei + (int)cur) * WormSimConst.NeighbourCount + e];
                Vector3 d2 = x - V3(Anchors[basei + (int)nb].A);
                float l2 = Dot(d2, d2);
                if (l2 < best) { best = l2; bi = nb; }
            }
            return bi;
        }

        private uint HopOf(int mi, uint target, uint src)
        {
            uint word = Hop[mi * WormSimConst.MaxAnchors * WormSimConst.HopWords + (int)target * WormSimConst.HopWords + (int)(src >> 2)];
            return (word >> (int)((src & 3u) * 8u)) & 255u;
        }

        // Where a worm enters the body: the anchor of the base (the lowest ones) nearest to its head.
        private uint EntryAnchor(int mi, in GpuMassParams m, Vector3 from)
        {
            uint nb = Math.Max((uint)(m.Plan2.x + 0.5f), 1u);
            uint best = Order[mi * WormSimConst.MaxAnchors];
            Vector3 d0 = from - V3(Anchors[mi * WormSimConst.MaxAnchors + (int)best].A);
            float bd = Dot(d0, d0);
            for (uint i = 1u; i < nb; i++)
            {
                uint a = Order[mi * WormSimConst.MaxAnchors + (int)i];
                Vector3 d = from - V3(Anchors[mi * WormSimConst.MaxAnchors + (int)a].A);
                float l2 = Dot(d, d);
                if (l2 < bd) { bd = l2; best = a; }
            }
            return best;
        }

        /// <summary>Direction of a worm settled on the surface at a point of normal n: up the surface turned by up to 52 degrees either side, or down for one that hangs.</summary>
        public static Vector3 SkinDir(Vector3 n, uint seed, bool pend)
        {
            Vector3 u0 = new Vector3(0f, 1f, 0f) - Scale(n, n.y);
            float l2 = Dot(u0, u0);
            if (l2 > 0.04f) u0 = Div(u0, (float)Math.Sqrt(l2));
            else
            {
                float ha = Hash01(seed, 3) * 6.2831853f;
                Vector3 alt = new Vector3((float)Math.Cos(ha), 0f, (float)Math.Sin(ha));
                alt = alt - Scale(n, Dot(alt, n));
                u0 = Norm(alt, Cross(n, new Vector3(1f, 0f, 0f)));
            }
            float phi = (Hash01(seed, 13) * 2f - 1f) * 0.9f;
            Vector3 d = Scale(u0, (float)Math.Cos(phi)) + Scale(Cross(n, u0), (float)Math.Sin(phi));
            return pend ? new Vector3(-d.x, -d.y, -d.z) : d;
        }

        /// <summary>A node on the plane of its nearest anchor (within ProjectRadius): at SkinOffset from the skin (attract), or only pushed out when it is behind the surface.</summary>
        public Vector3 SkinSurface(int mi, uint cur, Vector3 x, bool attract, float off, ref float att)
        {
            uint bi = NearestAnchor(mi, cur, x);
            GpuAnchor an = Anchors[mi * WormSimConst.MaxAnchors + (int)bi];
            Vector3 d = x - V3(an.A);
            if (Dot(d, d) >= WormSimConst.ProjectRadius * WormSimConst.ProjectRadius) return x;
            float dn = Dot(d, V3(an.B));
            if (attract) { x += Scale(V3(an.B), off - dn); att = 1f; }
            else if (dn < WormSimConst.Radius + 0.003f) x += Scale(V3(an.B), WormSimConst.Radius + 0.003f - dn);
            return x;
        }

        /// <summary>Loads the static data of a skin set for a mass (what <c>GpuWormHost.UploadSkin</c> uploads).</summary>
        public void SetSkin(int mi, GpuSkinSet set)
        {
            int a = WormSimConst.MaxAnchors;
            Array.Copy(set.Verts, 0, SkinVerts, mi * a, set.Count);
            Array.Copy(set.Knn, 0, Knn, mi * a * WormSimConst.NeighbourCount, set.Count * WormSimConst.NeighbourCount);
            Array.Copy(set.Order, 0, Order, mi * a, set.Count);
            Array.Copy(set.Hop, 0, Hop, mi * a * WormSimConst.HopWords, set.Count * WormSimConst.HopWords);
        }

        /// <summary>KSkin: skins the anchors of mass <paramref name="mi"/> from <see cref="Bones"/> (the matrices of its bones, relative to its origin).</summary>
        public void SkinAnchors(int mi)
        {
            int n = (int)(Masses[mi].Skin.x + 0.5f);
            for (int i = 0; i < n; i++)
            {
                GpuSkinning.Skin(SkinVerts[mi * WormSimConst.MaxAnchors + i], Bones, mi * WormSimConst.MaxBones, out Vector3 p, out Vector3 nr);
                Anchors[mi * WormSimConst.MaxAnchors + i] = new GpuAnchor { A = new Vector4(p.x, p.y, p.z, 0f), B = new Vector4(nr.x, nr.y, nr.z, SkinVerts[mi * WormSimConst.MaxAnchors + i].PosRel.w) };
            }
        }

        /// <summary>Port of SolidPush in WormSim.compute: a node inside the visual hull of the obstacles leaves by the axis of the smallest penetration.</summary>
        public Vector3 SolidPush(int mi, in GpuMassParams m, Vector3 x, bool glide = true)
        {
            if (m.Side.w < 0.5f) return x;
            float fx = (x.x - m.GridRect.x) * m.GridRect.w;
            float fz = (x.z - m.GridRect.y) * m.GridRect.w;
            float fy = (x.y - m.Side.x) * m.Side.z;
            if (fx < 0f || fz < 0f || fy < 0f || fx > 15f || fz > 15f || fy > 15f) return x;
            int ix = (int)Math.Min(fx, 14f), iz = (int)Math.Min(fz, 14f), iy = (int)Math.Min(fy, 14f);
            int b = mi * WormSimConst.SideMaps * WormSimConst.SideMapSize;
            float wmin = 1e6f, emax = -1e6f, smin = 1e6f, nmax = -1e6f, tmax = -1e6f;
            for (int dy = 0; dy < 2; dy++)
                for (int du = 0; du < 2; du++)
                {
                    wmin = Math.Min(wmin, Sides[b + (iy + dy) * 16 + iz + du]);
                    emax = Math.Max(emax, Sides[b + 256 + (iy + dy) * 16 + iz + du]);
                    smin = Math.Min(smin, Sides[b + 512 + (iy + dy) * 16 + ix + du]);
                    nmax = Math.Max(nmax, Sides[b + 768 + (iy + dy) * 16 + ix + du]);
                    tmax = Math.Max(tmax, Sides[b + 1024 + (iz + dy) * 16 + ix + du]);
                }
            if (!(x.x > wmin && x.x < emax && x.z > smin && x.z < nmax && x.y < tmax)) return x;
            float best = tmax - x.y;
            int dir = 4;
            float pw = x.x - wmin, pe = emax - x.x, ps = x.z - smin, pn = nmax - x.z;
            if (pw < best) { best = pw; dir = 0; }
            if (pe < best) { best = pe; dir = 1; }
            if (ps < best) { best = ps; dir = 2; }
            if (pn < best) { best = pn; dir = 3; }
            float slide = glide ? (best + WormSimConst.Radius) * WormSimConst.SlideGain : 0f;
            if (dir == 0 || dir == 1)
            {
                float hz = m.Heading.z, mid = 0.5f * (smin + nmax);
                x.z += slide * (Math.Abs(hz) > 0.3f ? (hz > 0f ? 1f : -1f) : (x.z >= mid ? 1f : -1f));
            }
            else if (dir == 2 || dir == 3)
            {
                float hx = m.Heading.x, mid = 0.5f * (wmin + emax);
                x.x += slide * (Math.Abs(hx) > 0.3f ? (hx > 0f ? 1f : -1f) : (x.x >= mid ? 1f : -1f));
            }
            if (dir == 0) x.x = wmin - WormSimConst.Radius;
            else if (dir == 1) x.x = emax + WormSimConst.Radius;
            else if (dir == 2) x.z = smin - WormSimConst.Radius;
            else if (dir == 3) x.z = nmax + WormSimConst.Radius;
            else x.y = tmax + WormSimConst.Radius;
            return x;
        }

        /// <summary>A node behind the surface of the nearest anchor is pushed out along its normal (the worms do not go through the body).</summary>
        public static Vector3 SkinPush(GpuAnchor[] anchors, int mi, in GpuMassParams m, Vector3 x)
        {
            Vector3 dc = x - V3(m.Center);
            if (dc.x * dc.x + dc.z * dc.z > 1f || x.y < m.Center.y - 0.3f || x.y > m.Center.y + m.Skin.z + 0.4f) return x;
            uint n = (uint)(m.Skin.x + 0.5f);
            int basei = mi * WormSimConst.MaxAnchors;
            float best = 1e9f;
            int bi = 0;
            for (int i = 0; i < n; i++)
            {
                Vector3 d = x - V3(anchors[basei + i].A);
                float l2 = Dot(d, d);
                if (l2 < best) { best = l2; bi = i; }
            }
            if (best > 0.0081f) return x;
            GpuAnchor an = anchors[basei + bi];
            float dn = Dot(x - V3(an.A), V3(an.B));
            if (dn < WormSimConst.Radius + 0.003f) x += Scale(V3(an.B), WormSimConst.Radius + 0.003f - dn);
            return x;
        }

        // ---------------------------------------------------------------- pseudopod (exact port of WormSim.compute; the shape is Arm() of MassLayout.cs)

        /// <summary>
        /// The <see cref="GpuMassParams.Pseudo"/> of a mass: (direction x, direction z, length of the net past its start, ramp). <paramref name="dirX"/>, <paramref name="dirZ"/> is the horizontal unit
        /// direction to the target in the local frame, <paramref name="targetDist"/> the horizontal distance to it, <paramref name="radius"/> the radius of the mass. Zero (no net) when the ramp is 0,
        /// when the target is on top of the mass or when it is too close for a net (the same length as Arm(): ArmMax or what is left before the target, less the margin, times the ramp).
        /// </summary>
        public static Vector4 PseudoParams(float dirX, float dirZ, float targetDist, float ramp, float radius)
        {
            if (!(ramp > 0f) || !(targetDist > 0f)) return Vector4.zero;
            float lim = targetDist - WormSimConst.ArmMargin - WormSimConst.ArmStart * radius;
            float ln = WormSimConst.ArmMax < lim ? WormSimConst.ArmMax : lim;
            ln = (ln > 0f ? ln : 0f) * Sat(ramp);
            if (!(ln > 0f)) return Vector4.zero;
            return new Vector4(dirX, dirZ, ln, Sat(ramp));
        }

        /// <summary>The ramp of the net given to the GPU: follows the ramp of the visual when it grows, falls at most <see cref="WormSimConst.ArmRetractRate"/> per second (the worms come back to the dome as the net shortens).</summary>
        public static float SmoothRamp(float current, float want, float dt)
        {
            if (want >= current) return want;
            float fall = current - WormSimConst.ArmRetractRate * dt;
            return fall > want ? fall : want;
        }

        /// <summary>Whether the worm is of the net toward the target.</summary>
        public static bool ArmIs(in GpuMassParams m, uint seed) =>
            m.Pseudo.z > 0f && m.Pseudo.w > 0f && m.Sleeve.x < 0.5f && Hash01(seed, 36) < WormSimConst.ArmFrac * m.Pseudo.w - 0.5f * WormSimConst.ArmSoft;

        public static bool ArmTip(uint seed) => Hash01(seed, 36) < WormSimConst.ArmTipFrac;

        /// <summary>Place of the worm on the conveyor of the net: 0 at the root, 1 at the tip (a worm that rears up stays at the tip).</summary>
        public static float ArmS(uint seed, float t)
        {
            if (ArmTip(seed)) return 1f;
            float c = Hash01(seed, 37) + t * WormSimConst.ArmFlow;
            return c - (float)Math.Floor(c);
        }

        /// <summary>Size of the worm at place s: grows from nothing at the root and shrinks to nothing at the tip (a rearing worm keeps its size).</summary>
        public static float ArmFadeOf(float s, bool tip) => tip ? 1f : Smooth(0f, WormSimConst.ArmFade, s) * (1f - Smooth(1f - WormSimConst.ArmFade, 1f, s));

        /// <summary>
        /// Place of the worm in the net at time t: <paramref name="hp"/> the centre of its line, <paramref name="f"/> the direction of its line (a rearing worm: hp is its root and f points up), its size.
        /// </summary>
        public void ArmFrame(int mi, in GpuMassParams m, uint seed, float t, out Vector3 hp, out Vector3 f, out float fade, out bool tip)
        {
            tip = ArmTip(seed);
            float s = ArmS(seed, t);
            float ln = m.Pseudo.z;
            float dx = m.Pseudo.x, dz = m.Pseudo.y;
            float px = -dz, pz = dx;
            float waveA = 6.2831853f * (s * WormSimConst.ArmWaveK - t / WormSimConst.ArmWavePer);
            float sw = (float)Math.Sin(waveA);
            float wave = WormSimConst.ArmWave * s * sw;
            float wid = L(WormSimConst.ArmWidth, WormSimConst.ArmWidth * WormSimConst.ArmTaper, s);
            float lat = (Hash01(seed, 38) - 0.5f) * wid;
            float dist = WormSimConst.ArmStart * m.Center.w + s * ln;
            float x = m.Center.x + dx * dist + px * (wave + lat);
            float z = m.Center.z + dz * dist + pz * (wave + lat);
            hp = new Vector3(x, Ground(mi, m, x, z) + WormSimConst.Radius + WormSimConst.ArmThick * Hash01(seed, 39) * (1f - 0.4f * s), z);
            float dw = WormSimConst.ArmWave * (sw + s * WormSimConst.ArmWaveK * 6.2831853f * (float)Math.Cos(waveA)) / Math.Max(ln, 0.2f);
            f = Norm(new Vector3(dx + px * dw, 0f, dz + pz * dw), new Vector3(dx, 0f, dz));
            if (tip) f = Scale(f, (float)Math.Cos(WormSimConst.ArmTipPitch)) + Scale(new Vector3(0f, 1f, 0f), (float)Math.Sin(WormSimConst.ArmTipPitch));
            fade = ArmFadeOf(s, tip);
        }

        public Vector3 HomePoint(int mi, in GpuMassParams m, uint seed, float t)
        {
            if (m.Sleeve.x > 0.5f && m.Skin.x < 0.5f)
            {
                if (SleeveParams(mi, m, seed, t, out GpuPrim spr, out float sh, out float sa, out float sth, out float sphi, out float ssg)) return SleeveNode(spr, sh, sa, sth, sphi, ssg, 3.5f);
            }
            float a2 = Hash01(seed, 0) * 6.2831853f;
            float cz = Hash01(seed, 1);
            float rr = Cbrt01(Hash01(seed, 2)) * 0.92f;
            float sn = (float)Math.Sqrt(Math.Max(0f, 1f - cz * cz));
            Vector3 u = Scale(new Vector3(sn * (float)Math.Cos(a2), cz, sn * (float)Math.Sin(a2)), rr);
            Vector3 fwd = V3(m.Heading);
            Vector3 pos = V3(m.Center) + Scale(fwd, m.Shape.w + u.x * m.Shape.x) + Scale(Side(fwd), u.z * m.Shape.z);
            float qh = (float)Math.Sqrt(u.x * u.x + u.z * u.z);
            pos.y = Ground(mi, m, pos.x, pos.z) + WormSimConst.Radius + u.y * DomeThick(mi, m, pos.x, pos.z, qh);
            return pos;
        }

        private Vector3 ContainCorr(int mi, in GpuMassParams m, Vector3 x)
        {
            Vector3 fwd = V3(m.Heading);
            Vector3 side = Side(fwd);
            Vector3 d = x - V3(m.Center);
            float qx = (Dot(d, fwd) - m.Shape.w) / m.Shape.x, qz = Dot(d, side) / m.Shape.z;
            float qh = (float)Math.Sqrt(qx * qx + qz * qz);
            Vector3 dc = Vector3.zero;
            if (qh > 1f)
            {
                float sx = qx / qh, sz = qz / qh;
                Vector3 target = V3(m.Center) + Scale(fwd, sx * m.Shape.x + m.Shape.w) + Scale(side, sz * m.Shape.z);
                dc.x = target.x - x.x;
                dc.z = target.z - x.z;
            }
            float ceilY = Ground(mi, m, x.x, x.z) + WormSimConst.Radius + DomeThick(mi, m, x.x, x.z, qh);
            if (x.y > ceilY) dc.y = ceilY - x.y;
            if (m.Sleeve.x > 0.5f)
            {
                int kc = (int)(m.Sleeve.x + 0.5f);
                Vector3 sc = Vector3.zero;
                float best = 1e30f;
                for (int ci = 0; ci < kc; ci++)
                {
                    GpuPrim pr = Prims[mi * WormSimConst.MaxPrims + (int)(m.Sleeve.z + 0.5f) + ci];
                    Vector3 a0 = V3(pr.A);
                    Vector3 ab = V3(pr.B) - a0;
                    float len = Len(ab);
                    Vector3 ax = len > 1e-6f ? Div(ab, len) : new Vector3(0f, 1f, 0f);
                    float t = Mathf.Clamp(Dot(x - a0, ax), 0f, len);
                    Vector3 rv = x - (a0 + Scale(ax, t));
                    float r = Len(rv);
                    float maxr = pr.A.w + 0.024f + m.Sleeve.y * 1.4f;
                    Vector3 c = Vector3.zero;
                    if (r > maxr) c -= Scale(Div(rv, r), r - maxr);
                    float along = Dot(x - a0, ax) + pr.A.w;
                    float top = pr.D.z * (len + 2f * pr.A.w) + 0.12f;
                    if (along > top) c -= Scale(ax, along - top);
                    float cm = Dot(c, c);
                    if (cm < best) { best = cm; sc = c; }
                }
                float md = Len(dc), ms = Len(sc);
                float wb = Sat(0.5f + (md - ms) * 10f);
                dc = dc + Scale(sc - dc, wb);
            }
            return dc;
        }

        private void InsertPoint(uint id, Vector3 pos, uint mass)
        {
            int cell = (int)HashCell(Mathf.FloorToInt(pos.x * WormSimConst.CellInv), Mathf.FloorToInt(pos.y * WormSimConst.CellInv), Mathf.FloorToInt(pos.z * WormSimConst.CellInv), mass);
            uint v = id;
            _touched.Add(cell);
            for (int s = 0; s < SLOTS; s++)
            {
                uint old = _grid[cell * SLOTS + s];
                if (v < old) _grid[cell * SLOTS + s] = v;      // InterlockedMin
                if (old == WormSimConst.Empty) break;
                if (old > v) v = old;
            }
        }

        // ---------------------------------------------------------------- kernels

        /// <summary>KInit: puts the worms [first, first + count) straight at their home point, at rest.</summary>
        public void Init(int first, int count, float scale = 1f)
        {
            for (int w = first; w < first + count; w++)
            {
                GpuWormData wd = Worms[w];
                GpuMassParams m = Masses[wd.Mass];
                Vector3 hp = HomePoint((int)wd.Mass, m, wd.Seed, (float)StepIndex * WormSimConst.Dt);
                float ha = Hash01(wd.Seed, 3) * 6.2831853f;
                Vector3 dir = new Vector3((float)Math.Cos(ha), 0f, (float)Math.Sin(ha));
                float g = Ground((int)wd.Mass, m, hp.x, hp.z) + WormSimConst.Radius;
                hp.y = Math.Max(hp.y, g);
                for (int i = 0; i < K; i++)
                {
                    Vector3 p = hp + Scale(dir, (i - 3.5f) * WormSimConst.SegLen);
                    p.y = Math.Max(p.y, Ground((int)wd.Mass, m, p.x, p.z) + WormSimConst.Radius);
                    Pos[w * K + i] = new Vector4(p.x, p.y, p.z, scale);
                    Prev[w * K + i] = Pos[w * K + i];
                    Pred[w * K + i] = Pos[w * K + i];
                    NodeAnchor[w * K + i] = 0u;
                }
                SkinState[w] = Vector4.zero;
            }
            if (first + count > WormCount) WormCount = first + count;
        }

        /// <summary>Moves the local frame of the worms [first, first + count) by <paramref name="shift"/> (KShift).</summary>
        public void Shift(int first, int count, Vector3 shift)
        {
            for (int w = first; w < first + count; w++)
                for (int i = 0; i < K; i++)
                {
                    Vector4 a = Pos[w * K + i], b = Prev[w * K + i];
                    Pos[w * K + i] = new Vector4(a.x + shift.x, a.y + shift.y, a.z + shift.z, a.w);
                    Prev[w * K + i] = new Vector4(b.x + shift.x, b.y + shift.y, b.z + shift.z, b.w);
                }
        }

        /// <summary>One step of 1/60 s: KClear, KPredict, KSeparate, KSolve.</summary>
        public void Step()
        {
            for (int i = 0; i < _touched.Count; i++)
                for (int s = 0; s < SLOTS; s++) _grid[_touched[i] * SLOTS + s] = WormSimConst.Empty;
            _touched.Clear();

            if (ReverseInsert) for (int w = WormCount - 1; w >= 0; w--) Predict(w);
            else for (int w = 0; w < WormCount; w++) Predict(w);
            for (int w = 0; w < WormCount; w++)
                for (int k = 0; k < WormSimConst.Points; k++) Separate(w, k);
            for (int w = 0; w < WormCount; w++) Solve(w);
            StepIndex++;
        }

        public void Step(int count)
        {
            for (int i = 0; i < count; i++) Step();
        }

        private void Predict(int w)
        {
            GpuWormData wd = Worms[w];
            GpuMassParams m = Masses[wd.Mass];
            if (m.Origin.w < 0.5f) return;

            const float dt = WormSimConst.Dt;
            var p = new Vector3[K];
            var pr = new Vector3[K];
            Vector3 cen = Vector3.zero;
            for (int i = 0; i < K; i++)
            {
                p[i] = V3(Pos[w * K + i]);
                pr[i] = V3(Prev[w * K + i]);
                cen += p[i];
            }
            cen = Scale(cen, 0.125f);

            float t = (float)StepIndex * dt;
            GpuPrim spr = default;
            float sh = 0f, sa = 0f, sth = 0f, sphi = 0f, ssg = 1f;
            bool member = false;
            if (m.Sleeve.x > 0.5f && m.Skin.x < 0.5f) member = SleeveParams((int)wd.Mass, m, wd.Seed, t, out spr, out sh, out sa, out sth, out sphi, out ssg);
            Vector3 pull = Scale(HomePoint((int)wd.Mass, m, wd.Seed, t) - cen, m.Motion.z);
            float pl2 = Dot(pull, pull);
            if (pl2 > WormSimConst.HomeMax * WormSimConst.HomeMax) pull = Scale(Div(pull, (float)Math.Sqrt(pl2)), WormSimConst.HomeMax);
            pull.y *= member ? 1f : 0.6f;

            Vector3 fwd = V3(m.Heading);
            float freq = 1f + 0.5f * Hash01(wd.Seed, 5);
            float cyc = t * freq + wd.Phase;
            cyc -= (float)Math.Floor(cyc);

            // Skin engulf. Modes: 0 pile (the dome), 1 transit (the head follows a path of anchors from the base of the body to the anchor of the worm, the rope trails behind), 2 settled
            // (leashed to its anchor, lying on the surface up or diagonally, or hanging by one end), 3 falling (real gravity, nothing pulls it). kx is the kinematic pull of the nodes.
            uint mode = 0u;
            var kx = new Vector3[K];
            float creepScale = 1f;
            if (m.Skin.x > 0.5f)
            {
                int mi = (int)wd.Mass;
                int basei = mi * WormSimConst.MaxAnchors;
                Vector4 st = SkinState[w];
                mode = (uint)(st.x + 0.5f);
                uint pathA = (uint)(st.y + 0.5f);
                float timer = st.z;
                float off = SkinOff(wd.Seed);
                float rk = SkinRank(w, m, wd.Seed);
                bool elig = SkinEligible(mi, m, wd.Seed, t, rk, mode, out uint slotA);
                bool entered = false;
                if (mode == 0u)
                {
                    if (elig)
                    {
                        mode = 1u;
                        timer = 0f;
                        pathA = EntryAnchor(mi, m, p[7]);
                        entered = true;
                    }
                }
                else if (mode == 1u)
                {
                    if (!elig) { mode = 3u; timer = 0f; }
                    else
                    {
                        // the head is at the anchor of the path (or has been trying for HopTime: a body in the way): on to the next anchor, or settled at the last one
                        timer += dt;
                        GpuAnchor pa = Anchors[basei + (int)pathA];
                        Vector3 dh = V3(pa.A) + Scale(V3(pa.B), off) - p[7];
                        if (Dot(dh, dh) < WormSimConst.AdvanceRadius * WormSimConst.AdvanceRadius || timer > WormSimConst.HopTime)
                        {
                            timer = 0f;
                            if (pathA == slotA) mode = 2u;
                            else
                            {
                                uint hb = HopOf(mi, slotA, pathA);
                                if (hb < WormSimConst.NeighbourCount) pathA = Knn[(basei + (int)pathA) * WormSimConst.NeighbourCount + (int)hb];
                                else mode = 2u;
                            }
                        }
                    }
                }
                else if (mode == 2u)
                {
                    if (!elig || SkinDrops(wd.Seed, t)) { mode = 3u; timer = 0f; }
                }
                else
                {
                    timer += dt;
                    if (timer > WormSimConst.FallTime || cen.y < Ground(mi, m, cen.x, cen.z) + 0.15f) mode = 0u;
                }
                SkinState[w] = new Vector4((float)mode, (float)pathA, timer, 0f);

                if (mode == 1u || mode == 2u)
                {
                    // the anchor of each node follows the node over the surface (twice: the body can move by more than one neighbour in a step)
                    var nodeA = new uint[K];
                    for (int j0 = 0; j0 < K; j0++)
                    {
                        uint a0 = entered ? pathA : NodeAnchor[w * K + j0];
                        a0 = NearestAnchor(mi, a0, p[j0]);
                        a0 = NearestAnchor(mi, a0, p[j0]);
                        NodeAnchor[w * K + j0] = a0;
                        nodeA[j0] = a0;
                    }
                    if (mode == 1u)
                    {
                        GpuAnchor pa = Anchors[basei + (int)pathA];
                        Vector3 tgt = V3(pa.A) + Scale(V3(pa.B), off);
                        // on the surface the pull does not go into the body (it would only fight the projection): the head slides along it, or leaves it toward a neighbouring surface (an arm)
                        Vector3 hd = tgt - p[7];
                        GpuAnchor ha = Anchors[basei + (int)nodeA[7]];
                        Vector3 hrel = p[7] - V3(ha.A);
                        if (Dot(hrel, hrel) < WormSimConst.ProjectRadius * WormSimConst.ProjectRadius) hd -= Scale(V3(ha.B), Math.Min(Dot(hd, V3(ha.B)), 0f));
                        Vector3 hv = Scale(hd, WormSimConst.TransitK);
                        float cap = (1.8f + 0.8f * Hash01(wd.Seed, 32)) * dt;
                        float hl2 = Dot(hv, hv);
                        if (hl2 > cap * cap) hv = Scale(Div(hv, (float)Math.Sqrt(hl2)), cap);
                        for (int j1 = 0; j1 < K; j1++)
                            kx[j1] = Scale(hv, 0.85f + 0.15f * ((float)j1 / 7f));
                    }
                    else
                    {
                        bool pend = Hash01(wd.Seed, 19) < WormSimConst.PendantShare;
                        GpuAnchor ca = Anchors[basei + (int)nodeA[3]];
                        Vector3 d = SkinDir(V3(ca.B), wd.Seed, pend);
                        GpuAnchor sn = Anchors[basei + (int)slotA];
                        float tw = 4f + 6f * Hash01(wd.Seed, 17);
                        float drift = pend ? 0f : 0.05f * (float)Math.Sin(6.2831853f * (t / tw + Hash01(wd.Seed, 18)));
                        Vector3 ps = V3(sn.A) + Scale(V3(sn.B), off) + Scale(d, drift);
                        if (pend)
                        {
                            // hangs by its first node, the others are free
                            Vector3 pc0 = Scale(ps - p[0], 0.3f);
                            float pl0 = Dot(pc0, pc0);
                            if (pl0 > WormSimConst.BodyPullMax * WormSimConst.BodyPullMax) pc0 = Scale(Div(pc0, (float)Math.Sqrt(pl0)), WormSimConst.BodyPullMax);
                            kx[0] = pc0;
                            creepScale = 0f;
                        }
                        else
                        {
                            Vector3 pc = Scale(ps - cen, WormSimConst.LeashK);
                            float pcl = Dot(pc, pc);
                            if (pcl > WormSimConst.BodyPullMax * WormSimConst.BodyPullMax) pc = Scale(Div(pc, (float)Math.Sqrt(pcl)), WormSimConst.BodyPullMax);
                            Vector3 sd = Cross(V3(ca.B), d);
                            for (int j2 = 0; j2 < K; j2++)
                            {
                                Vector3 tj = cen + Scale(d, ((float)j2 - 3.5f) * WormSimConst.SegLen) + Scale(sd, 0.006f * (float)Math.Sin(6.2831853f * (cyc - (float)j2 * 0.11f)));
                                kx[j2] = pc + Scale(tj - p[j2], WormSimConst.OrientK);
                            }
                        }
                    }
                }
            }
            bool onBody = mode == 1u || mode == 2u;
            bool dropping = m.Skin.x > 0.5f && mode == 3u;

            // Pseudopod: the nodes are pulled toward their place in the net (kinematic): a share of the gap per step, at most ArmPullMax; a worm that is invisible (fade near 0: the wrap
            // of the conveyor, a worm just recruited at the root) goes straight to its place.
            bool arm = mode == 0u && ArmIs(m, wd.Seed);
            if (arm)
            {
                ArmFrame((int)wd.Mass, m, wd.Seed, t, out Vector3 ahp, out Vector3 af, out float afade, out bool atip);
                for (int ja = 0; ja < K; ja++)
                {
                    Vector3 kd = ahp + Scale(af, (ja - (atip ? 0f : 3.5f)) * WormSimConst.SegLen) - p[ja];
                    if (afade >= WormSimConst.ArmSnap)
                    {
                        kd = Scale(kd, WormSimConst.ArmK);
                        float kl2 = Dot(kd, kd);
                        if (kl2 > WormSimConst.ArmPullMax * WormSimConst.ArmPullMax) kd = Scale(Div(kd, (float)Math.Sqrt(kl2)), WormSimConst.ArmPullMax);
                    }
                    kx[ja] = kd;
                }
            }

            var pn = new Vector3[K];
            for (int j = 0; j < K; j++)
            {
                Vector3 v = Scale(p[j] - pr[j], WormSimConst.Damp);
                float gh = Ground((int)wd.Mass, m, p[j].x, p[j].z);
                Vector2 gr = GroundGrad((int)wd.Mass, m, p[j].x, p[j].z);
                float tg = Sat(1f - (p[j].y - gh) / 0.02f);
                float sk = 2f * WormSimConst.Gravity * dt * dt * tg;
                Vector3 a = new Vector3(-gr.x * sk, -WormSimConst.Gravity * dt * dt, -gr.y * sk);
                Vector3 pj = pull;
                if (onBody)
                {
                    pj = Vector3.zero;
                }
                else if (dropping)
                {
                    pj = Vector3.zero;
                    a.y = -9.8f * dt * dt;
                }
                else if (arm)
                {
                    pj = Vector3.zero;                  // the net is pulled by kx
                }
                else if (member)
                {
                    pj = Scale(SleeveNode(spr, sh, sa, sth, sphi, ssg, j) - p[j], m.Motion.z * 8f);
                    float jl2 = Dot(pj, pj);
                    if (jl2 > WormSimConst.HomeMax * WormSimConst.HomeMax) pj = Scale(Div(pj, (float)Math.Sqrt(jl2)), WormSimConst.HomeMax);
                }
                float hf = Sat((p[j].y - gh) / Math.Max(m.Shape.y, 0.05f));
                Vector3 conv = Scale(fwd, m.Motion.x * m.Heading.w * dt * Smooth(0.25f, 0.9f, hf));
                Vector3 axis = Norm(p[Math.Min(j + 1, K - 1)] - p[Math.Max(j - 1, 0)], fwd);
                float wave = 0.5f + 0.5f * (float)Math.Sin(6.2831853f * (cyc - j * 0.11f));
                Vector3 creep = Scale(axis, wd.Speed * m.Motion.y * dt * wave * creepScale);
                pn[j] = p[j] + v + a + pj + conv + creep + kx[j];
                Pred[w * K + j] = new Vector4(pn[j].x, pn[j].y, pn[j].z, 1f);
                // The creep, the conveyor and the pulls of the sleeve are kinematic: they move the node without giving it momentum (a tangential push that fed the Verlet
                // velocity would be a follower force, and the rope would flutter). The previous position is no longer needed: Solve reads the displacement here.
                Vector3 kin = creep + conv + kx[j];
                Prev[w * K + j] = new Vector4(kin.x, kin.y, kin.z, Prev[w * K + j].w);
            }

            for (int k = 0; k < K; k++) InsertPoint((uint)(w * 16 + k), pn[k], wd.Mass);
            for (int s = 0; s < K - 1; s++) InsertPoint((uint)(w * 16 + 8 + s), Scale(pn[s] + pn[s + 1], 0.5f), wd.Mass);
        }


        private Vector3 PredPoint(int w, int k)
        {
            if (k < 8) return V3(Pred[w * K + k]);
            return Scale(V3(Pred[w * K + k - 8]) + V3(Pred[w * K + k - 7]), 0.5f);
        }

        private void Separate(int w, int k)
        {
            GpuWormData wd = Worms[w];
            if (Masses[wd.Mass].Origin.w < 0.5f) return;

            // a worm on its way up the body (mode 1) goes through the others, and the others through it
            bool skinMass = Masses[wd.Mass].Skin.x > 0.5f;
            bool inTransit = skinMass && (uint)(SkinState[w].x + 0.5f) == 1u;
            uint myId = (uint)(w * 16 + k);
            Vector3 pa = PredPoint(w, k);
            int cx = Mathf.FloorToInt(pa.x * WormSimConst.CellInv), cy = Mathf.FloorToInt(pa.y * WormSimConst.CellInv), cz = Mathf.FloorToInt(pa.z * WormSimConst.CellInv);
            Vector3 sum = Vector3.zero;
            float count = 0f;
            for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int cell = (int)HashCell(cx + dx, cy + dy, cz + dz, wd.Mass);
                        for (int s = 0; s < SLOTS; s++)
                        {
                            uint nb = _grid[cell * SLOTS + s];
                            if (nb == WormSimConst.Empty) break;
                            int nw = (int)(nb >> 4);
                            if (nw == w || Worms[nw].Mass != wd.Mass) continue;
                            if (skinMass && (inTransit || (uint)(SkinState[nw].x + 0.5f) == 1u)) continue;
                            Vector3 d = pa - PredPoint(nw, (int)(nb & 15u));
                            float l2 = Dot(d, d);
                            if (l2 >= WormSimConst.SepDist * WormSimConst.SepDist) continue;
                            float l = (float)Math.Sqrt(l2);
                            Vector3 dir = l > 1e-6f ? Div(d, l) : new Vector3(0f, myId > nb ? 1f : -1f, 0f);
                            sum += Scale(dir, (WormSimConst.SepDist - l) * 0.5f * WormSimConst.SepK);
                            count += 1f;
                        }
                    }
            float s2 = Dot(sum, sum);
            if (s2 > WormSimConst.MaxSep * WormSimConst.MaxSep) sum = Scale(Div(sum, (float)Math.Sqrt(s2)), WormSimConst.MaxSep);
            Delta[myId] = new Vector4(sum.x, sum.y, sum.z, count);
        }

        private static void Lengths(Vector3[] x)
        {
            for (int s = 0; s < K - 1; s++) LengthPair(x, s);
            for (int s = K - 2; s >= 0; s--) LengthPair(x, s);
        }

        private static void LengthPair(Vector3[] x, int s)
        {
            Vector3 d = x[s + 1] - x[s];
            float l = Len(d);
            if (l > 1e-6f)
            {
                Vector3 c = Scale(d, (l - WormSimConst.SegLen) / l * 0.5f);
                x[s] += c;
                x[s + 1] -= c;
            }
        }

        private void Solve(int w)
        {
            GpuWormData wd = Worms[w];
            int mi = (int)wd.Mass;
            GpuMassParams m = Masses[mi];
            if (m.Origin.w < 0.5f) return;

            var x = new Vector3[K];
            var o = new Vector3[K];
            var rs = new float[K];
            for (int i = 0; i < K; i++)
            {
                x[i] = V3(Pred[w * K + i]);
                o[i] = V3(Pos[w * K + i]) + V3(Prev[w * K + i]);       // the old position moved by the kinematic displacement of the step
                rs[i] = Pos[w * K + i].w;
            }

            for (int a = 0; a < K; a++)
            {
                Vector3 dn = V3(Delta[w * 16 + a]);
                float ws = 1f;
                if (a > 0) { dn += V3(Delta[w * 16 + 8 + a - 1]); ws += 1f; }
                if (a < K - 1) { dn += V3(Delta[w * 16 + 8 + a]); ws += 1f; }
                x[a] += Div(dn, ws);
            }

            // skin engulf: a worm on the body (transit, settled) is projected on the surface and not contained in the dome; a falling one is not contained either
            uint sMode = 0u;
            if (m.Skin.x > 0.5f) sMode = (uint)(SkinState[w].x + 0.5f);
            bool onBody = sMode == 1u || sMode == 2u;
            bool pend = sMode == 2u && Hash01(wd.Seed, 19) < WormSimConst.PendantShare;
            float off = SkinOff(wd.Seed);
            float tNow = (float)StepIndex * WormSimConst.Dt;
            bool arm = sMode == 0u && ArmIs(m, wd.Seed);       // a worm of the net is not contained in the dome
            // A worm that has just left the net (the ramp falls) is far outside the dome: the containment would throw it back in one step. While a net is on it shrinks to nothing in
            // ArmLeaveTime instead, where it is, and once it is gone the containment brings it back (it grows again in the dome).
            bool leaving = false;
            if (sMode == 0u && !arm && m.Pseudo.w > 0f && rs[3] > WormSimConst.ArmGone)
            {
                Vector3 lc = Vector3.zero;
                for (int lk = 0; lk < K; lk++) lc += x[lk];
                Vector3 ld = Scale(lc, 0.125f) - V3(m.Center);
                Vector3 lf = V3(m.Heading);
                float lqx = (Dot(ld, lf) - m.Shape.w) / m.Shape.x, lqz = Dot(ld, Side(lf)) / m.Shape.z;
                leaving = lqx * lqx + lqz * lqz > WormSimConst.ArmOut * WormSimConst.ArmOut;
            }
            bool skipContain = sMode != 0u || arm || leaving;
            var nodeA = new uint[K];
            for (int na = 0; na < K; na++) nodeA[na] = onBody ? NodeAnchor[w * K + na] : 0u;
            for (int i = 0; i < K; i++) _attached[i] = 0f;

            for (int it = 0; it < WormSimConst.Iterations; it++)
            {
                Lengths(x);
                for (int b = 1; b < K - 1; b++)
                {
                    Vector3 d = x[b + 1] - x[b - 1];
                    float l = Len(d);
                    if (l > 1e-6f && l < WormSimConst.BendMin)
                    {
                        Vector3 c = Scale(Div(d, l), (WormSimConst.BendMin - l) * WormSimConst.BendK);
                        x[b - 1] -= Scale(c, 0.5f);
                        x[b + 1] += Scale(c, 0.5f);
                    }
                }
                for (int n = 0; n < K; n++)
                {
                    if (!skipContain) x[n] += Scale(ContainCorr(mi, m, x[n]), WormSimConst.ContainK);
                    float att = 0f;
                    if (onBody) x[n] = SkinSurface(mi, nodeA[n], x[n], !pend || n == 0, off, ref att);
                    else if (m.Skin.x > 0.5f && it == WormSimConst.Iterations - 1) x[n] = SkinPush(Anchors, mi, m, x[n]);
                    _attached[n] = Math.Max(ResolvePrims(mi, m, ref x[n]), att);
                    x[n] = SolidPush(mi, m, x[n]);
                    float g = Ground(mi, m, x[n].x, x[n].z) + WormSimConst.Radius;
                    if (x[n].y < g) x[n].y = g;
                }
            }
            Lengths(x);
            for (int e = 0; e < K; e++)
            {
                if (onBody)
                {
                    float att2 = 0f;
                    x[e] = SkinSurface(mi, nodeA[e], x[e], !pend || e == 0, off, ref att2);
                }
                x[e] = SolidPush(mi, m, x[e], false);
                float g = Ground(mi, m, x[e].x, x[e].z) + WormSimConst.Radius;
                if (x[e].y < g) x[e].y = g;
                Vector3 vel = x[e] - o[e];
                float tg = Sat(1f - (x[e].y - g) / 0.004f);
                vel.x *= 1f - m.Sleeve.w * tg;
                vel.z *= 1f - m.Sleeve.w * tg;
                vel = Scale(vel, 1f + (WormSimConst.BodyDamp - 1f) * _attached[e]);
                float rsn = rs[e];
                if (m.Plan2.y > 0.5f) rsn = (uint)(w - (int)m.FirstWorm) < (uint)(m.Plan2.y + 0.5f) ? Math.Min(1f, rsn + WormSimConst.Dt / WormSimConst.BirthTime) : Math.Max(0f, rsn - WormSimConst.Dt / WormSimConst.BirthTime);
                if (m.Plan2.y <= 0.5f && rsn < 1f) rsn = Math.Min(1f, rsn + WormSimConst.Dt / WormSimConst.BirthTime);     // a worm that left the net grows back
                if (arm) rsn = Math.Min(rsn, ArmFadeOf(ArmS(wd.Seed, tNow), ArmTip(wd.Seed)));
                if (leaving) rsn = Math.Max(0f, Math.Min(rsn, rs[e] - WormSimConst.Dt / WormSimConst.ArmLeaveTime));
                Pos[w * K + e] = new Vector4(x[e].x, x[e].y, x[e].z, rsn);
                Prev[w * K + e] = new Vector4(x[e].x - vel.x, x[e].y - vel.y, x[e].z - vel.z, rsn);
            }
        }
    }
}
