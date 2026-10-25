// MistWorms/WormGPU: a straight tube (see GpuWormMesh in the mod) bent in the vertex shader along a per-instance curve, with a crawl wave and
// peristalsis driven by _Time and a per-instance phase. Built-in pipeline, Standard lighting (forward and deferred passes: no noforwardadd / exclude_path), so fog, ambient, light probes, cascaded shadows
// and point lights follow the game. Instances come from a StructuredBuffer (procedural instancing, Graphics.DrawMeshInstancedIndirect), nothing
// is uploaded per draw call. The shadow caster pass (addshadow) runs the same vertex function.
Shader "MistWorms/WormGPU"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _GlossTex ("Gloss (alpha = smoothness)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal scale", Range(0,2)) = 1
        _Glossiness ("Smoothness scale", Range(0,1)) = 0.9
        _RimColor ("Slime rim", Color) = (0.55,0.5,0.4,1)
        _RimPower ("Rim power", Range(0.5,8)) = 3
        _TexScale ("Texture repeat (around, along)", Vector) = (1,1,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        Cull Back

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 4.5
        #pragma multi_compile_instancing
        #pragma instancing_options procedural:setup assumeuniformscaling

        sampler2D _MainTex, _GlossTex, _BumpMap;
        fixed4 _Color, _RimColor;
        half _Glossiness, _BumpScale, _RimPower;
        float4 _TexScale;

        // 48 bytes per worm; must match GpuWorms.Inst in the mod.
        struct WormInst
        {
            float4 posLen;   // xyz world position of the tail, w = length scale (1 = 0.30 m)
            float4 quat;     // orientation (xyzw), the tube axis is local +z
            float4 wave;     // x = phase (cycles), y = wave amplitude (0..1), z = wave frequency (Hz), w = bend (-1..1)
        };

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<WormInst> _Worms;
        #endif

        static float4 g_wave = float4(0, 0.5, 1.5, 0);

        // Where the instance stands: the matrices of the draw are replaced by the ones of the worm (rigid, uniform scale).
        void setup()
        {
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            WormInst w = _Worms[unity_InstanceID];
            float4 q = w.quat;
            float s = w.posLen.w;
            float x2 = q.x + q.x, y2 = q.y + q.y, z2 = q.z + q.z;
            float xx = q.x * x2, xy = q.x * y2, xz = q.x * z2;
            float yy = q.y * y2, yz = q.y * z2, zz = q.z * z2;
            float wx = q.w * x2, wy = q.w * y2, wz = q.w * z2;
            float3x3 r = float3x3(1 - (yy + zz), xy - wz,       xz + wy,
                                  xy + wz,       1 - (xx + zz), yz - wx,
                                  xz - wy,       yz + wx,       1 - (xx + yy));
            unity_ObjectToWorld = float4x4(r[0] * s, w.posLen.x,
                                           r[1] * s, w.posLen.y,
                                           r[2] * s, w.posLen.z,
                                           0, 0, 0, 1);
            float3x3 inv = transpose(r) / s;
            float3 t = mul(inv, -w.posLen.xyz);
            unity_WorldToObject = float4x4(inv[0], t.x,
                                           inv[1], t.y,
                                           inv[2], t.z,
                                           0, 0, 0, 1);
            g_wave = w.wave;
        #endif
        }

        static const float kLen = 0.30;

        // Centre line of the body in object space, t in 0..1 from tail to head.
        float3 Centre(float t, float phase, float amp, float freq, float bend)
        {
            float time = _Time.y;
            float env = sin(3.14159265 * t);
            float lateral = amp * 0.12 * kLen * env * sin(6.2831853 * (1.5 * t - phase - freq * time));
            float arch = bend * kLen * 0.5 * t * t;
            // The body shortens a little where it bends, the crest of the wave lifts it off the ground.
            float lift = amp * 0.02 * kLen * env * (0.5 + 0.5 * sin(6.2831853 * (1.5 * t - phase - freq * time)));
            return float3(lateral + arch, lift, kLen * t * (1.0 - 0.04 * amp));
        }

        void vert(inout appdata_full v)
        {
            float phase = g_wave.x, amp = g_wave.y, freq = g_wave.z, bend = g_wave.w;
            float t = saturate(v.vertex.z / kLen);
            float e = 0.02;
            float3 c = Centre(t, phase, amp, freq, bend);
            float3 ca = Centre(saturate(t - e), phase, amp, freq, bend);
            float3 cb = Centre(saturate(t + e), phase, amp, freq, bend);
            float3 tang = normalize(cb - ca);
            float3 right = normalize(cross(float3(0, 1, 0), tang));
            float3 up = cross(tang, right);

            // Peristalsis: a ring of contraction travelling head-ward, twice as fast as the crawl wave.
            float peri = 1.0 + 0.22 * sin(6.2831853 * (4.0 * t - 2.0 * freq * _Time.y - phase));

            float3 p = c + (right * v.vertex.x + up * v.vertex.y) * peri;
            float3 n = right * v.normal.x + up * v.normal.y + tang * v.normal.z;
            float3 tn = right * v.tangent.x + up * v.tangent.y + tang * v.tangent.z;
            v.vertex = float4(p, 1);
            v.normal = normalize(n);
            v.tangent = float4(normalize(tn), v.tangent.w);
        }

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.uv_MainTex * _TexScale.xy;
            fixed4 albedo = tex2D(_MainTex, uv);
            o.Albedo = albedo.rgb * _Color.rgb;
            o.Metallic = 0;
            o.Smoothness = tex2D(_GlossTex, uv).a * _Glossiness;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, uv), _BumpScale);
            // Wet look: a soft rim on top of the lighting.
            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
            o.Emission = _RimColor.rgb * rim * 0.04;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback Off
}
