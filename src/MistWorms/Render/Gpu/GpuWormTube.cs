using UnityEngine;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// The straight tube of a GPU worm (the vertex shader of WormRope.shader bends it along the spline of the rope): 0.30 m along +z from 0 (tail) to 0.30 (head),
    /// body radius 0.009 (diameter 1.8 cm), head bulb 0.0105 (2.1 cm), fuselage taper at both ends. Three levels of detail: 8 sides by 20 rings, 6 by 10, 4 by 6.
    /// UVs follow the atlas of make_worm.py (worm_albedo.png): U around the body (0.25 = back), V along the body, in the convention of the glb the game imports
    /// (V flipped by Unity): the head, which is at the bottom of the PNG, has v = 0 here, the tail v = 1.
    /// </summary>
    internal static class GpuWormTube
    {
        public const int Lods = 3;
        public static readonly int[] Sides = { 8, 6, 4 };
        public static readonly int[] Rings = { 20, 10, 6 };
        public const float Length = 0.30f, Radius = 0.009f, HeadRadius = 0.0105f;
        private const float TailTaper = 0.12f, HeadBulbStart = 0.70f, HeadBulbPeak = 0.88f;      // as fractions of the length

        /// <summary>Radius profile over t (0 tail, 1 head), relative to <see cref="Radius"/>: quarter ellipse at the tail, the full radius, a swelling to the head bulb, a quarter ellipse at the head.</summary>
        public static float Profile(float t)
        {
            float bulb = HeadRadius / Radius;
            if (t < TailTaper) { float k = (TailTaper - t) / TailTaper; return Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
            if (t < HeadBulbStart) return 1f;
            if (t < HeadBulbPeak) { float k = (t - HeadBulbStart) / (HeadBulbPeak - HeadBulbStart); k = k * k * (3f - 2f * k); return 1f + (bulb - 1f) * k; }
            { float k = (t - HeadBulbPeak) / (1f - HeadBulbPeak); return bulb * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
        }

        public static int Triangles(int lod) => (Rings[lod] - 1) * Sides[lod] * 2;

        public static Mesh Build(int lod)
        {
            int sides = Sides[lod], rings = Rings[lod];
            int stride = sides + 1;                       // the seam vertex is duplicated for the u coordinate
            int vertexCount = stride * rings;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var uvs = new Vector2[vertexCount];
            for (int ring = 0; ring < rings; ring++)
            {
                float t = ring / (float)(rings - 1);
                float r = Radius * Profile(t);
                // Slope of the profile along z, for the normal (clamped near the ends where it goes vertical).
                float t0 = Mathf.Max(0f, t - 0.01f), t1 = Mathf.Min(1f, t + 0.01f);
                float dr = Radius * (Profile(t1) - Profile(t0)) / (t1 - t0) / Length;
                for (int s = 0; s < stride; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    int i = ring * stride + s;
                    vertices[i] = new Vector3(c * r, sn * r, t * Length);
                    normals[i] = new Vector3(c, sn, -dr).normalized;
                    tangents[i] = new Vector4(-sn, c, 0f, 1f);
                    uvs[i] = new Vector2(s / (float)sides, 1f - t);
                }
            }

            var indices = new int[(rings - 1) * sides * 6];
            int n = 0;
            for (int ring = 0; ring < rings - 1; ring++)
            {
                for (int s = 0; s < sides; s++)
                {
                    int a = ring * stride + s, b = a + 1, c = a + stride, d = c + 1;
                    indices[n++] = a; indices[n++] = b; indices[n++] = c;
                    indices[n++] = b; indices[n++] = d; indices[n++] = c;
                }
            }

            var mesh = new Mesh { name = "MW_WormTube_LOD" + lod, vertices = vertices, normals = normals, tangents = tangents, uv = uvs, triangles = indices };
            mesh.bounds = new Bounds(new Vector3(0f, 0f, Length * 0.5f), new Vector3(Length, Length * 0.6f, Length * 1.1f));
            mesh.UploadMeshData(false);
            return mesh;
        }
    }
}
