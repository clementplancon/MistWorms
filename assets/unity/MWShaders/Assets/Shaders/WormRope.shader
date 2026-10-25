// MistWorms/WormRope: the worms of a mass, drawn from the nodes of their simulated rope (WormSim.compute). One instance per worm; the instance reads its
// slot in _DrawList (a list per LOD, built by the host), the 8 nodes of the slot in _Nodes (local frame of the mass) and the origin of the mass. The vertex shader
// builds the tube along the Catmull-Rom spline of the 8 nodes (end tangents by reflection), with a rotation minimising frame carried from the tail so the section does
// not twist. The straight tube meshes (8x20, 6x10, 4x6; z = 0..0.30 m, radius profile and UVs of make_worm.py) only give the section and the parameter along the body.
// Built-in pipeline, Standard lighting (forward and deferred passes, shadow caster with addshadow), so fog, ambient and shadows follow the game. Linear colour space
// (the albedo texture is imported as sRGB, the gloss and the normal map as data). Tint, smoothness and rim are toned down from the spike so that the worms read as
// pale cave worms in full sun, like the Custom/Creature flipbook worm (vanilla smoothness 0.54 to 0.76), not white.
Shader "MistWorms/WormRope"
{
    Properties
    {
        _Color ("Tint", Color) = (0.80,0.77,0.72,1)
        _MainTex ("Albedo (head at v = 0)", 2D) = "white" {}
        _GlossTex ("Gloss (alpha = smoothness)", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal scale", Range(0,2)) = 1
        _Glossiness ("Smoothness scale", Range(0,1)) = 0.62
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
        #pragma instancing_options procedural:setup

        sampler2D _MainTex, _GlossTex, _BumpMap;
        fixed4 _Color, _RimColor;
        half _Glossiness, _BumpScale, _RimPower;
        float4 _TexScale;

        // 16 bytes per worm and 192 bytes per mass: the layouts of WormData and MassParams in WormSim.compute (GpuWormTypes.cs).
        struct WormData { uint mass; uint seed; float speed; float phase; };
        struct MassParams
        {
            float4 origin; float4 center; float4 heading; float4 shape; float4 motion; float4 sleeve; uint4 ranges; float4 gridRect; float4 skin; float4 side; float4 plan; float4 plan2;
        };

        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
        StructuredBuffer<float4> _Nodes;
        StructuredBuffer<uint> _DrawList;
        StructuredBuffer<WormData> _WormData;
        StructuredBuffer<MassParams> _MassData;
        #endif

        static uint g_slot = 0;
        static float3 g_origin = float3(0, 0, 0);

        static const float kLen = 0.30;
        static const int K = 8;

        void setup()
        {
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            g_slot = _DrawList[unity_InstanceID];
            g_origin = _MassData[_WormData[g_slot].mass].origin.xyz;
            // The vertices are built in world space.
            unity_ObjectToWorld = float4x4(1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1);
            unity_WorldToObject = unity_ObjectToWorld;
        #endif
        }

        float3 NodeAt(int i)
        {
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            return _Nodes[g_slot * K + i].xyz + g_origin;
        #else
            return float3(0, 0, i * 0.04);
        #endif
        }

        float ScaleAt(int i)
        {
        #ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
            return _Nodes[g_slot * K + i].w;
        #else
            return 1.0;
        #endif
        }

        void vert(inout appdata_full v)
        {
            float t = saturate(v.vertex.z / kLen);
            float s = t * (K - 1);
            int i1 = min((int)floor(s), K - 2);
            float u = s - i1;
            int i0 = max(i1 - 1, 0), i2 = i1 + 1, i3 = min(i1 + 2, K - 1);

            float3 n0 = NodeAt(i0), n1 = NodeAt(i1), n2 = NodeAt(i2), n3 = NodeAt(i3);
            // End tangents by reflection of the neighbour node: the spline leaves the first and the last node along their segment.
            if (i1 == 0) n0 = 2.0 * n1 - n2;
            if (i2 == K - 1) n3 = 2.0 * n2 - n1;

            // Catmull-Rom (uniform)
            float u2 = u * u, u3 = u2 * u;
            float3 c = 0.5 * ((2.0 * n1) + (-n0 + n2) * u + (2.0 * n0 - 5.0 * n1 + 4.0 * n2 - n3) * u2 + (-n0 + 3.0 * n1 - 3.0 * n2 + n3) * u3);
            float3 d = 0.5 * ((-n0 + n2) + 2.0 * (2.0 * n0 - 5.0 * n1 + 4.0 * n2 - n3) * u + 3.0 * (-n0 + 3.0 * n1 - 3.0 * n2 + n3) * u2);
            float3 tang = normalize(d + float3(0, 1e-7, 0));

            // Rotation minimising frame (double reflection, Wang et al. 2008), carried from the tail node by node: the section "up" starts along the world up at the
            // tail and does not twist along the body, whatever the bends (vertical worms on a capsule included).
            float3 xPrev = NodeAt(0);
            float3 tPrev = normalize(NodeAt(1) - xPrev + float3(0, 1e-7, 0));
            float3 refUp = abs(tPrev.y) > 0.98 ? float3(1, 0, 0) : float3(0, 1, 0);
            float3 frameA = normalize(refUp - tPrev * dot(refUp, tPrev));    // frame at node 0
            float3 frameAtI1 = frameA;
            float3 frameB = frameA;                                           // frame at node i1 + 1 once the loop has run
            [loop] for (int k = 1; k <= K - 1; k++)
            {
                if (k > i1 + 1) break;
                float3 xk = NodeAt(k);
                float3 tk = normalize(NodeAt(min(k + 1, K - 1)) - NodeAt(k - 1) + float3(0, 1e-7, 0));
                float3 v1 = xk - xPrev;
                float c1 = dot(v1, v1) + 1e-12;
                float3 rL = frameB - v1 * (2.0 / c1 * dot(v1, frameB));
                float3 tL = tPrev - v1 * (2.0 / c1 * dot(v1, tPrev));
                float3 v2 = tk - tL;
                float c2 = dot(v2, v2) + 1e-12;
                frameB = rL - v2 * (2.0 / c2 * dot(v2, rL));
                xPrev = xk;
                tPrev = tk;
                if (k == i1) frameAtI1 = frameB;
            }
            float3 upv = lerp(frameAtI1, frameB, u);
            upv = upv - tang * dot(upv, tang);
            upv = upv * rsqrt(max(dot(upv, upv), 1e-12));
            float3 right = cross(upv, tang);

            float sc = lerp(ScaleAt(i1), ScaleAt(i2), u);
            float3 p = c + (right * v.vertex.x + upv * v.vertex.y) * sc;
            float3 n = right * v.normal.x + upv * v.normal.y + tang * v.normal.z;
            float3 tn = right * v.tangent.x + upv * v.tangent.y + tang * v.tangent.z;
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
            o.Emission = _RimColor.rgb * rim * 0.015;
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback Off
}
