// CodeGamified.Celestial — Per-layer atmosphere shell shader
// MIT License
// Transparent, front-face culled, fresnel rim, day/night, altitude fade.

Shader "CodeGamified/CelestialAtmosphereLayer"
{
    Properties
    {
        _Color ("Layer Color", Color) = (0.5, 0.7, 1.0, 0.04)
        _AlphaMultiplier ("Alpha Multiplier", Range(0, 1)) = 1.0
        // _CelestialSunDir set globally by AtmosphereSystem — not a per-material property
        _FresnelPower ("Fresnel Power", Range(1, 8)) = 3.0
        _FresnelIntensity ("Fresnel Intensity", Range(0, 2)) = 1.0
        _DaySideBoost ("Day Side Brightness", Range(0.5, 2)) = 1.3
        _NightSideMin ("Night Side Minimum", Range(0, 0.5)) = 0.1
        _TerminatorSharpness ("Terminator Sharpness", Range(0.5, 8)) = 2.0
        _AltitudeFade ("Altitude Fade", Range(0, 1)) = 0.5
        _InnerRadius ("Inner Radius (normalized)", Float) = 1.0
        _OuterRadius ("Outer Radius (normalized)", Float) = 1.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "IgnoreProjector"="True" }

        Pass
        {
            Name "AtmosphereLayer"
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
                float3 localPos : TEXCOORD3;
                UNITY_FOG_COORDS(4)
            };

            float4 _Color;
            float  _AlphaMultiplier;
            // Sun direction set globally by AtmosphereSystem via Shader.SetGlobalVector
            float4 _CelestialSunDir;
            float  _FresnelPower, _FresnelIntensity;
            float  _DaySideBoost, _NightSideMin, _TerminatorSharpness;
            float  _AltitudeFade, _InnerRadius, _OuterRadius;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.worldN   = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.localPos = v.vertex.xyz;
                o.viewDir  = normalize(_WorldSpaceCameraPos - o.worldPos);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 normal  = normalize(-i.worldN); // flip — front-face culled
                float3 viewDir = normalize(i.viewDir);
                float3 sunDir  = _CelestialSunDir.xyz; // pre-normalized by C#
                float3 outerN  = normalize(i.worldN);

                // Fresnel
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower) * _FresnelIntensity;
                float combined = saturate(fresnel + 0.15);

                // Day/night
                float dayAmt = saturate(dot(outerN, sunDir) * _TerminatorSharpness + 0.5);
                float bright = lerp(_NightSideMin, _DaySideBoost, dayAmt);

                // Altitude density fade
                float alt     = length(i.localPos);
                float thick   = _OuterRadius - _InnerRadius;
                float normAlt = saturate((alt - _InnerRadius) / max(thick, 0.001));
                float density = lerp(1.0, 1.0 - _AltitudeFade, normAlt);

                float4 col = _Color;
                col.rgb   *= bright;
                col.a     *= combined * density * _AlphaMultiplier;
                col.a     *= lerp(0.6, 1.0, dayAmt);
                col.a      = saturate(col.a);

                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }

    Fallback "Legacy Shaders/Transparent/Diffuse"
}
