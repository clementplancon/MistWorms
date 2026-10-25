using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace MistWorms.Assets
{
    public sealed class AnimationInfo
    {
        public int Frames;
        public float Fps;
        public bool Loop;
    }

    /// <summary>
    /// Loads every mesh and texture listed in assets/manifest.json once, at plugin start (spec §13.3).
    /// Any missing or invalid asset makes Load return false with Error set; the mod then disables itself.
    /// </summary>
    public static class AssetCatalog
    {
        private const long SlowLoadMs = 300;

        /// <summary>Textures with one of these prefixes are Point filtered like the vanilla creatures (dump section 1); the nest stays bilinear.</summary>
        public static readonly string[] PointTexturePrefixes = { "worm_" };

        private static readonly Dictionary<string, Mesh> _meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Mesh[]> _frames = new Dictionary<string, Mesh[]>();
        private static readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, AnimationInfo> _anims = new Dictionary<string, AnimationInfo>();
        private static readonly Dictionary<string, int> _tris = new Dictionary<string, int>();
        private static readonly Dictionary<string, int[]> _frameTris = new Dictionary<string, int[]>();

        private static string _breakdown = string.Empty;

        public static bool IsReady { get; private set; }
        public static string Error { get; private set; }

        public static bool Load(string assetsDir)
        {
            IsReady = false;
            Error = null;
            try
            {
                var sw = Stopwatch.StartNew();
                Clear();
                LoadAll(assetsDir);
                sw.Stop();
                IsReady = true;
                string msg = "[Assets] loaded " + _meshes.Count + " meshes, " + _textures.Count + " textures in " + sw.ElapsedMilliseconds + " ms (" + _breakdown + ")";
                Plugin.Log?.LogInfo(msg);
                if (sw.ElapsedMilliseconds > SlowLoadMs) Plugin.Log?.LogWarning("[Assets] load time above the " + SlowLoadMs + " ms target");
                return true;
            }
            catch (Exception e)
            {
                Clear();
                Error = e is GlbFormatException || e is FileNotFoundException || e is InvalidDataException ? e.Message : e.ToString();
                Plugin.Log?.LogError("[Assets] load failed: " + Error);
                return false;
            }
        }

        public static Mesh[] Frames(string animation)
        {
            Mesh[] f;
            return _frames.TryGetValue(animation, out f) ? f : null;
        }

        /// <summary>Triangle count per frame (manifest 'triangles', else the parsed index count); read at load because UploadMeshData makes the meshes unreadable.</summary>
        public static int[] FrameTriangles(string animation)
        {
            int[] t;
            return _frameTris.TryGetValue(animation, out t) ? t : null;
        }

        public static Mesh Mesh(string name)
        {
            Mesh m;
            return _meshes.TryGetValue(name, out m) ? m : null;
        }

        public static Texture2D Texture(string name)
        {
            Texture2D t;
            return _textures.TryGetValue(name, out t) ? t : null;
        }

        public static AnimationInfo Animation(string name)
        {
            AnimationInfo a;
            return _anims.TryGetValue(name, out a) ? a : null;
        }

        private static void Clear()
        {
            foreach (var m in _meshes.Values) if (m != null) UnityEngine.Object.Destroy(m);
            foreach (var t in _textures.Values) if (t != null) UnityEngine.Object.Destroy(t);
            _meshes.Clear();
            _frames.Clear();
            _textures.Clear();
            _anims.Clear();
            _tris.Clear();
            _frameTris.Clear();
        }

        private static void LoadAll(string dir)
        {
            string manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) throw new FileNotFoundException("manifest.json not found in " + dir);
            object root;
            try { root = JsonReader.Parse(File.ReadAllText(manifestPath)); }
            catch (FormatException e) { throw new InvalidDataException("manifest.json: " + e.Message); }

            var anims = JsonReader.Obj(root, "animations");
            var meshes = JsonReader.Arr(root, "meshes");
            var textures = JsonReader.Arr(root, "textures");
            if (anims == null || meshes == null || textures == null)
                throw new InvalidDataException("manifest.json: needs 'animations', 'meshes' and 'textures'");

            // Topology reference per animation (first frame), compared against the following frames.
            var refIndices = new Dictionary<string, int[]>();
            var refVertices = new Dictionary<string, int>();
            var animOfMesh = new Dictionary<string, string>();
            foreach (var kv in anims)
            {
                int frames = (int)JsonReader.Num(kv.Value, "frames", 0);
                if (frames < 1) throw new InvalidDataException("manifest.json: animation '" + kv.Key + "' has no frames");
                _anims[kv.Key] = new AnimationInfo
                {
                    Frames = frames,
                    Fps = (float)JsonReader.Num(kv.Value, "fps", 0),
                    Loop = JsonReader.Bool(kv.Value, "loop", false)
                };
                if (frames == 1) animOfMesh[kv.Key] = kv.Key;
                for (int i = 0; i < frames; i++) animOfMesh[kv.Key + "_" + i.ToString("00")] = kv.Key;
            }

            var sw = Stopwatch.StartNew();

            // Stage 1, worker threads: read every .glb and .png, parse the glTF and convert to Unity-space arrays.
            // Nothing here touches the Unity API; failures are kept per slot and rethrown in manifest order below.
            int meshCount = meshes.Count, texCount = textures.Count;
            var meshNames = new string[meshCount];
            var meshFiles = new string[meshCount];
            for (int i = 0; i < meshCount; i++)
            {
                meshNames[i] = JsonReader.Str(meshes[i], "name");
                meshFiles[i] = JsonReader.Str(meshes[i], "file");
                if (meshNames[i] == null || meshFiles[i] == null) throw new InvalidDataException("manifest.json: mesh entry without name/file");
            }
            var texNames = new string[texCount];
            var texFiles = new string[texCount];
            for (int i = 0; i < texCount; i++)
            {
                texNames[i] = JsonReader.Str(textures[i], "name");
                texFiles[i] = JsonReader.Str(textures[i], "file");
                if (texNames[i] == null || texFiles[i] == null) throw new InvalidDataException("manifest.json: texture entry without name/file");
            }

            var parsed = new GlbMeshData[meshCount];
            var arrays = new GltfLoader.MeshArrays[meshCount];
            var meshErrors = new Exception[meshCount];
            var pngs = new byte[texCount][];
            var texErrors = new Exception[texCount];
            Parallel.For(0, meshCount + texCount, k =>
            {
                if (k < meshCount)
                {
                    try
                    {
                        string path = Path.Combine(dir, meshFiles[k]);
                        if (!File.Exists(path)) throw new FileNotFoundException("missing mesh file " + meshFiles[k]);
                        parsed[k] = GlbParser.Parse(File.ReadAllBytes(path), meshFiles[k]);
                        arrays[k] = GltfLoader.Convert(parsed[k]);
                    }
                    catch (Exception e) { meshErrors[k] = e; }
                }
                else
                {
                    int t = k - meshCount;
                    try
                    {
                        string path = Path.Combine(dir, texFiles[t]);
                        if (!File.Exists(path)) throw new FileNotFoundException("missing texture file " + texFiles[t]);
                        pngs[t] = File.ReadAllBytes(path);
                    }
                    catch (Exception e) { texErrors[t] = e; }
                }
            });
            long parallelMs = sw.ElapsedMilliseconds;
            sw.Restart();

            for (int mi = 0; mi < meshCount; mi++)
            {
                if (meshErrors[mi] != null) throw meshErrors[mi];
                string name = meshNames[mi];
                string file = meshFiles[mi];
                object entry = meshes[mi];
                GlbMeshData data = parsed[mi];
                int vc = data.Positions.Length / 3;

                string anim;
                if (animOfMesh.TryGetValue(name, out anim))
                {
                    int[] refIdx;
                    if (!refIndices.TryGetValue(anim, out refIdx))
                    {
                        refIndices[anim] = data.Indices;
                        refVertices[anim] = vc;
                    }
                    else if (refVertices[anim] != vc || !SameIndices(refIdx, data.Indices))
                        throw new GlbFormatException(file + ": topology differs from the first frame of '" + anim + "'");
                }

                int declared = (int)JsonReader.Num(entry, "vertices", vc);
                if (declared != vc) Plugin.Log?.LogWarning("[Assets] " + file + ": manifest says " + declared + " vertices, file has " + vc);

                _meshes[name] = GltfLoader.Build(arrays[mi], name);
                _tris[name] = (int)JsonReader.Num(entry, "triangles", data.Indices.Length / 3);
            }
            long meshMs = sw.ElapsedMilliseconds;
            sw.Restart();

            foreach (var kv in _anims)
            {
                var arr = new Mesh[kv.Value.Frames];
                var tris = new int[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    Mesh m;
                    string frameName = kv.Key + "_" + i.ToString("00");
                    string used = frameName;
                    if (!_meshes.TryGetValue(frameName, out m))
                    {
                        used = kv.Key;
                        if (!(kv.Value.Frames == 1 && _meshes.TryGetValue(kv.Key, out m)))
                            throw new FileNotFoundException("animation '" + kv.Key + "' is missing frame " + frameName);
                    }
                    arr[i] = m;
                    tris[i] = _tris[used];
                }
                _frames[kv.Key] = arr;
                _frameTris[kv.Key] = tris;
            }

            TextureLoader.ResetTimings();
            for (int ti = 0; ti < texCount; ti++)
            {
                if (texErrors[ti] != null) throw texErrors[ti];
                object entry = textures[ti];
                string name = texNames[ti];
                string file = texFiles[ti];

                Texture2D tex = TextureLoader.FromPng(pngs[ti], name, JsonReader.Bool(entry, "linear", false),
                    IsPointTexture(name));
                if (tex == null) throw new InvalidDataException(file + ": PNG could not be decoded");
                int w = (int)JsonReader.Num(entry, "width", tex.width), h = (int)JsonReader.Num(entry, "height", tex.height);
                if (tex.width != w || tex.height != h)
                {
                    int tw = tex.width, th = tex.height;
                    UnityEngine.Object.Destroy(tex);
                    throw new InvalidDataException(file + ": size " + tw + "x" + th + " does not match manifest " + w + "x" + h);
                }
                _textures[name] = tex;
            }
            _breakdown = "read+parse+convert " + parallelMs + " (parallel), meshes " + meshMs + ", textures " + sw.ElapsedMilliseconds
                + " [decode " + TextureLoader.Ms(TextureLoader.DecodeTicks).ToString("0") + ", compress " + TextureLoader.Ms(TextureLoader.CompressTicks).ToString("0")
                + ", apply+mips " + TextureLoader.Ms(TextureLoader.ApplyTicks).ToString("0") + "]";
        }

        private static bool IsPointTexture(string name)
        {
            for (int i = 0; i < PointTexturePrefixes.Length; i++)
                if (name.StartsWith(PointTexturePrefixes[i], StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool SameIndices(int[] a, int[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
