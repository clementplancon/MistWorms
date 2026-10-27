#if MW_DEV
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using MistWorms.Render.Gpu;

namespace MistWorms.Dev
{
    /// <summary>
    /// Spike of the GPU worm path (visual v2): the AssetBundle "mistworms_shaders" (shader MistWorms/WormGPU, its material, the compute shader MWTest, a variant
    /// collection), a procedural tube mesh and a test field of up to 10000 worms drawn by procedural instancing (<c>Graphics.DrawMeshInstancedIndirect</c>) from
    /// a structured buffer. The vertex shader bends the straight tube along a curve and animates the crawl wave and the peristalsis from <c>_Time</c> and a per
    /// worm phase, so nothing but the buffer upload happens on the main thread. The existing mass and worm rendering is not touched.
    /// Everything is preallocated at the first spawn: the per frame path does not allocate.
    /// </summary>
    internal static class GpuWorms
    {
        public const int MaxWorms = 16384;

        /// <summary>Per worm data (48 bytes), the layout of <c>WormInst</c> in WormGPU.shader.</summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct Inst
        {
            public Vector4 PosLen;      // xyz tail position, w = scale of the length
            public Vector4 Quat;        // orientation, the tube axis is local +z
            public Vector4 Wave;        // phase (cycles), amplitude 0..1, frequency (Hz), bend -1..1
        }

        private static Shader s_shader;
        private static Material s_material;
        private static ComputeShader s_compute;
        private static Mesh s_mesh;
        private static GraphicsBuffer s_buffer, s_args;
        private static NativeArray<Inst> s_inst;       // persistent: SetData(NativeArray) goes straight to the native upload (no managed Array / Marshal.SizeOf path)
        private static readonly uint[] s_argsData = new uint[5];
        private static Bounds s_bounds = new Bounds(Vector3.zero, new Vector3(400f, 400f, 400f));
        private static GpuWormsDriver s_driver;

        public static bool Loaded { get; private set; }
        public static bool BundleLoaded => GpuWormBundle.BundleLoaded;
        public static string Error { get; private set; }
        public static bool ShaderSupported { get; private set; }
        public static bool ComputeSupported { get; private set; }
        public static bool ComputeOk { get; private set; }
        public static string ComputeResult { get; private set; } = "not run";
        public static int Count { get; private set; }
        public static int IndexCount { get; private set; }
        public static int VertexCount { get; private set; }

        /// <summary>Draw call skipped and nothing uploaded while true (the data stays), for the differential allocation check.</summary>
        public static bool Hidden;

        // ---- main thread cost of the per frame path (upload + submission), reset by ResetStats
        public static double LastMs { get; private set; }
        public static double MaxMs { get; private set; }
        public static int Frames { get; private set; }
        private static double s_sumMs;
        public static double AvgMs => Frames > 0 ? s_sumMs / Frames : 0.0;

        /// <summary>When true each frame also sums the rises of the managed heap seen around our own Update (adds its own cost: measure the timing apart).</summary>
        public static bool MeasureAlloc;
        public static long OwnAllocBytes;
        public static int OwnAllocFrames;
        public static long ControlAllocBytes;   // the same measure around a busy wait of the same length as our Frame: what the rest of the process allocates meanwhile (the probe reads the whole heap)
        public static int ControlAllocFrames;       // frames of the window in which the heap rose around our own Frame (a real allocation does it in every frame, noise in a few)

        public static void ResetStats()
        {
            LastMs = 0; MaxMs = 0; Frames = 0; s_sumMs = 0; OwnAllocBytes = 0; OwnAllocFrames = 0; ControlAllocBytes = 0; ControlAllocFrames = 0;
        }

        // ---------------------------------------------------------------- load

        /// <summary>Loads the bundle, the mesh and the buffers, runs the compute kernel once. Never throws; <see cref="Error"/> says why it did not load.</summary>
        public static void Load()
        {
            if (Loaded || Error != null) return;
            try
            {
                ComputeSupported = SystemInfo.supportsComputeShaders;
                if (!GpuWormBundle.Load()) { Error = GpuWormBundle.Error; return; }
                s_shader = GpuWormBundle.GpuShader;
                s_compute = GpuWormBundle.TestCompute;
                s_material = GpuWormBundle.GpuMaterial;
                ShaderSupported = GpuWormBundle.GpuShaderSupported;

                s_mesh = GpuWormMesh.Build();
                s_mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                IndexCount = (int)s_mesh.GetIndexCount(0);
                VertexCount = s_mesh.vertexCount;

                RunComputeTest();

                var go = new GameObject("MW_GpuWorms") { hideFlags = HideFlags.HideAndDontSave };
                UnityEngine.Object.DontDestroyOnLoad(go);
                s_driver = go.AddComponent<GpuWormsDriver>();

                Loaded = ShaderSupported;
                if (!ShaderSupported) Error = "shader not supported on this device";
                Plugin.Log.LogInfo($"[GpuWorms] ready={Loaded}, mesh {VertexCount} vertices / {IndexCount / 3} triangles");
            }
            catch (Exception e)
            {
                Error = e.Message;
                Plugin.LogException("GpuWorms.Load", e);
            }
        }

        /// <summary>One dispatch of the trivial kernel: Result[i] = i * 2 + 7 for 64 entries, read back once (startup, not a per frame path).</summary>
        private static void RunComputeTest()
        {
            if (!ComputeSupported || s_compute == null) { ComputeResult = "compute not supported"; Plugin.Log.LogWarning("[GpuWorms] compute test skipped: not supported"); return; }
            GraphicsBuffer buf = null;
            try
            {
                const int n = 64;
                buf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, sizeof(uint));
                int kernel = s_compute.FindKernel("CSMain");
                s_compute.SetBuffer(kernel, "Result", buf);
                s_compute.Dispatch(kernel, 1, 1, 1);
                var result = new uint[n];
                buf.GetData(result);
                int bad = 0;
                for (int i = 0; i < n; i++) if (result[i] != (uint)(i * 2 + 7)) bad++;
                ComputeOk = bad == 0;
                ComputeResult = ComputeOk ? "ok (64 values, Result[i] = 2i + 7)" : $"WRONG: {bad} of {n} values differ, first {result[0]} {result[1]} {result[2]}";
            }
            catch (Exception e)
            {
                ComputeResult = "failed: " + e.Message;
            }
            finally
            {
                buf?.Release();
            }
            Plugin.Log.LogInfo("[GpuWorms] compute kernel: " + ComputeResult);
        }

        // ---------------------------------------------------------------- field

        /// <summary>
        /// A test field of <paramref name="count"/> worms (0 clears it) on the ground around <paramref name="center"/>: an annulus of about 4 worms per square metre,
        /// each on the terrain height, random heading, phase, amplitude, frequency and bend. Allocates the arrays and buffers once, at the first non-empty call.
        /// </summary>
        public static bool Spawn(int count, Vector3 center, out string how)
        {
            how = null;
            count = Mathf.Clamp(count, 0, MaxWorms);
            if (!Loaded) { how = "not loaded: " + (Error ?? "unknown"); return false; }
            if (count == 0)
            {
                Count = 0;
                how = "cleared";
                return true;
            }

            EnsureBuffers();

            float radius = Mathf.Sqrt(count / (4f * Mathf.PI) + 0.36f);
            uint rng = 0x9E3779B9u;
            ZoneSystem zones = ZoneSystem.instance;
            for (int i = 0; i < count; i++)
            {
                float a = Next(ref rng) * Mathf.PI * 2f;
                float r = Mathf.Sqrt(Mathf.Lerp(0.36f, radius * radius, Next(ref rng)));
                Vector3 p = center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                p.y = zones != null ? zones.GetGroundHeight(p) + 0.004f : center.y;
                Quaternion q = Quaternion.Euler(0f, Next(ref rng) * 360f, 0f);
                var inst = new Inst
                {
                    PosLen = new Vector4(p.x, p.y, p.z, 1f),
                    Quat = new Vector4(q.x, q.y, q.z, q.w),
                };
                inst.Wave = new Vector4(Next(ref rng), 0.4f + 0.6f * Next(ref rng), 1.1f + 0.7f * Next(ref rng), (Next(ref rng) - 0.5f) * 1.6f);
                s_inst[i] = inst;
            }
            s_buffer.SetData(s_inst, 0, 0, count);
            Count = count;
            s_bounds = new Bounds(center, new Vector3(radius * 2f + 2f, 4f, radius * 2f + 2f));
            s_argsData[0] = (uint)IndexCount;
            s_argsData[1] = (uint)count;
            s_argsData[2] = 0; s_argsData[3] = 0; s_argsData[4] = 0;
            s_args.SetData(s_argsData);
            Hidden = false;
            ResetStats();

            Camera cam = Utils.GetMainCamera();
            how = $"{count} worms within {radius:0.0} m of {center.x:0},{center.z:0}";
            if (cam != null)
                Plugin.Log.LogInfo($"[GpuWorms] {how}; camera: renderingPath={cam.actualRenderingPath} hdr={cam.allowHDR} fogMode={RenderSettings.fogMode} fog={RenderSettings.fog} colorSpace={QualitySettings.activeColorSpace}");
            return true;
        }

        private static void EnsureBuffers()
        {
            if (s_inst.IsCreated) return;
            s_inst = new NativeArray<Inst>(MaxWorms, Allocator.Persistent);
            s_buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, MaxWorms, Marshal.SizeOf(typeof(Inst)));
            s_args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            s_material.SetBuffer("_Worms", s_buffer);
        }

        /// <summary>
        /// A single GPU worm lying along <paramref name="dir"/> (horizontal), its body centred on <paramref name="centre"/> (a point on the ground), for the lighting
        /// comparison with the flipbook worm. Replaces the field. The wave is the same one as the field's.
        /// </summary>
        public static bool PlaceSingle(Vector3 centre, Vector3 dir, out string how)
        {
            how = null;
            if (!Loaded) { how = "not loaded: " + (Error ?? "unknown"); return false; }
            EnsureBuffers();
            dir.y = 0f;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.forward;
            Vector3 tail = centre - dir * (GpuWormMesh.Length * 0.5f);
            Quaternion q = Quaternion.LookRotation(dir, Vector3.up);
            s_inst[0] = new Inst
            {
                PosLen = new Vector4(tail.x, tail.y, tail.z, 1f),
                Quat = new Vector4(q.x, q.y, q.z, q.w),
                Wave = new Vector4(0.3f, 0.6f, 1.3f, 0f),
            };
            s_buffer.SetData(s_inst, 0, 0, 1);
            Count = 1;
            s_bounds = new Bounds(centre, new Vector3(2f, 2f, 2f));
            s_argsData[0] = (uint)IndexCount;
            s_argsData[1] = 1;
            s_argsData[2] = 0; s_argsData[3] = 0; s_argsData[4] = 0;
            s_args.SetData(s_argsData);
            Hidden = false;
            ResetStats();
            how = "1 worm at " + centre.ToString("0.00");
            return true;
        }

        private static float Next(ref uint s)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            return (s & 0xFFFFFF) / 16777216f;
        }

        /// <summary>The per frame path: upload the field and submit the draw. No allocation.</summary>
        internal static void Frame()
        {
            if (Count == 0 || Hidden || s_buffer == null) return;
            long heap0 = MeasureAlloc ? Profiler.GetMonoUsedSizeLong() : 0L;
            long t0 = Stopwatch.GetTimestamp();
            // The upload is repeated every frame on purpose: it is the cost of a field refreshed by a worker (the real path refreshes at 10 to 20 Hz).
            s_buffer.SetData(s_inst, 0, 0, Count);
            Graphics.DrawMeshInstancedIndirect(s_mesh, 0, s_material, s_bounds, s_args, 0, null, ShadowCastingMode.On, true, 0);
            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            LastMs = ms;
            if (ms > MaxMs) MaxMs = ms;
            s_sumMs += ms;
            Frames++;
            if (MeasureAlloc)
            {
                long rise = Profiler.GetMonoUsedSizeLong() - heap0;
                if (rise > 0) { OwnAllocBytes += rise; OwnAllocFrames++; }
                long span = Stopwatch.GetTimestamp() - t0, c0 = Profiler.GetMonoUsedSizeLong(), until = Stopwatch.GetTimestamp() + span;
                while (Stopwatch.GetTimestamp() < until) { }
                long crise = Profiler.GetMonoUsedSizeLong() - c0;
                if (crise > 0) { ControlAllocBytes += crise; ControlAllocFrames++; }
            }
        }

        /// <summary>The block of GET /state.</summary>
        public static Dictionary<string, object> StateJson() => new Dictionary<string, object>
        {
            { "loaded", Loaded },
            { "error", Error },
            { "shaderSupported", ShaderSupported },
            { "computeSupported", ComputeSupported },
            { "computeResult", ComputeResult },
            { "count", Count },
            { "hidden", Hidden },
            { "frames", Frames },
            { "mainThreadMsAvg", AvgMs },
            { "mainThreadMsLast", LastMs },
            { "mainThreadMsMax", MaxMs },
            { "triangles", Loaded ? Count * (IndexCount / 3) : 0 },
        };

        private sealed class GpuWormsDriver : MonoBehaviour
        {
            private void Update() => Frame();
        }
    }

    /// <summary>
    /// The straight tube of the GPU worm: 8 sides by 20 rings, 0.30 m long along +z from 0 (tail) to 0.30 (head), body radius 0.009 (diameter 1.8 cm), head bulb 0.0105
    /// (2.1 cm), fuselage taper at both ends. UVs follow the atlas of make_worm.py (worm_albedo.png): U around the body (0.25 = back), V along the body, in the
    /// convention of the glb the game imports (V flipped by Unity): the head, which is at the bottom of the PNG, has v = 0 here, the tail v = 1.
    /// </summary>
    internal static class GpuWormMesh
    {
        public const int Sides = 8, Rings = 20;
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

        public static Mesh Build()
        {
            int stride = Sides + 1;                       // the seam vertex is duplicated for the u coordinate
            int vertexCount = stride * Rings;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var tangents = new Vector4[vertexCount];
            var uvs = new Vector2[vertexCount];
            for (int ring = 0; ring < Rings; ring++)
            {
                float t = ring / (float)(Rings - 1);
                float r = Radius * Profile(t);
                // Slope of the profile along z, for the normal (clamped near the ends where it goes vertical).
                float dr = Radius * (Profile(Mathf.Min(1f, t + 0.01f)) - Profile(Mathf.Max(0f, t - 0.01f))) / (Mathf.Min(1f, t + 0.01f) - Mathf.Max(0f, t - 0.01f)) / Length;
                for (int s = 0; s < stride; s++)
                {
                    float a = s / (float)Sides * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                    int i = ring * stride + s;
                    vertices[i] = new Vector3(c * r, sn * r, t * Length);
                    normals[i] = new Vector3(c, sn, -dr).normalized;
                    tangents[i] = new Vector4(-sn, c, 0f, 1f);
                    uvs[i] = new Vector2(s / (float)Sides, 1f - t);
                }
            }

            var indices = new int[(Rings - 1) * Sides * 6];
            int n = 0;
            for (int ring = 0; ring < Rings - 1; ring++)
            {
                for (int s = 0; s < Sides; s++)
                {
                    int a = ring * stride + s, b = a + 1, c = a + stride, d = c + 1;
                    indices[n++] = a; indices[n++] = b; indices[n++] = c;
                    indices[n++] = b; indices[n++] = d; indices[n++] = c;
                }
            }

            var mesh = new Mesh { name = "MW_GpuWormTube", vertices = vertices, normals = normals, tangents = tangents, uv = uvs, triangles = indices };
            // The bend and the wave move the body sideways: generous bounds (the draw gives its own bounds anyway).
            mesh.bounds = new Bounds(new Vector3(0f, 0f, Length * 0.5f), new Vector3(Length, Length * 0.6f, Length * 1.1f));
            return mesh;
        }
    }
}
#endif
