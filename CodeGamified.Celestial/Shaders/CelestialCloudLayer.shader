// CodeGamified.Celestial — Cloud layer with day/night lighting
// MIT License
// Dual SubShader: HDRP (HLSL) + Built-in/URP fallback (CG).

Shader "CodeGamified/CelestialCloudLayer"
{
    Properties
    {
        _MainTex ("Cloud Texture", 2D) = "white" {}
        _SunDir ("Sun Direction", Vector) = (1, 0, 0, 0)
        _CloudOpacity ("Cloud Opacity", Range(0, 1)) = 0.6
        _DayBrightness ("Day Brightness", Range(0, 2)) = 1.0
        _NightBrightness ("Night Brightness", Range(0, 0.5)) = 0.2
        _TerminatorSharpness ("Terminator Sharpness", Range(1, 20)) = 6
    }

    // ═══════════════════════════════════════════════════════════════
    // HDRP
    // ═══════════════════════════════════════════════════════════════
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="HDRenderPipeline" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode"="ForwardOnly" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _SunDir;
            float  _CloudOpacity, _DayBrightness, _NightBrightness, _TerminatorSharpness;

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv         = i.uv;
                o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float4 cloud  = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float3 sunDir = normalize(_SunDir.xyz);
                float3 nWS    = normalize(i.normalWS);

                float dayF   = smoothstep(0.0, 1.0, saturate(dot(nWS, sunDir) * _TerminatorSharpness * 0.5 + 0.5));
                float bright = lerp(_NightBrightness, _DayBrightness, dayF);
                float alpha  = dot(cloud.rgb, float3(0.299, 0.587, 0.114)) * _CloudOpacity;

                return float4(cloud.rgb * bright, alpha);
            }
            ENDHLSL
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Built-in / URP Fallback
    // ═══════════════════════════════════════════════════════════════
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 nWS : TEXCOORD1; };

            sampler2D _MainTex;
            float4 _MainTex_ST, _SunDir;
            float  _CloudOpacity, _DayBrightness, _NightBrightness, _TerminatorSharpness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = TRANSFORM_TEX(v.uv, _MainTex);
                o.nWS = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 cloud = tex2D(_MainTex, i.uv);
                float  dayF  = smoothstep(0.0, 1.0, saturate(dot(normalize(i.nWS), normalize(_SunDir.xyz)) * _TerminatorSharpness * 0.5 + 0.5));
                float  bright = lerp(_NightBrightness, _DayBrightness, dayF);
                float  alpha  = dot(cloud.rgb, float3(0.299, 0.587, 0.114)) * _CloudOpacity;
                return float4(cloud.rgb * bright, alpha);
            }
            ENDCG
        }
    }

    Fallback "Transparent/Diffuse"
}
