// CodeGamified.Celestial — Day/Night planet shader
// MIT License
// Calculates lighting from sun direction without Unity's lighting system.
// Dual SubShader: HDRP (HLSL) + Built-in/URP fallback (CG).

Shader "CodeGamified/CelestialDayNight"
{
    Properties
    {
        _DayTex ("Day Texture", 2D) = "white" {}
        _NightTex ("Night Texture", 2D) = "black" {}
        _NormalMap ("Normal Map", 2D) = "bump" {}
        _SpecularMap ("Specular Map", 2D) = "black" {}
        _SunDir ("Sun Direction", Vector) = (1, 0, 0, 0)
        _TerminatorSharpness ("Terminator Sharpness", Range(1, 20)) = 8
        _NightBrightness ("Night Brightness", Range(0, 2)) = 1.0
        _DayBrightness ("Day Brightness", Range(0.5, 2)) = 1.3
        _AmbientLight ("Ambient Light", Range(0, 0.3)) = 0.05
        _NormalStrength ("Normal Strength", Range(0, 2)) = 1.0
        _SpecularIntensity ("Specular Intensity", Range(0, 2)) = 0.35
        _SpecularPower ("Specular Power", Range(1, 128)) = 6
        _OceanSpecularColor ("Ocean Specular Color", Color) = (1, 1, 1, 1)
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 5
        _FresnelIntensity ("Fresnel Intensity", Range(0, 1)) = 0.3
        [Toggle] _InvertSpecularMap ("Invert Specular Map", Float) = 0

        [Header(Moonlight)]
        _MoonDir ("Moon Direction", Vector) = (0, 0, 1, 0)
        _MoonPhase ("Moon Phase (0=new, 1=full)", Range(0, 1)) = 0
        _MoonlightIntensity ("Moonlight Intensity", Range(0, 0.5)) = 0.15
        _MoonlightColor ("Moonlight Color", Color) = (0.7, 0.8, 1.0, 1.0)
    }

    // ═══════════════════════════════════════════════════════════════
    // HDRP SubShader
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

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "CelestialCommon.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 positionWS  : TEXCOORD4;
            };

            TEXTURE2D(_DayTex);       SAMPLER(sampler_DayTex);
            TEXTURE2D(_NightTex);     SAMPLER(sampler_NightTex);
            TEXTURE2D(_NormalMap);    SAMPLER(sampler_NormalMap);
            TEXTURE2D(_SpecularMap);  SAMPLER(sampler_SpecularMap);

            float4 _SunDir;
            float  _TerminatorSharpness;
            float  _NightBrightness;
            float  _DayBrightness;
            float  _AmbientLight;
            float  _NormalStrength;
            float  _SpecularIntensity;
            float  _SpecularPower;
            float4 _OceanSpecularColor;
            float  _FresnelPower;
            float  _FresnelIntensity;
            float  _InvertSpecularMap;
            float4 _MoonDir;
            float  _MoonPhase;
            float  _MoonlightIntensity;
            float4 _MoonlightColor;

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

                float3 normalTS = CelestialUnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _NormalStrength);
                float3 normal   = normalize(mul(normalTS, TBN));
                float3 geoN     = normalize(i.normalWS);
                float3 sunDir   = CELESTIAL_SUN_DIR(_SunDir);
                float3 viewDir  = normalize(_WorldSpaceCameraPos - i.positionWS);

                // Terminator
                float sunFacing = dot(geoN, sunDir);
                float dayAmount = saturate((sunFacing * _TerminatorSharpness) * 0.5 + 0.5);

                // Textures
                float4 dayCol   = SAMPLE_TEXTURE2D(_DayTex, sampler_DayTex, i.uv);
                float4 nightCol = SAMPLE_TEXTURE2D(_NightTex, sampler_NightTex, i.uv);
                float  specMask = SAMPLE_TEXTURE2D(_SpecularMap, sampler_SpecularMap, i.uv).r;
                specMask = _InvertSpecularMap > 0.5 ? 1.0 - specMask : specMask;

                dayCol.rgb   *= _DayBrightness;
                nightCol.rgb *= _NightBrightness;

                // Diffuse detail from normal map
                float NdotL        = dot(normal, sunDir);
                float dayLighting  = saturate(NdotL * 0.5 + 0.5);
                float normalDetail = saturate(dot(normal, sunDir) * 0.3 + 0.7);
                dayCol.rgb *= lerp(0.5, 1.0, dayLighting) * normalDetail;

                // Blinn-Phong specular
                float3 halfDir   = normalize(sunDir + viewDir);
                float  NdotH     = saturate(dot(geoN, halfDir));
                float  tightPow  = _SpecularPower + 40.0;
                float  spec      = pow(NdotH, tightPow) * _SpecularIntensity * 0.25 * specMask;
                spec *= saturate(sunFacing * 3.0);

                // Fresnel rim
                float  fresnel     = pow(1.0 - saturate(dot(geoN, viewDir)), _FresnelPower);
                float3 fresnelCol  = float3(0.4, 0.6, 1.0) * fresnel * _FresnelIntensity;
                dayCol.rgb += fresnelCol * dayAmount;

                // Blend day/night
                float3 final = lerp(nightCol.rgb, dayCol.rgb, dayAmount);
                final += clamp(spec * _OceanSpecularColor.rgb, 0.0, 0.3);
                final += _AmbientLight * dayCol.rgb * (1.0 - dayAmount);

                // Moonlight
                float3 moonDir = CELESTIAL_SUN_DIR(_MoonDir);
                float  moonFacing = dot(geoN, moonDir);
                float  nightAmt   = 1.0 - dayAmount;
                float  moonIllum  = saturate(moonFacing);
                float  moonContrib = moonIllum * _MoonPhase * _MoonlightIntensity * nightAmt * 3.0;
                final += dayCol.rgb * _MoonlightColor.rgb * moonContrib;

                // Moon ocean specular
                float3 moonHalf = normalize(moonDir + viewDir);
                float  mNdotH   = saturate(dot(geoN, moonHalf));
                float  moonSpec = pow(mNdotH, _SpecularPower * 3.0 + 80.0) * 0.1;
                moonSpec *= specMask * nightAmt * _MoonPhase * moonIllum;
                final += moonSpec * _MoonlightColor.rgb;

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

            struct appdata
            {
                float4 vertex  : POSITION;
                float3 normal  : NORMAL;
                float4 tangent : TANGENT;
                float2 uv      : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos       : SV_POSITION;
                float2 uv        : TEXCOORD0;
                float3 normalW   : TEXCOORD1;
                float3 tangentW  : TEXCOORD2;
                float3 binormalW : TEXCOORD3;
                float3 posW      : TEXCOORD4;
            };

            sampler2D _DayTex;
            sampler2D _NightTex;
            sampler2D _NormalMap;
            sampler2D _SpecularMap;
            float4 _SunDir;
            float  _TerminatorSharpness, _NightBrightness, _DayBrightness, _AmbientLight;
            float  _NormalStrength, _SpecularIntensity, _SpecularPower;
            float4 _OceanSpecularColor;
            float  _FresnelPower, _FresnelIntensity, _InvertSpecularMap;
            float4 _MoonDir, _MoonlightColor;
            float  _MoonPhase, _MoonlightIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.uv       = v.uv;
                o.normalW  = UnityObjectToWorldNormal(v.normal);
                o.tangentW = UnityObjectToWorldDir(v.tangent.xyz);
                o.binormalW = cross(o.normalW, o.tangentW) * v.tangent.w;
                o.posW     = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 T = normalize(i.tangentW);
                float3 B = normalize(i.binormalW);
                float3 N = normalize(i.normalW);
                float3x3 TBN = float3x3(T, B, N);

                float3 nTS   = CelestialUnpackNormal(tex2D(_NormalMap, i.uv), _NormalStrength);
                float3 normal = normalize(mul(nTS, TBN));
                float3 geoN   = normalize(i.normalW);
                float3 sunDir = CELESTIAL_SUN_DIR(_SunDir);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.posW);

                float sunFacing = dot(geoN, sunDir);
                float dayAmount = saturate((sunFacing * _TerminatorSharpness) * 0.5 + 0.5);

                fixed4 dayCol   = tex2D(_DayTex, i.uv) * _DayBrightness;
                fixed4 nightCol = tex2D(_NightTex, i.uv) * _NightBrightness;
                float  specMask = tex2D(_SpecularMap, i.uv).r;
                specMask = _InvertSpecularMap > 0.5 ? 1.0 - specMask : specMask;

                float NdotL = dot(normal, sunDir);
                float dayL  = saturate(NdotL * 0.5 + 0.5);
                float nDet  = saturate(dot(normal, sunDir) * 0.3 + 0.7);
                dayCol.rgb *= lerp(0.5, 1.0, dayL) * nDet;

                float3 halfDir = normalize(sunDir + viewDir);
                float  NdotH   = saturate(dot(geoN, halfDir));
                float  spec    = pow(NdotH, _SpecularPower + 40.0) * _SpecularIntensity * 0.25 * specMask;
                spec *= saturate(sunFacing * 3.0);

                float  fresnel = pow(1.0 - saturate(dot(geoN, viewDir)), _FresnelPower);
                dayCol.rgb += float3(0.4, 0.6, 1.0) * fresnel * _FresnelIntensity * dayAmount;

                float3 final = lerp(nightCol.rgb, dayCol.rgb, dayAmount);
                final += clamp(spec * _OceanSpecularColor.rgb, 0.0, 0.3);
                final += _AmbientLight * dayCol.rgb * (1.0 - dayAmount);

                // Moonlight
                float3 moonDir = CELESTIAL_SUN_DIR(_MoonDir);
                float  nightAmt = 1.0 - dayAmount;
                float  mFace = saturate(dot(geoN, moonDir));
                float  mC    = mFace * _MoonPhase * _MoonlightIntensity * nightAmt * 3.0;
                final += dayCol.rgb * _MoonlightColor.rgb * mC;

                float3 mHalf = normalize(moonDir + viewDir);
                float  mNdH  = saturate(dot(geoN, mHalf));
                float  mSpec = pow(mNdH, _SpecularPower * 3.0 + 80.0) * 0.1;
                mSpec *= specMask * nightAmt * _MoonPhase * mFace;
                final += mSpec * _MoonlightColor.rgb;

                return fixed4(final, 1.0);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Texture"
}
