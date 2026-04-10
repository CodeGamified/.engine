// Procedural screen-space sun lens flare shader (URP compatible).
// Renders: bright core, soft glow, horizontal anamorphic streaks, starburst rays.
// All effects are procedural — no textures needed.
Shader "CodeGamified/SunFlare"
{
    Properties
    {
        _FlareColor ("Flare Color", Color) = (1, 0.95, 0.8, 1)
        _CoreIntensity ("Core Intensity", Range(0, 30)) = 8
        _GlowIntensity ("Glow Intensity", Range(0, 3)) = 0.8
        _GlowFalloff ("Glow Falloff", Range(1, 30)) = 12
        _AnamorphicStrength ("Anamorphic Strength", Range(0, 5)) = 1.5
        _AnamorphicWidth ("Anamorphic Width", Range(1, 100)) = 40
        _StarburstIntensity ("Starburst Intensity", Range(0, 3)) = 0.5
        _GhostIntensity ("Ghost Intensity", Range(0, 3)) = 0.8
        _GlintIntensity ("Glint Sweep Intensity", Range(0, 1)) = 0.3
        _Visibility ("Visibility", Range(0, 1)) = 1
        _ViewAngle ("View Angle", Float) = 0
        _Time2 ("Time", Float) = 0
        _OcclusionAmount ("Occlusion Amount", Range(0, 1)) = 0
        _OcclusionSoftness ("Occlusion Softness", Range(0.1, 2)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "SunFlare"
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _FlareColor;
            float _CoreIntensity;
            float _GlowIntensity;
            float _GlowFalloff;
            float _AnamorphicStrength;
            float _AnamorphicWidth;
            float _StarburstIntensity;
            float _GhostIntensity;
            float _GlintIntensity;
            float _Visibility;
            float _ViewAngle;
            float _Time2;
            float _OcclusionAmount;
            float _OcclusionSoftness;

            // Sweeping soft band — rotated, positioned, with bell-curve profile
            float glintBand(float2 uv, float sweep, float halfWidth, float angle)
            {
                float ca = cos(angle), sa = sin(angle);
                float2 ruv = float2(uv.x * ca + uv.y * sa, -uv.x * sa + uv.y * ca);
                float d = abs(ruv.x - sweep) / max(halfWidth, 0.001);
                // Bell curve: tight peak, soft tails
                return exp(-d * d * 4.0) * exp(-ruv.y * ruv.y * 0.5);
            }

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv - 0.5;
                float dist = length(uv);

                // Core — tiny intense pinpoint
                float core = exp(-dist * dist * 800.0) * _CoreIntensity;

                // Inner glow — tight, not blobby
                float glow = pow(max(0, 1.0 - dist * 4.0), 3.0) * _GlowIntensity;

                // Diffraction spikes — sharp 6-pointed star with long reach
                float angle = atan2(uv.y, uv.x) + _ViewAngle;
                float spike6 = pow(abs(cos(angle * 3.0)), 200.0);    // very sharp
                float spike4 = pow(abs(cos(angle * 2.0 + 0.4)), 120.0); // secondary 4-point offset
                float spikeFalloff = 1.0 / (1.0 + dist * 6.0);      // long reach, 1/r falloff
                float spikes = (spike6 + spike4 * 0.4) * spikeFalloff * _StarburstIntensity;

                // Anamorphic horizontal streak — thin and sharp
                float hStreak = exp(-uv.y * uv.y * _AnamorphicWidth * _AnamorphicWidth * 4.0)
                              * (1.0 / (1.0 + abs(uv.x) * 2.0))
                              * _AnamorphicStrength;

                // Chromatic ring — subtle rainbow ring at mid-distance
                float ringDist = abs(dist - 0.12);
                float ring = exp(-ringDist * ringDist * 2000.0) * 0.3;
                float3 ringColor = float3(
                    exp(-ringDist * ringDist * 1500.0),
                    exp(-(ringDist - 0.005) * (ringDist - 0.005) * 1500.0),
                    exp(-(ringDist - 0.01) * (ringDist - 0.01) * 1500.0)) * 0.2;

                // Ghost flares — small sharp dots
                float ghosts = 0.0;
                for (int g = 1; g <= 3; g++)
                {
                    float2 gc = uv + float2(0.15 * g, 0.0);
                    float gd = length(gc);
                    ghosts += exp(-gd * gd * 3000.0) * (0.3 / g);
                }
                ghosts *= _GhostIntensity;

                // Sweeping glint bands — animated, at different angles and speeds
                float t = _Time2;
                float sw1 = frac(t * 0.042) * 1.8 - 0.4;  // slow sweep right
                float sw2 = frac(t * 0.047) * -1.8 + 1.4;  // slow sweep left
                float sw3 = frac(t * 0.071) * 1.8 - 0.4;  // faster sweep right
                float sw4 = frac(t * 0.063) * -1.8 + 1.4;  // faster sweep left

                float osc1 = 0.02 + 0.02 * sin(t * 0.56);  // oscillating intensity
                float osc2 = 0.015 + 0.015 * sin(t * 0.43);
                float osc3 = 0.02 + 0.02 * sin(t * 0.38);
                float osc4 = 0.01 + 0.015 * sin(t * 0.51);

                float glints = 0.0;
                glints += glintBand(uv, sw1, 0.06, -0.44) * osc1;
                glints += glintBand(uv, sw2, 0.04, -0.44) * osc2;
                glints += glintBand(uv, sw3, 0.09, -0.30) * osc3;
                glints += glintBand(uv, sw4, 0.05, -0.30) * osc4;
                glints *= _GlintIntensity;

                float3 color = _FlareColor.rgb * (core + glow + spikes + hStreak + ghosts + glints)
                             + ringColor;

                float total = core + glow + spikes + hStreak + ghosts + ring + glints;

                // Circular soft edge — extended for spikes
                float circleFade = 1.0 - smoothstep(0.45, 0.5, dist);
                total *= circleFade;
                color *= circleFade;

                total *= (1.0 - _OcclusionAmount) * _Visibility;
                color *= (1.0 - _OcclusionAmount) * _Visibility;

                clip(circleFade - 0.001);

                return half4(color, total);
            }
            ENDHLSL
        }
    }

    // Built-in fallback
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            float4 _FlareColor;
            float _CoreIntensity, _GlowIntensity, _GlowFalloff;
            float _AnamorphicStrength, _AnamorphicWidth;
            float _StarburstIntensity, _GhostIntensity;
            float _Visibility, _ViewAngle;
            float _OcclusionAmount;

            v2f vert(appdata v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.uv; return o; }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;
                float dist = length(uv);
                float core = exp(-dist * dist * 200.0) * _CoreIntensity;
                float glow = exp(-dist * _GlowFalloff) * _GlowIntensity;
                float streak = exp(-abs(uv.y * uv.y) * _AnamorphicWidth * _AnamorphicWidth) * exp(-abs(uv.x) * 3.0) * _AnamorphicStrength;
                float angle = atan2(uv.y, uv.x) + _ViewAngle;
                float rays = pow(abs(cos(angle * 3.0)), 20.0) * exp(-dist * 8.0) * _StarburstIntensity;
                float total = (core + glow + streak + rays) * _Visibility * (1.0 - _OcclusionAmount);
                float circleFade = 1.0 - smoothstep(0.4, 0.5, dist);
                total *= circleFade;
                clip(circleFade - 0.001);
                return fixed4(_FlareColor.rgb * total, total);
            }
            ENDCG
        }
    }
}
