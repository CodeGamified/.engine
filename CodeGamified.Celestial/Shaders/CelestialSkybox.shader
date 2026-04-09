// CodeGamified.Celestial — Dual-texture space skybox shader
// MIT License
// Dual SubShader: HDRP (HLSL) + Built-in/URP fallback (CG).

Shader "CodeGamified/CelestialSkybox"
{
    Properties
    {
        _BaseMap ("Base (Milky Way)", 2D) = "black" {}
        _EmissiveMap ("Emissive (Stars)", 2D) = "black" {}
        _BaseBrightness ("Base Brightness", Range(0, 10)) = 1.5
        _EmissiveBrightness ("Star Emissive Brightness", Range(0, 50)) = 8.0
        _EmissivePower ("Emissive Power (contrast)", Range(0.5, 4)) = 1.5
    }

    // ═══════════════════════════════════════════════════════════════
    // HDRP
    // ═══════════════════════════════════════════════════════════════
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "RenderPipeline"="HDRenderPipeline" }
        LOD 100
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_EmissiveMap); SAMPLER(sampler_EmissiveMap);
            float _BaseBrightness, _EmissiveBrightness, _EmissivePower;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; return o; }

            float4 frag(Varyings i) : SV_Target
            {
                float3 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb * _BaseBrightness;
                float3 em   = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_EmissiveMap, i.uv).rgb;
                float  lum  = dot(em, float3(0.299, 0.587, 0.114));
                float3 final = base + em * _EmissiveBrightness * pow(lum, _EmissivePower);
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
        Tags { "RenderType"="Background" "Queue"="Background" }
        LOD 100
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _BaseMap, _EmissiveMap;
            float _BaseBrightness, _EmissiveBrightness, _EmissivePower;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 base = tex2D(_BaseMap, i.uv).rgb * _BaseBrightness;
                float3 em   = tex2D(_EmissiveMap, i.uv).rgb;
                float  lum  = dot(em, float3(0.299, 0.587, 0.114));
                float3 final = base + em * _EmissiveBrightness * pow(lum, _EmissivePower);
                return fixed4(final, 1.0);
            }
            ENDCG
        }
    }
}
