// CodeGamified.Celestial — Single-layer atmosphere with sun + moon specular
// MIT License

Shader "CodeGamified/CelestialAtmosphere"
{
    Properties
    {
        _Color ("Atmosphere Color", Color) = (0.4, 0.7, 1.0, 0.1)
        _SunDir ("Sun Direction", Vector) = (1, 0, 0, 0)
        _MoonDir ("Moon Direction", Vector) = (0, 0, 1, 0)
        _MoonPhase ("Moon Phase (0=new, 1=full)", Range(0, 1)) = 0
        _FresnelPower ("Fresnel Power", Range(1, 10)) = 3
        _SunSpecularIntensity ("Sun Specular Intensity", Range(0, 2)) = 0.5
        _SunSpecularPower ("Sun Specular Power", Range(1, 128)) = 32
        _MoonSpecularIntensity ("Moon Specular Intensity", Range(0, 1)) = 0.15
        _DaySideBoost ("Day Side Brightness", Range(0, 2)) = 1.2
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "Atmosphere"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 worldN   : TEXCOORD0;
                float3 viewDir  : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            float4 _Color, _SunDir, _MoonDir;
            float  _MoonPhase, _FresnelPower;
            float  _SunSpecularIntensity, _SunSpecularPower, _MoonSpecularIntensity;
            float  _DaySideBoost;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldN   = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir  = normalize(_WorldSpaceCameraPos - o.worldPos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 normal  = normalize(-i.worldN);
                float3 viewDir = normalize(i.viewDir);
                float3 sunDir  = normalize(_SunDir.xyz);
                float3 moonDir = normalize(_MoonDir.xyz);
                float3 outerN  = normalize(i.worldN);

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                // Day/night
                float sunFacing = dot(outerN, sunDir);
                float dayAmt    = saturate(sunFacing * 4.0 + 0.5);
                float nightAmt  = 1.0 - dayAmt;

                // Sun specular
                float3 sHalf = normalize(sunDir + viewDir);
                float  sSpec = pow(saturate(dot(outerN, sHalf)), _SunSpecularPower) * _SunSpecularIntensity;
                sSpec *= saturate(sunFacing * 2.0 + 0.5);

                // Moon specular
                float3 mHalf = normalize(moonDir + viewDir);
                float  mSpec = pow(saturate(dot(outerN, mHalf)), _SunSpecularPower * 0.5) * _MoonSpecularIntensity;
                float  mFace = dot(outerN, moonDir);
                mSpec *= nightAmt * _MoonPhase * saturate(mFace + 0.3);

                float4 col = _Color;
                col.rgb *= lerp(0.15, _DaySideBoost, dayAmt);
                col.rgb += sSpec * float3(1.0, 0.95, 0.9);
                col.rgb += mSpec * float3(0.7, 0.8, 1.0);
                col.a   *= fresnel;
                col.a   *= lerp(0.4, 1.2, dayAmt);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    Fallback "Legacy Shaders/Transparent/Diffuse"
}
