using System;
using System.Collections.Generic;
using System.Text;

namespace MistWorms.Assets
{
    public sealed class GlbFormatException : Exception
    {
        public GlbFormatException(string message) : base(message) { }
    }

    /// <summary>Raw glTF geometry, still in glTF space (right-handed). The handedness flip happens in GltfLoader.</summary>
    public sealed class GlbMeshData
    {
        public float[] Positions; // xyz
        public float[] Normals;   // xyz
        public float[] Uvs;       // uv
        public int[] Indices;
    }

    /// <summary>
    /// Binary glTF 2.0 reader (spec §13.3). No UnityEngine dependency so it can be unit-tested outside the game.
    /// Accepts exactly one mesh with one triangle primitive; everything the mod does not use is refused loudly.
    /// </summary>
    public static class GlbParser
    {
        private const uint Magic = 0x46546C67;      // 'glTF'
        private const uint ChunkJson = 0x4E4F534A;  // 'JSON'
        private const uint ChunkBin = 0x004E4942;   // 'BIN\0'

        private const int UByte = 5121, UShort = 5123, UInt = 5125, Float = 5126;

        /// <param name="name">File name used in error messages.</param>
        public static GlbMeshData Parse(byte[] glb, string name = "glb")
        {
            if (glb == null) throw new GlbFormatException(name + ": no data");
            if (glb.Length < 20) throw new GlbFormatException(name + ": file too short for a GLB header");
            if (ReadU32(glb, 0) != Magic) throw new GlbFormatException(name + ": bad magic, not a binary glTF");
            uint version = ReadU32(glb, 4);
            if (version != 2) throw new GlbFormatException(name + ": unsupported glTF version " + version + " (need 2)");
            long total = ReadU32(glb, 8);
            if (total > glb.Length) throw new GlbFormatException(name + ": header length " + total + " exceeds file size " + glb.Length + " (truncated)");

            string jsonText = null;
            int binOffset = -1, binLength = 0;
            long pos = 12;
            while (pos + 8 <= total)
            {
                long len = ReadU32(glb, (int)pos);
                uint type = ReadU32(glb, (int)pos + 4);
                long start = pos + 8;
                if (start + len > total) throw new GlbFormatException(name + ": chunk overruns the file (truncated)");
                if (type == ChunkJson && jsonText == null)
                    jsonText = Encoding.UTF8.GetString(glb, (int)start, (int)len);
                else if (type == ChunkBin && binOffset < 0)
                {
                    binOffset = (int)start;
                    binLength = (int)len;
                }
                pos = start + ((len + 3) & ~3L); // chunks are 4-byte aligned
            }
            if (jsonText == null) throw new GlbFormatException(name + ": missing JSON chunk");
            if (binOffset < 0) throw new GlbFormatException(name + ": missing BIN chunk");

            object root;
            try { root = JsonReader.Parse(jsonText); }
            catch (FormatException e) { throw new GlbFormatException(name + ": " + e.Message); }
            if (!(root is Dictionary<string, object>)) throw new GlbFormatException(name + ": JSON root is not an object");

            Validate(root, name);

            var meshes = JsonReader.Arr(root, "meshes");
            if (meshes == null || meshes.Count == 0) throw new GlbFormatException(name + ": no mesh");
            if (meshes.Count != 1) throw new GlbFormatException(name + ": " + meshes.Count + " meshes, exactly one expected");
            var prims = JsonReader.Arr(meshes[0], "primitives");
            if (prims == null || prims.Count == 0) throw new GlbFormatException(name + ": mesh has no primitive");
            if (prims.Count != 1) throw new GlbFormatException(name + ": " + prims.Count + " primitives, exactly one expected");
            object prim = prims[0];

            if ((int)JsonReader.Num(prim, "mode", 4) != 4) throw new GlbFormatException(name + ": primitive mode is not triangles (4)");
            if (JsonReader.Obj(prim, "extensions") != null) throw new GlbFormatException(name + ": primitive extensions (e.g. Draco) are not supported");
            if (JsonReader.Has(prim, "targets")) throw new GlbFormatException(name + ": morph targets are not supported");

            var attrs = JsonReader.Obj(prim, "attributes");
            if (attrs == null) throw new GlbFormatException(name + ": primitive has no attributes");

            var ctx = new Ctx { Root = root, Bin = glb, BinOffset = binOffset, BinLength = binLength, Name = name };
            var data = new GlbMeshData();
            int count, nCount, uCount;
            data.Positions = ReadFloats(ctx, AttrIndex(attrs, "POSITION", name), 3, out count);
            data.Normals = ReadFloats(ctx, AttrIndex(attrs, "NORMAL", name), 3, out nCount);
            data.Uvs = ReadFloats(ctx, AttrIndex(attrs, "TEXCOORD_0", name), 2, out uCount);
            if (nCount != count || uCount != count)
                throw new GlbFormatException(name + ": attribute counts differ (POSITION " + count + ", NORMAL " + nCount + ", TEXCOORD_0 " + uCount + ")");

            if (!JsonReader.Has(prim, "indices")) throw new GlbFormatException(name + ": primitive is not indexed");
            data.Indices = ReadIndices(ctx, (int)JsonReader.Num(prim, "indices", -1), count);
            if (data.Indices.Length % 3 != 0) throw new GlbFormatException(name + ": index count " + data.Indices.Length + " is not a multiple of 3");
            return data;
        }

        private sealed class Ctx
        {
            public object Root;
            public byte[] Bin;
            public int BinOffset, BinLength;
            public string Name;
        }

        private static void Validate(object root, string name)
        {
            var skins = JsonReader.Arr(root, "skins");
            if (skins != null && skins.Count > 0) throw new GlbFormatException(name + ": skins are not supported");
            var anims = JsonReader.Arr(root, "animations");
            if (anims != null && anims.Count > 0) throw new GlbFormatException(name + ": animations are not supported (frames are separate meshes)");
            var images = JsonReader.Arr(root, "images");
            if (images != null && images.Count > 0) throw new GlbFormatException(name + ": embedded images are not supported (textures are separate PNGs)");
            var req = JsonReader.Arr(root, "extensionsRequired");
            if (req != null && req.Count > 0)
            {
                string first = req[0] as string;
                throw new GlbFormatException(name + ": required extension '" + first + "' is not supported"
                    + (first == "KHR_draco_mesh_compression" ? " (Draco)" : string.Empty));
            }
            var used = JsonReader.Arr(root, "extensionsUsed");
            if (used != null && used.Contains("KHR_draco_mesh_compression"))
                throw new GlbFormatException(name + ": Draco compression is not supported");
            var buffers = JsonReader.Arr(root, "buffers");
            if (buffers == null || buffers.Count == 0) throw new GlbFormatException(name + ": no buffer");
            if (JsonReader.Str(buffers[0], "uri") != null) throw new GlbFormatException(name + ": buffer URIs are not supported (GLB BIN chunk only)");
        }

        private static int AttrIndex(Dictionary<string, object> attrs, string key, string name)
        {
            object v;
            if (!attrs.TryGetValue(key, out v) || !(v is double)) throw new GlbFormatException(name + ": missing attribute " + key);
            return (int)(double)v;
        }

        private static float[] ReadFloats(Ctx c, int accessorIndex, int comps, out int count)
        {
            int compType, stride, offset;
            LocateAccessor(c, accessorIndex, comps == 3 ? "VEC3" : "VEC2", out count, out compType, out stride, out offset);
            if (compType != Float) throw new GlbFormatException(c.Name + ": accessor " + accessorIndex + " must be FLOAT");
            if (stride == 0) stride = comps * 4;
            CheckRange(c, accessorIndex, count, stride, comps * 4, offset);
            var result = new float[count * comps];
            for (int i = 0; i < count; i++)
            {
                int p = offset + i * stride;
                for (int k = 0; k < comps; k++)
                    result[i * comps + k] = BitConverter.ToSingle(c.Bin, p + k * 4);
            }
            return result;
        }

        private static int[] ReadIndices(Ctx c, int accessorIndex, int vertexCount)
        {
            int count, compType, stride, offset;
            LocateAccessor(c, accessorIndex, "SCALAR", out count, out compType, out stride, out offset);
            int size;
            switch (compType)
            {
                case UByte: size = 1; break;
                case UShort: size = 2; break;
                case UInt: size = 4; break;
                default: throw new GlbFormatException(c.Name + ": index accessor must be UNSIGNED_BYTE/SHORT/INT");
            }
            if (stride == 0) stride = size;
            CheckRange(c, accessorIndex, count, stride, size, offset);
            var result = new int[count];
            for (int i = 0; i < count; i++)
            {
                int p = offset + i * stride;
                long v = size == 1 ? c.Bin[p] : size == 2 ? BitConverter.ToUInt16(c.Bin, p) : BitConverter.ToUInt32(c.Bin, p);
                if (v >= vertexCount) throw new GlbFormatException(c.Name + ": index " + v + " out of range (" + vertexCount + " vertices)");
                result[i] = (int)v;
            }
            return result;
        }

        private static void LocateAccessor(Ctx c, int index, string type, out int count, out int compType, out int stride, out int offset)
        {
            var accessors = JsonReader.Arr(c.Root, "accessors");
            if (accessors == null || index < 0 || index >= accessors.Count) throw new GlbFormatException(c.Name + ": accessor " + index + " does not exist");
            object acc = accessors[index];
            if (JsonReader.Obj(acc, "sparse") != null) throw new GlbFormatException(c.Name + ": sparse accessors are not supported");
            if (JsonReader.Str(acc, "type") != type) throw new GlbFormatException(c.Name + ": accessor " + index + " must be " + type);
            if (!JsonReader.Has(acc, "bufferView")) throw new GlbFormatException(c.Name + ": accessor " + index + " has no bufferView");
            count = (int)JsonReader.Num(acc, "count", 0);
            compType = (int)JsonReader.Num(acc, "componentType", 0);
            if (count <= 0) throw new GlbFormatException(c.Name + ": accessor " + index + " is empty");

            var views = JsonReader.Arr(c.Root, "bufferViews");
            int vi = (int)JsonReader.Num(acc, "bufferView", -1);
            if (views == null || vi < 0 || vi >= views.Count) throw new GlbFormatException(c.Name + ": bufferView " + vi + " does not exist");
            object view = views[vi];
            if ((int)JsonReader.Num(view, "buffer", 0) != 0) throw new GlbFormatException(c.Name + ": only buffer 0 (GLB BIN) is supported");
            stride = (int)JsonReader.Num(view, "byteStride", 0);
            int viewStart = c.BinOffset + (int)JsonReader.Num(view, "byteOffset", 0);
            offset = viewStart + (int)JsonReader.Num(acc, "byteOffset", 0);
            if (viewStart + (int)JsonReader.Num(view, "byteLength", 0) > c.BinOffset + c.BinLength)
                throw new GlbFormatException(c.Name + ": bufferView " + vi + " exceeds the BIN chunk");
        }

        private static void CheckRange(Ctx c, int accessorIndex, int count, int stride, int elemSize, int offset)
        {
            long end = (long)offset + (long)(count - 1) * stride + elemSize;
            if (offset < c.BinOffset || end > c.BinOffset + c.BinLength)
                throw new GlbFormatException(c.Name + ": accessor " + accessorIndex + " reads outside the BIN chunk");
        }

        private static uint ReadU32(byte[] b, int o)
        {
            return BitConverter.ToUInt32(b, o);
        }
    }
}
