using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace MistWorms.Render.Gpu
{
    /// <summary>
    /// The AssetBundle "mistworms_shaders" (tools/build-shaders.ps1, shipped in &lt;plugin&gt;/assets): the shaders MistWorms/WormRope and MistWorms/WormGPU (spike), their materials,
    /// the compute shaders WormSim and MWTest and the ShaderVariantCollection. Loaded once for the whole mod (a bundle cannot be loaded twice). Never throws:
    /// <see cref="Error"/> says why it did not load, and <see cref="Init"/> then leaves the callers on the CPU path.
    /// </summary>
    internal static class GpuWormBundle
    {
        public const string BundleFile = "mistworms_shaders";

        private static AssetBundle s_bundle;
        private static bool s_tried;
        private static bool s_warned;

        public static bool Loaded { get; private set; }
        public static string Error { get; private set; }
        public static bool ComputeSupported { get; private set; }
        public static bool RopeSupported { get; private set; }
        public static bool GpuShaderSupported { get; private set; }
        public static Shader GpuShader { get; private set; }
        public static Material GpuMaterial { get; private set; }
        public static Material RopeMaterial { get; private set; }
        public static ComputeShader SimCompute { get; private set; }
        public static ComputeShader TestCompute { get; private set; }
        public static string VariantsInfo { get; private set; } = "not loaded";
        public static bool BundleLoaded => s_bundle != null;

        /// <summary>Loads the bundle and the assets (idempotent). True when the rope shader, the simulation and the spike assets are all there.</summary>
        public static bool Load()
        {
            if (s_tried) return Loaded;
            s_tried = true;
            try
            {
                GraphicsDeviceType api = SystemInfo.graphicsDeviceType;
                ComputeSupported = SystemInfo.supportsComputeShaders;
                Plugin.Log.LogInfo($"[GpuWorms] graphics={api} compute={ComputeSupported} maxComputeBufferInputsVertex={SystemInfo.maxComputeBufferInputsVertex} " +
                                   $"shaderLevel={SystemInfo.graphicsShaderLevel} colorSpace={QualitySettings.activeColorSpace} instancing={SystemInfo.supportsInstancing}");
                if (api == GraphicsDeviceType.Null || Application.isBatchMode) { Error = "no graphics device (batch mode / dedicated server)"; Plugin.Log.LogInfo("[GpuWorms] skipped: " + Error); return false; }

                string path = Path.Combine(Plugin.PluginDir, "assets", BundleFile);
                if (!File.Exists(path)) { Error = "bundle not found: " + path + " (tools/build-shaders.ps1)"; Plugin.Log.LogWarning("[GpuWorms] " + Error); return false; }

                s_bundle = AssetBundle.LoadFromFile(path);
                if (s_bundle == null) { Error = "AssetBundle.LoadFromFile returned null (bundle already loaded or built for another Unity version)"; Plugin.Log.LogError("[GpuWorms] " + Error); return false; }

                GpuShader = s_bundle.LoadAsset<Shader>("Assets/Shaders/WormGPU.shader");
                Shader rope = s_bundle.LoadAsset<Shader>("Assets/Shaders/WormRope.shader");
                Material gpuSource = s_bundle.LoadAsset<Material>("Assets/Materials/WormGPU.mat");
                Material ropeSource = s_bundle.LoadAsset<Material>("Assets/Materials/WormRope.mat");
                SimCompute = s_bundle.LoadAsset<ComputeShader>("Assets/Shaders/WormSim.compute");
                TestCompute = s_bundle.LoadAsset<ComputeShader>("Assets/Shaders/MWTest.compute");
                var variants = s_bundle.LoadAsset<ShaderVariantCollection>("Assets/Materials/Worms.shadervariants");
                if (GpuShader == null || rope == null || gpuSource == null || ropeSource == null || SimCompute == null || TestCompute == null)
                {
                    Error = $"missing assets in the bundle (WormGPU {GpuShader != null}, WormRope {rope != null}, materials {gpuSource != null}/{ropeSource != null}, WormSim {SimCompute != null}, MWTest {TestCompute != null})";
                    Plugin.Log.LogError("[GpuWorms] " + Error);
                    return false;
                }

                GpuShaderSupported = GpuShader.isSupported;
                RopeSupported = rope.isSupported;
                GpuShader.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                rope.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                SimCompute.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                TestCompute.hideFlags |= HideFlags.DontUnloadUnusedAsset;
                GpuMaterial = new Material(gpuSource) { name = "MW_WormGPU", hideFlags = HideFlags.DontUnloadUnusedAsset, enableInstancing = true };
                RopeMaterial = new Material(ropeSource) { name = "MW_WormRope", hideFlags = HideFlags.DontUnloadUnusedAsset, enableInstancing = true };
                VariantsInfo = variants != null ? $"{variants.shaderCount} shader(s), {variants.variantCount} variant(s), warmed up: {WarmUp(variants)}" : "missing";
                Plugin.Log.LogInfo($"[GpuWorms] bundle loaded: WormGPU isSupported={GpuShaderSupported}, WormRope isSupported={RopeSupported}, compute {SimCompute.name} and {TestCompute.name}, variants {VariantsInfo}");
                Loaded = true;
                return true;
            }
            catch (Exception e)
            {
                Error = e.Message;
                Plugin.LogException("GpuWormBundle.Load", e);
                return false;
            }
        }

        /// <summary>Loads the bundle if needed and initialises the host of the GPU masses (single warning when unavailable). True when masses can be created.</summary>
        public static bool InitHost()
        {
            if (GpuWormHost.Ready) return true;
            if (!Load())
            {
                if (!s_warned)
                {
                    s_warned = true;
                    Plugin.Log.LogWarning("[GpuWormHost] GPU worms unavailable, the CPU path is kept: " + (Error ?? "bundle not loaded"));
                }
                return false;
            }
            GpuWormHost.Log = s => Plugin.Log.LogInfo(s);
            GpuWormHost.LogWarning = s => Plugin.Log.LogWarning(s);
            GpuWormHost.CameraProvider = Utils.GetMainCamera;
            return GpuWormHost.TryInit(new GpuWormAssets { Compute = SimCompute, RopeMaterial = RopeMaterial }, true);
        }

        private static string WarmUp(ShaderVariantCollection svc)
        {
            try { svc.WarmUp(); return svc.isWarmedUp.ToString(); }
            catch (Exception e) { return "failed (" + e.Message + ")"; }
        }
    }
}
