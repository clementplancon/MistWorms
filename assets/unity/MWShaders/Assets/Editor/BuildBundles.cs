using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds the AssetBundle "mistworms_shaders" (StandaloneWindows64): the WormGPU shader, its material, the MWTest compute shader and a
/// ShaderVariantCollection with the instancing variants. Entry point for the batchmode: -executeMethod BuildBundles.Build
/// Output: Build/mistworms_shaders (tools/build-shaders.ps1 copies it to assets/build). Exit code 1 on any failure.
/// </summary>
public static class BuildBundles
{
    private const string BundleName = "mistworms_shaders";
    private const string OutDir = "Build";
    private const string ShaderPath = "Assets/Shaders/WormGPU.shader";
    private const string ComputePath = "Assets/Shaders/MWTest.compute";
    private const string MaterialPath = "Assets/Materials/WormGPU.mat";
    private const string VariantsPath = "Assets/Materials/WormGPU.shadervariants";

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

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) throw new Exception("shader not found: " + ShaderPath);
        if (ShaderUtil.ShaderHasError(shader))
        {
            foreach (ShaderMessage m in ShaderUtil.GetShaderMessages(shader))
                Debug.LogError($"BuildBundles: shader {m.severity}: {m.message} ({m.file}:{m.line}) [{m.platform}]");
            throw new Exception("WormGPU has compilation errors");
        }
        Debug.Log("BuildBundles: shader compiled, supported=" + shader.isSupported);

        var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(ComputePath);
        if (compute == null) throw new Exception("compute shader not found: " + ComputePath);

        var mat = new Material(shader) { name = "WormGPU" };
        mat.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/worm_albedo.png"));
        mat.SetTexture("_GlossTex", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/worm_gloss.png"));
        mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/worm_normal.png"));
        mat.enableInstancing = true;
        AssetDatabase.DeleteAsset(MaterialPath);
        AssetDatabase.CreateAsset(mat, MaterialPath);

        var svc = BuildVariants(shader);
        AssetDatabase.DeleteAsset(VariantsPath);
        AssetDatabase.CreateAsset(svc, VariantsPath);
        AssetDatabase.SaveAssets();

        var build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames = new[]
            {
                ShaderPath, ComputePath, MaterialPath, VariantsPath,
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

    private static ShaderVariantCollection BuildVariants(Shader shader)
    {
        var svc = new ShaderVariantCollection();
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

        int added = 0, rejected = 0;
        string[][] gbuffers =
        {
            new string[0], new[] { "UNITY_HDR_ON" }, new[] { "LIGHTPROBE_SH" }, new[] { "UNITY_HDR_ON", "LIGHTPROBE_SH" },
            new[] { "SHADOWS_SHADOWMASK", "UNITY_HDR_ON", "LIGHTPROBE_SH" },
        };
        foreach (bool inst in new[] { true, false })
        {
            foreach (string[] g in gbuffers) Add(svc, shader, PassType.Deferred, g, new string[0], inst, ref added, ref rejected);
            foreach (string[] fog in fogs)
            {
                foreach (string[] b in bases) Add(svc, shader, PassType.ForwardBase, b, fog, inst, ref added, ref rejected);
                foreach (string[] a in adds) Add(svc, shader, PassType.ForwardAdd, a, fog, inst, ref added, ref rejected);
            }
            foreach (string[] c in casters) Add(svc, shader, PassType.ShadowCaster, c, new string[0], inst, ref added, ref rejected);
        }
        Debug.Log($"BuildBundles: ShaderVariantCollection {added} variants ({rejected} rejected)");
        if (added == 0) throw new Exception("empty ShaderVariantCollection");
        return svc;
    }

    private static void Add(ShaderVariantCollection svc, Shader shader, PassType pass, string[] a, string[] b, bool instancing, ref int added, ref int rejected)
    {
        var kw = new List<string>(a);
        kw.AddRange(b);
        if (instancing) kw.Add("INSTANCING_ON");
        try
        {
            if (svc.Add(new ShaderVariantCollection.ShaderVariant(shader, pass, kw.ToArray()))) added++;
        }
        catch (ArgumentException)
        {
            rejected++;
        }
    }
}
