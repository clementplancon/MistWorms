using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Smoke test of the built bundle in the editor (D3D11 on the dev machine): loads Build/mistworms_shaders, draws a few worms by procedural instancing
/// and writes Logs/render-smoke.png. Run: Unity.exe -batchmode -projectPath ... -executeMethod RenderSmoke.Run (without -nographics).
/// The tube mesh is a copy of GpuWormMesh in the mod (src/MistWorms/Dev/GpuWorms.cs).
/// </summary>
public static class RenderSmoke
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Inst { public Vector4 PosLen, Quat, Wave; }

    public static void Run()
    {
        try { Go(); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogError("RenderSmoke: FAILED " + e); EditorApplication.Exit(1); }
    }

    private static void Go()
    {
        var bundle = AssetBundle.LoadFromFile("Build/mistworms_shaders");
        var mat = new Material(bundle.LoadAsset<Material>("Assets/Materials/WormGPU.mat")) { enableInstancing = true };
        var cs = bundle.LoadAsset<ComputeShader>("Assets/Shaders/MWTest.compute");
        Debug.Log($"RenderSmoke: api={SystemInfo.graphicsDeviceType} compute={SystemInfo.supportsComputeShaders} shaderSupported={mat.shader.isSupported}");

        var cb = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 64, 4);
        cs.SetBuffer(0, "Result", cb);
        cs.Dispatch(0, 1, 1, 1);
        var r = new uint[64];
        cb.GetData(r);
        Debug.Log($"RenderSmoke: compute r[0]={r[0]} r[5]={r[5]} r[63]={r[63]} ok={r[0] == 7 && r[5] == 17 && r[63] == 133}");
        cb.Release();

        Mesh mesh = BuildMesh();
        const int n = 12;
        var inst = new Inst[n];
        for (int i = 0; i < n; i++)
        {
            Quaternion q = Quaternion.Euler(0f, 20f * i, 0f);
            inst[i].PosLen = new Vector4((i % 4) * 0.12f - 0.18f, 0f, (i / 4) * 0.2f - 0.2f, 1f);
            inst[i].Quat = new Vector4(q.x, q.y, q.z, q.w);
            inst[i].Wave = new Vector4(i * 0.13f, 0.4f + 0.05f * i, 1.4f, (i - 6) * 0.15f);
        }
        var buf = new GraphicsBuffer(GraphicsBuffer.Target.Structured, n, Marshal.SizeOf(typeof(Inst)));
        buf.SetData(inst);
        mat.SetBuffer("_Worms", buf);
        var args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
        args.SetData(new uint[] { mesh.GetIndexCount(0), n, 0, 0, 0 });

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
        ground.transform.position = new Vector3(0f, -0.001f, 0f);
        var light = new GameObject("Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var camGo = new GameObject("Cam");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.25f, 0.28f, 0.32f);
        cam.renderingPath = RenderingPath.DeferredShading;
        cam.allowHDR = true;
        cam.nearClipPlane = 0.01f;
        camGo.transform.position = new Vector3(0.1f, 0.4f, -0.7f);
        camGo.transform.LookAt(new Vector3(0f, 0f, 0f));
        var rt = new RenderTexture(1024, 768, 24);
        cam.targetTexture = rt;

        Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, new Bounds(Vector3.zero, Vector3.one * 4f), args, 0, null, ShadowCastingMode.On, true, 0);
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        Directory.CreateDirectory("Logs");
        File.WriteAllBytes("Logs/render-smoke.png", tex.EncodeToPNG());
        // How much of the picture is not the background: the worms were drawn.
        Color32[] px = tex.GetPixels32();
        Color32 bg = cam.backgroundColor;
        int diff = 0;
        foreach (Color32 c in px) if (Mathf.Abs(c.r - bg.r) + Mathf.Abs(c.g - bg.g) + Mathf.Abs(c.b - bg.b) > 30) diff++;
        Debug.Log($"RenderSmoke: OK picture written, {diff} pixels differ from the background");
    }

    private static Mesh BuildMesh()
    {
        const int sides = 8, rings = 20;
        const float len = 0.30f, rad = 0.009f, headRad = 0.0105f, tail = 0.12f, bulb0 = 0.70f, bulb1 = 0.88f;
        Func<float, float> prof = t =>
        {
            if (t < tail) { float k = (tail - t) / tail; return Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
            float bulb = headRad / rad;
            if (t < bulb0) return 1f;
            if (t < bulb1) { float k = (t - bulb0) / (bulb1 - bulb0); k = k * k * (3f - 2f * k); return 1f + (bulb - 1f) * k; }
            { float k = (t - bulb1) / (1f - bulb1); return bulb * Mathf.Sqrt(Mathf.Max(0f, 1f - k * k)); }
        };
        int stride = sides + 1;
        var v = new Vector3[stride * rings]; var nr = new Vector3[v.Length]; var tg = new Vector4[v.Length]; var uv = new Vector2[v.Length];
        for (int ring = 0; ring < rings; ring++)
        {
            float t = ring / (float)(rings - 1);
            float t0 = Mathf.Max(0f, t - 0.01f), t1 = Mathf.Min(1f, t + 0.01f);
            float dr = rad * (prof(t1) - prof(t0)) / (t1 - t0) / len;
            for (int s = 0; s < stride; s++)
            {
                float a = s / (float)sides * Mathf.PI * 2f; float c = Mathf.Cos(a), sn = Mathf.Sin(a);
                int i = ring * stride + s;
                v[i] = new Vector3(c, sn, 0) * rad * prof(t) + new Vector3(0, 0, t * len);
                nr[i] = new Vector3(c, sn, -dr).normalized; tg[i] = new Vector4(-sn, c, 0, 1); uv[i] = new Vector2(s / (float)sides, 1f - t);
            }
        }
        var idx = new int[(rings - 1) * sides * 6]; int k2 = 0;
        for (int ring = 0; ring < rings - 1; ring++)
            for (int s = 0; s < sides; s++)
            {
                int a = ring * stride + s, b = a + 1, c = a + stride, d = c + 1;
                idx[k2++] = a; idx[k2++] = b; idx[k2++] = c; idx[k2++] = b; idx[k2++] = d; idx[k2++] = c;
            }
        var m = new Mesh { vertices = v, normals = nr, tangents = tg, uv = uv, triangles = idx };
        m.bounds = new Bounds(new Vector3(0, 0, len * 0.5f), new Vector3(len, len * 0.6f, len * 1.1f));
        return m;
    }
}
