using MistWorms.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistWorms.Assets
{
    /// <summary>GlbMeshData (glTF, right-handed) to UnityEngine.Mesh (left-handed), spec §13.2.</summary>
    internal static class GltfLoader
    {
        /// <summary>Unity-space arrays of one mesh; built on any thread, turned into a Mesh on the main thread.</summary>
        public sealed class MeshArrays
        {
            public Vector3[] Pos, Nrm;
            public Vector2[] Uv;
            public int[] Tris;
        }

        public static Mesh ToMesh(GlbMeshData d, string name) => Build(Convert(d), name);

        /// <summary>Handedness and UV conversion. No Unity API call besides plain structs: safe on worker threads.</summary>
        public static MeshArrays Convert(GlbMeshData d)
        {
            int vc = d.Positions.Length / 3;
            var pos = new Vector3[vc];
            var nrm = new Vector3[vc];
            var uv = new Vector2[vc];
            for (int i = 0; i < vc; i++)
            {
                // x -> -x mirrors the handedness; the winding reversal below keeps faces pointing outwards.
                pos[i] = new Vector3(-d.Positions[i * 3], d.Positions[i * 3 + 1], d.Positions[i * 3 + 2]);
                nrm[i] = new Vector3(-d.Normals[i * 3], d.Normals[i * 3 + 1], d.Normals[i * 3 + 2]);
                // glTF has v = 0 at the top of the image, Unity at the bottom.
                uv[i] = new Vector2(d.Uvs[i * 2], 1f - d.Uvs[i * 2 + 1]);
            }

            int[] src = d.Indices;
            var tris = new int[src.Length];
            for (int i = 0; i < src.Length; i += 3)
            {
                tris[i] = src[i];
                tris[i + 1] = src[i + 2];
                tris[i + 2] = src[i + 1];
            }

            return new MeshArrays { Pos = pos, Nrm = nrm, Uv = uv, Tris = tris };
        }

        /// <summary>Main thread only.</summary>
        public static Mesh Build(MeshArrays a, string name)
        {
            var mesh = new Mesh { name = name };
            if (a.Pos.Length > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(a.Pos);
            mesh.SetNormals(a.Nrm);
            mesh.SetUVs(0, a.Uv);
            mesh.SetTriangles(a.Tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents(); // normal maps need tangents and glTF exports none
            // Keep the CPU copy only when frames are blended on the CPU (Performance.FlipbookBlend).
            mesh.UploadMeshData(!MWConfig.Performance.FlipbookBlend.Value);
            return mesh;
        }
    }
}
