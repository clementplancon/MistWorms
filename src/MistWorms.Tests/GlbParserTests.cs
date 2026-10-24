using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MistWorms.Assets;
using Xunit;

namespace MistWorms.Tests
{
    public class GlbParserTests
    {
        // ---------------------------------------------------------------- synthetic GLB builder

        private static byte[] Glb(string json, byte[] bin, bool patchLength = true)
        {
            byte[] j = Encoding.UTF8.GetBytes(json);
            int jPad = (4 - j.Length % 4) % 4;
            int bPad = (4 - bin.Length % 4) % 4;
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                w.Write(0x46546C67u);
                w.Write(2u);
                w.Write(0u); // patched below
                w.Write((uint)(j.Length + jPad));
                w.Write(0x4E4F534Au);
                w.Write(j);
                for (int i = 0; i < jPad; i++) w.Write((byte)' ');
                w.Write((uint)(bin.Length + bPad));
                w.Write(0x004E4942u);
                w.Write(bin);
                for (int i = 0; i < bPad; i++) w.Write((byte)0);
                byte[] all = ms.ToArray();
                if (patchLength) BitConverter.GetBytes((uint)all.Length).CopyTo(all, 8);
                return all;
            }
        }

        private static byte[] Floats(params float[] v)
        {
            var b = new byte[v.Length * 4];
            Buffer.BlockCopy(v, 0, b, 0, b.Length);
            return b;
        }

        private static byte[] Concat(params byte[][] parts)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var p in parts) ms.Write(p, 0, p.Length);
                return ms.ToArray();
            }
        }

        private static byte[] U16(params ushort[] v)
        {
            var b = new byte[v.Length * 2];
            Buffer.BlockCopy(v, 0, b, 0, b.Length);
            return b;
        }

        private static byte[] U32(params uint[] v)
        {
            var b = new byte[v.Length * 4];
            Buffer.BlockCopy(v, 0, b, 0, b.Length);
            return b;
        }

        private const string TriangleJson =
            "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":128}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":36},{\"buffer\":0,\"byteOffset\":36,\"byteLength\":36}," +
            "{\"buffer\":0,\"byteOffset\":72,\"byteLength\":24},{\"buffer\":0,\"byteOffset\":96,\"byteLength\":6}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"}," +
            "{\"bufferView\":1,\"componentType\":5126,\"count\":3,\"type\":\"VEC3\"}," +
            "{\"bufferView\":2,\"componentType\":5126,\"count\":3,\"type\":\"VEC2\"}," +
            "{\"bufferView\":3,\"componentType\":5123,\"count\":3,\"type\":\"SCALAR\"}]," +
            "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2},\"indices\":3,\"mode\":4}]}]%EXTRA%}";

        private static byte[] TriangleBin()
        {
            return Concat(
                Floats(0, 0, 0, 1, 0, 0, 0, 1, 0),
                Floats(0, 0, 1, 0, 0, 1, 0, 0, 1),
                Floats(0, 0, 1, 0, 0, 1),
                U16(0, 1, 2));
        }

        private static byte[] Triangle(string extraJson = "")
        {
            return Glb(TriangleJson.Replace("%EXTRA%", extraJson), TriangleBin());
        }

        // ---------------------------------------------------------------- tests

        [Fact]
        public void Triangle_U16Indices()
        {
            GlbMeshData d = GlbParser.Parse(Triangle(), "tri.glb");
            Assert.Equal(new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0 }, d.Positions);
            Assert.Equal(new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1 }, d.Normals);
            Assert.Equal(new float[] { 0, 0, 1, 0, 0, 1 }, d.Uvs);
            Assert.Equal(new[] { 0, 1, 2 }, d.Indices);
        }

        [Fact]
        public void Quad_U32Indices_InterleavedStride()
        {
            // 4 vertices interleaved as pos(3) nrm(3) uv(2) = 32 bytes, plus 6 u32 indices.
            var vb = Floats(
                0, 0, 0, 0, 0, 1, 0, 0,
                1, 0, 0, 0, 0, 1, 1, 0,
                1, 1, 0, 0, 0, 1, 1, 1,
                0, 1, 0, 0, 0, 1, 0, 1);
            var ib = U32(0, 1, 2, 0, 2, 3);
            string json =
                "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":152}]," +
                "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":128,\"byteStride\":32},{\"buffer\":0,\"byteOffset\":128,\"byteLength\":24}]," +
                "\"accessors\":[{\"bufferView\":0,\"byteOffset\":0,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"}," +
                "{\"bufferView\":0,\"byteOffset\":12,\"componentType\":5126,\"count\":4,\"type\":\"VEC3\"}," +
                "{\"bufferView\":0,\"byteOffset\":24,\"componentType\":5126,\"count\":4,\"type\":\"VEC2\"}," +
                "{\"bufferView\":1,\"componentType\":5125,\"count\":6,\"type\":\"SCALAR\"}]," +
                "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2},\"indices\":3}]}]}";
            GlbMeshData d = GlbParser.Parse(Glb(json, Concat(vb, ib)), "quad.glb");
            Assert.Equal(12, d.Positions.Length);
            Assert.Equal(new float[] { 0, 0, 0, 1, 0, 0, 1, 1, 0, 0, 1, 0 }, d.Positions);
            Assert.Equal(new float[] { 0, 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1 }, d.Normals);
            Assert.Equal(new float[] { 0, 0, 1, 0, 1, 1, 0, 1 }, d.Uvs);
            Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, d.Indices);
        }

        [Fact]
        public void Skin_Rejected()
        {
            var e = Assert.Throws<GlbFormatException>(() => GlbParser.Parse(Triangle(",\"skins\":[{\"joints\":[0]}]"), "skinned.glb"));
            Assert.Contains("skinned.glb", e.Message);
            Assert.Contains("skin", e.Message);
        }

        [Fact]
        public void Animation_Rejected()
        {
            Assert.Throws<GlbFormatException>(() => GlbParser.Parse(Triangle(",\"animations\":[{}]")));
        }

        [Fact]
        public void EmbeddedImage_Rejected()
        {
            Assert.Throws<GlbFormatException>(() => GlbParser.Parse(Triangle(",\"images\":[{\"bufferView\":0,\"mimeType\":\"image/png\"}]")));
        }

        [Fact]
        public void Draco_Rejected()
        {
            var e = Assert.Throws<GlbFormatException>(() => GlbParser.Parse(Triangle(",\"extensionsRequired\":[\"KHR_draco_mesh_compression\"]")));
            Assert.Contains("Draco", e.Message);
        }

        [Fact]
        public void BadMagic_Rejected()
        {
            byte[] b = Triangle();
            b[0] = (byte)'X';
            var e = Assert.Throws<GlbFormatException>(() => GlbParser.Parse(b, "bad.glb"));
            Assert.Contains("magic", e.Message);
        }

        [Fact]
        public void TruncatedChunk_Rejected()
        {
            byte[] b = Triangle();
            var cut = new byte[b.Length - 20];
            Array.Copy(b, cut, cut.Length);
            var e = Assert.Throws<GlbFormatException>(() => GlbParser.Parse(cut, "cut.glb"));
            Assert.Contains("truncated", e.Message);
        }

        [Fact]
        public void TwoPrimitives_Rejected()
        {
            string json = TriangleJson.Replace("%EXTRA%", string.Empty)
                .Replace("\"mode\":4}]", "\"mode\":4},{\"attributes\":{\"POSITION\":0,\"NORMAL\":1,\"TEXCOORD_0\":2},\"indices\":3}]");
            Assert.Throws<GlbFormatException>(() => GlbParser.Parse(Glb(json, TriangleBin())));
        }

        [Fact]
        public void IndexOutOfRange_Rejected()
        {
            var bin = Concat(Floats(0, 0, 0, 1, 0, 0, 0, 1, 0), Floats(0, 0, 1, 0, 0, 1, 0, 0, 1), Floats(0, 0, 1, 0, 0, 1), U16(0, 1, 7));
            Assert.Throws<GlbFormatException>(() => GlbParser.Parse(Glb(TriangleJson.Replace("%EXTRA%", string.Empty), bin)));
        }

        // ---------------------------------------------------------------- repository assets (skipped until assets/build exists)

        private static string FindRepoRoot()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(dir))
            {
                if (File.Exists(Path.Combine(dir, "MistWorms.sln"))) return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }

        [Fact]
        public void RepoAssets_MatchManifestAndShareTopology()
        {
            string root = FindRepoRoot();
            if (root == null) return;
            string buildDir = Path.Combine(root, "assets", "build");
            string manifestPath = Path.Combine(buildDir, "manifest.json");
            if (!File.Exists(manifestPath)) return;

            object manifest = JsonReader.Parse(File.ReadAllText(manifestPath));
            var meshes = JsonReader.Arr(manifest, "meshes");
            var anims = JsonReader.Obj(manifest, "animations");
            Assert.NotNull(meshes);
            Assert.NotNull(anims);

            var parsed = new Dictionary<string, GlbMeshData>();
            foreach (object entry in meshes)
            {
                string name = JsonReader.Str(entry, "name");
                string file = JsonReader.Str(entry, "file");
                GlbMeshData d = GlbParser.Parse(File.ReadAllBytes(Path.Combine(buildDir, file)), file);
                parsed[name] = d;
                Assert.Equal((int)JsonReader.Num(entry, "vertices", -1), d.Positions.Length / 3);
                Assert.Equal((int)JsonReader.Num(entry, "triangles", -1), d.Indices.Length / 3);
            }

            foreach (var kv in anims)
            {
                int frames = (int)JsonReader.Num(kv.Value, "frames", 0);
                GlbMeshData first = null;
                for (int i = 0; i < frames; i++)
                {
                    string n = frames == 1 && parsed.ContainsKey(kv.Key) ? kv.Key : kv.Key + "_" + i.ToString("00");
                    Assert.True(parsed.ContainsKey(n), "missing mesh " + n);
                    GlbMeshData d = parsed[n];
                    if (first == null) { first = d; continue; }
                    Assert.Equal(first.Positions.Length, d.Positions.Length);
                    Assert.Equal(first.Indices, d.Indices);
                }
            }
        }
    }
}
