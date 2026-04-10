// CodeGamified.Celestial — Moon shader with phase, earthshine, crater normals
// MIT License
// Dual SubShader: HDRP (HLSL) + Built-in/URP fallback (CG).

Shader "CodeGamified/CelestialMoon"
{
    Properties
    {
        _MainTex ("Moon Surface", 2D) = "white" {}
        _NormalMap ("Normal Map (Craters)", 2D) = "bump" {}
        _SunDir ("Sun Direction (World)", Vector) = (1, 0, 0, 0)
        _EarthPos ("Earth Position (World)", Vector) = (0, 0, 0, 0)

        [Header(Illumination)]
        _DayBrightness ("Day Brightness", Range(0.5, 2)) = 1.2
        _TerminatorSharpness ("Terminator Sharpness", Range(1, 20)) = 6
        _AmbientLight ("Ambient Light", Range(0, 0.1)) = 0.01

        [Header(Normal Mapping)]
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0

        [Header(Earthshine)]
        _EarthshineIntensity ("Earthshine Intensity", Range(0, 1)) = 0.15
        _EarthshineColor ("Earthshine Color", Color) = (0.4, 0.5, 0.7, 1)
        _EarthRadius ("Earth Radius", Float) = 6.371

        [Header(Surface)]
        _Smoothness ("Smoothness", Range(0, 1)) = 0.05
        _SurfaceColor ("Surface Tint", Color) = (0.85, 0.85, 0.85, 1)
    }

    // ═══════════════════════════════════════════════════════════════
    // HDRP
    // ═══════════════════════════════════════════════════════════════
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="HDRenderPipeline" }
        LOD 200

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "CelestialCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 tangentOS : TANGENT; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 tangentWS : TEXCOORD2; float3 bitangentWS : TEXCOORD3; float3 positionWS : TEXCOORD4; };

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);

            float4 _SunDir, _EarthPos, _EarthshineColor, _SurfaceColor;
            float  _DayBrightness, _TerminatorSharpness, _AmbientLight, _NormalStrength;
            float  _EarthshineIntensity, _EarthRadius, _Smoothness;

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS  = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS  = TransformWorldToHClip(o.positionWS);
                o.normalWS    = TransformObjectToWorldNormal(i.normalOS);
                o.tangentWS   = TransformObjectToWorldDir(i.tangentOS.xyz);
                o.bitangentWS = cross(o.normalWS, o.tangentWS) * i.tangentOS.w;
                o.uv          = i.uv;
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float3 T = normalize(i.tangentWS);
                float3 B = normalize(i.bitangentWS);
                float3 N = normalize(i.normalWS);
                float3x3 TBN = float3x3(T, B, N);

                float3 pertN = normalize(mul(CelestialUnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength), TBN));
                float3 geoN   = normalize(i.normalWS);
                float3 sunDir = CELESTIAL_SUN_DIR(_SunDir);

                float sunFacing = dot(geoN, sunDir);
                float dayAmount = saturate((sunFacing * _TerminatorSharpness) * 0.5 + 0.5);

                float4 surf = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _SurfaceColor;
                float  diff = saturate(dot(pertN, sunDir) * 0.6 + 0.4);
                float3 lit  = surf.rgb * _DayBrightness * diff;

                // Earthshine
                float3 toEarth = normalize(_EarthPos.xyz - i.positionWS);
                float  eFace   = saturate(dot(geoN, toEarth));
                float  eSun    = dot(normalize(-_EarthPos.xyz), sunDir);
                float  eIllum  = saturate(eSun * 0.5 + 0.5);
                float  es      = eFace * eIllum * _EarthshineIntensity * (1.0 - dayAmount);
                float3 dark    = surf.rgb * _AmbientLight + _EarthshineColor.rgb * es;

                float3 final = lerp(dark, lit, dayAmount);

                // Subtle specular
                float3 vDir  = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 hDir  = normalize(sunDir + vDir);
                float  spec  = pow(saturate(dot(pertN, hDir)), 64.0) * _Smoothness * 0.1;
                final += spec * dayAmount;

                return float4(final, 1.0);
            }
            ENDHLSL
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Built-in / URP Fallback
    // ═══════════════════════════════════════════════════════════════
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "CelestialCommon.hlsl"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float4 tangent : TANGENT; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 nW : TEXCOORD1; float3 tW : TEXCOORD2; float3 bW : TEXCOORD3; float3 pW : TEXCOORD4; };

            sampler2D _MainTex, _NormalMap;
            float4 _SunDir, _EarthPos, _EarthshineColor, _SurfaceColor;
            float  _DayBrightness, _TerminatorSharpness, _AmbientLight, _NormalStrength;
            float  _EarthshineIntensity, _EarthRadius, _Smoothness;

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.nW = UnityObjectToWorldNormal(v.normal); o.tW = UnityObjectToWorldDir(v.tangent.xyz); o.bW = cross(o.nW, o.tW) * v.tangent.w; o.pW = mul(unity_ObjectToWorld, v.vertex).xyz; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                float3x3 TBN = float3x3(normalize(i.tW), normalize(i.bW), normalize(i.nW));
                float3 pertN = normalize(mul(CelestialUnpackNormal(tex2D(_NormalMap, i.uv), _NormalStrength), TBN));
                float3 geoN   = normalize(i.nW);
                float3 sunDir = CELESTIAL_SUN_DIR(_SunDir);

                float dayAmt = saturate((dot(geoN, sunDir) * _TerminatorSharpness) * 0.5 + 0.5);
                fixed4 surf  = tex2D(_MainTex, i.uv) * _SurfaceColor;
                float  diff  = saturate(dot(pertN, sunDir) * 0.6 + 0.4);
                float3 lit   = surf.rgb * _DayBrightness * diff;

                float3 toE   = normalize(_EarthPos.xyz - i.pW);
                float  eF    = saturate(dot(geoN, toE));
                float  eS    = dot(normalize(-_EarthPos.xyz), sunDir);
                float  eI    = saturate(eS * 0.5 + 0.5);
                float  es    = eF * eI * _EarthshineIntensity * (1.0 - dayAmt);
                float3 dark  = surf.rgb * _AmbientLight + _EarthshineColor.rgb * es;

                float3 final = lerp(dark, lit, dayAmt);
                float3 vDir  = normalize(_WorldSpaceCameraPos - i.pW);
                float3 hDir  = normalize(sunDir + vDir);
                final += pow(saturate(dot(pertN, hDir)), 64.0) * _Smoothness * 0.1 * dayAmt;

                return fixed4(final, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
