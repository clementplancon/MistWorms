using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the AssetBundle "mistworms_shaders" (StandaloneWindows64): the WormRope shader (ropes simulated by WormSim.compute) and the WormGPU shader (spike), their materials,
/// the WormSim and MWTest compute shaders and a ShaderVariantCollection (Worms.shadervariants) with the instancing, Deferred, forward and shadow caster variants of both shaders.
/// Entry point for the batchmode: -executeMethod BuildBundles.Build
/// Output: Build/mistworms_shaders (tools/build-shaders.ps1 copies it to assets/build). Exit code 1 on any failure.
/// </summary>
public static class BuildBundles
{
    private const string BundleName = "mistworms_shaders";
    private const string OutDir = "Build";
    private const string ShaderPath = "Assets/Shaders/WormGPU.shader";
    private const string RopeShaderPath = "Assets/Shaders/WormRope.shader";
    private const string ComputePath = "Assets/Shaders/MWTest.compute";
    private const string SimPath = "Assets/Shaders/WormSim.compute";
    private const string MaterialPath = "Assets/Materials/WormGPU.mat";
    private const string RopeMaterialPath = "Assets/Materials/WormRope.mat";
    private const string VariantsPath = "Assets/Materials/Worms.shadervariants";
    private const string OldVariantsPath = "Assets/Materials/WormGPU.shadervariants";

    // The game runs in Linear (measured in game: QualitySettings.activeColorSpace = Linear, Deferred, HDR): UNITY_COLORSPACE_GAMMA is fixed at compile time,
    // and the albedo texture must be imported as sRGB, the gloss and the normal map as linear data.
    private const ColorSpace GameColorSpace = ColorSpace.Linear;

    public static void Build()
    {
        try
        {
            Run();
        }
        catch (Exception e)
        {
            Debug.LogError("BuildBundles: FAILED: " + e);
            EditorApplication.Exit(1);
        }
    }

    private static void Run()
    {
        ConfigureProject();
        ImportTextures();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        Shader shader = LoadShader(ShaderPath);
        Shader rope = LoadShader(RopeShaderPath);

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        if (compute == null) throw new Exception("compute shader not found: " + ComputePath);
        var sim = AssetDatabase.LoadAssetAtPath<ComputeShader>(SimPath);
        if (sim == null) throw new Exception("compute shader not found: " + SimPath);
        CheckCompute(sim, new[] { "KInit", "KClear", "KPredict", "KSeparate", "KSolve", "KShift" });

        Material mat = MakeMaterial(shader, "WormGPU");
        Material ropeMat = MakeMaterial(rope, "WormRope");
        AssetDatabase.DeleteAsset(MaterialPath);
        AssetDatabase.CreateAsset(mat, MaterialPath);
        AssetDatabase.DeleteAsset(RopeMaterialPath);
        AssetDatabase.CreateAsset(ropeMat, RopeMaterialPath);

        var svc = new ShaderVariantCollection();
        var listing = new List<string>();
        BuildVariants(svc, shader, listing);
        BuildVariants(svc, rope, listing);
        AssetDatabase.DeleteAsset(OldVariantsPath);
        AssetDatabase.DeleteAsset(VariantsPath);
        AssetDatabase.CreateAsset(svc, VariantsPath);
        AssetDatabase.SaveAssets();
        foreach (string l in listing) Debug.Log("BuildBundles: variant " + l);

        var build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames = new[]
            {
                ShaderPath, RopeShaderPath, ComputePath, SimPath, MaterialPath, RopeMaterialPath, VariantsPath,
                "Assets/Textures/worm_albedo.png", "Assets/Textures/worm_gloss.png", "Assets/Textures/worm_normal.png",
            },
        };
        if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
        Directory.CreateDirectory(OutDir);
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(OutDir, new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new Exception("BuildAssetBundles returned no manifest");

        string file = Path.Combine(OutDir, BundleName);
        if (!File.Exists(file)) throw new Exception("bundle file missing: " + file);
        Debug.Log($"BuildBundles: OK {file} {new FileInfo(file).Length} bytes; assets: {string.Join(", ", build.assetNames)}");
        EditorApplication.Exit(0);
    }

    private static void ConfigureProject()
    {
        PlayerSettings.colorSpace = GameColorSpace;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Vulkan });

        // Keep every instancing and fog variant: a bundle has no scene to tell the stripper what the game will use.
        var gs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];
        var so = new SerializedObject(gs);
        SetInt(so, "m_InstancingStripping", 2);                                  // Keep All
        SetBool(so, "m_FogKeepLinear", true);
        SetBool(so, "m_FogKeepExp", true);
        SetBool(so, "m_FogKeepExp2", true);
        SetInt(so, "m_FogStripping", 1);                                         // Custom
        SetInt(so, "m_LightmapStripping", 1);                                    // Custom
        SetBool(so, "m_LightmapKeepPlain", true);
        SetBool(so, "m_LightmapKeepDirCombined", true);
        SetBool(so, "m_LightmapKeepDynamicPlain", true);
        SetBool(so, "m_LightmapKeepDynamicDirCombined", true);
        SetBool(so, "m_LightmapKeepShadowMask", true);
        SetBool(so, "m_LightmapKeepSubtractive", true);
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static void SetInt(SerializedObject so, string name, int v)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("BuildBundles: GraphicsSettings has no property " + name); return; }
        p.intValue = v;
    }

    private static void SetBool(SerializedObject so, string name, bool v)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("BuildBundles: GraphicsSettings has no property " + name); return; }
        p.boolValue = v;
    }

    private static void ImportTextures()
    {
        Configure("Assets/Textures/worm_albedo.png", TextureImporterType.Default, true);
        Configure("Assets/Textures/worm_gloss.png", TextureImporterType.Default, false);
        Configure("Assets/Textures/worm_normal.png", TextureImporterType.NormalMap, false);
    }

    private static void Configure(string path, TextureImporterType type, bool srgb)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp == null) throw new Exception("texture not found: " + path);
        imp.textureType = type;
        imp.sRGBTexture = srgb;
        imp.filterMode = FilterMode.Point;       // the creature textures of the game are Point
        imp.mipmapEnabled = true;
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
    }

    private static Shader LoadShader(string path)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (shader == null) throw new Exception("shader not found: " + path);
        if (ShaderUtil.ShaderHasError(shader))
        {
            foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
                Debug.LogError($"BuildBundles: shader {m.severity}: {m.message} ({m.file}:{m.line}) [{m.platform}]");
            throw new Exception(path + " has compilation errors");
        }
        Debug.Log($"BuildBundles: shader {shader.name} compiled, supported={shader.isSupported}");
        return shader;
    }

    private static Material MakeMaterial(Shader shader, string name)
    {
        var mat = new Material(shader) { name = name };
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/worm_albedo.png"));
        mat.SetTexture("_GlossTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/worm_gloss.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/worm_normal.png"));
        mat.enableInstancing = true;
        return mat;
    }

    private static void CheckCompute(ComputeShader cs, string[] kernels)
    {
        foreach (string k in kernels)
        {
            if (!cs.HasKernel(k)) throw new Exception($"{cs.name}: kernel {k} missing (compile error?)");
            Debug.Log($"BuildBundles: compute {cs.name} kernel {k} index {cs.FindKernel(k)}");
        }
    }

    private static void BuildVariants(ShaderVariantCollection svc, Shader shader, List<string> listing)
    {
        string[][] fogs = { new string[0], new[] { "FOG_LINEAR" }, new[] { "FOG_EXP" }, new[] { "FOG_EXP2" } };
        string[][] bases =
        {
            new[] { "DIRECTIONAL" },
            new[] { "DIRECTIONAL", "SHADOWS_SCREEN" },
            new[] { "DIRECTIONAL", "LIGHTPROBE_SH" },
            new[] { "DIRECTIONAL", "SHADOWS_SCREEN", "LIGHTPROBE_SH" },
            new[] { "DIRECTIONAL", "SHADOWS_SCREEN", "LIGHTPROBE_SH", "VERTEXLIGHT_ON" },
            new[] { "DIRECTIONAL", "LIGHTPROBE_SH", "VERTEXLIGHT_ON" },
        };
        string[][] adds = { new[] { "POINT" }, new[] { "SPOT" }, new[] { "POINT", "SHADOWS_CUBE" }, new[] { "SPOT", "SHADOWS_DEPTH" } };
        string[][] casters = { new string[0], new[] { "SHADOWS_DEPTH" }, new[] { "SHADOWS_CUBE" } };
        string[][] gbuffers =
        {
            new string[0], new[] { "UNITY_HDR_ON" }, new[] { "LIGHTPROBE_SH" }, new[] { "UNITY_HDR_ON", "LIGHTPROBE_SH" },
            new[] { "SHADOWS_SHADOWMASK", "UNITY_HDR_ON", "LIGHTPROBE_SH" },
        };

        int added = 0, rejected = 0;
        foreach (bool inst in new[] { true, false })
        {
            foreach (string[] g in gbuffers) Add(svc, shader, PassType.Deferred, g, new string[0], inst, listing, ref added, ref rejected);
            foreach (string[] fog in fogs)
            {
                foreach (string[] b in bases) Add(svc, shader, PassType.ForwardBase, b, fog, inst, listing, ref added, ref rejected);
                foreach (string[] a in adds) Add(svc, shader, PassType.ForwardAdd, a, fog, inst, listing, ref added, ref rejected);
            }
            foreach (string[] c in casters) Add(svc, shader, PassType.ShadowCaster, c, new string[0], inst, listing, ref added, ref rejected);
        }
        Debug.Log($"BuildBundles: ShaderVariantCollection {shader.name}: {added} variants added, {rejected} rejected or duplicated");
        if (added == 0) throw new Exception("empty ShaderVariantCollection for " + shader.name);
    }

    private static void Add(ShaderVariantCollection svc, Shader shader, PassType pass, string[] a, string[] b, bool instancing, List<string> listing, ref int added, ref int rejected)
    {
        var kw = new List<string>(a);
        kw.AddRange(b);
        if (instancing) kw.Add("INSTANCING_ON");
        string line = $"{shader.name} {pass} [{string.Join(" ", kw)}]";
        try
        {
            if (svc.Add(new ShaderVariantCollection.ShaderVariant(shader, pass, kw.ToArray()))) { added++; listing.Add(line); }
            else { rejected++; listing.Add("(duplicate) " + line); }
        }
        catch (ArgumentException)
        {
            rejected++;
            listing.Add("(rejected) " + line);
        }
    }
}
