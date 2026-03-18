Shader "CodeGamified/UIBackgroundBlur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurBrightness ("Blur Brightness", Range(0, 2)) = 1.0

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            HLSLPROGRAM
            #pragma vertex UIVert
            #pragma fragment UIFrag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_TUIBlurTexture);
            SAMPLER(sampler_TUIBlurTexture);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _BlurBrightness;
                float4 _TextureSampleAdd;
                float4 _ClipRect;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS    : SV_POSITION;
                float4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPos     : TEXCOORD2;
            };

            Varyings UIVert(Attributes input)
            {
                Varyings output;
                output.worldPosition = input.positionOS;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos  = ComputeScreenPos(output.positionCS);
                output.uv    = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                return output;
            }

            float UnityGet2DClipping(float2 position, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, position.xy) * step(position.xy, clipRect.zw);
                return inside.x * inside.y;
            }

            half4 UIFrag(Varyings input) : SV_Target
            {
                // Image sprite mask (supports 9-slice rounded corners, etc.)
                half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) + _TextureSampleAdd;

                // Sample blurred scene at screen position
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                half4 blur = SAMPLE_TEXTURE2D(_TUIBlurTexture, sampler_TUIBlurTexture, screenUV);

                // Glassmorphic compositing: mix blurred scene with tint color.
                // input.color.a controls the tint-vs-blur ratio, NOT transparency.
                // Panel is fully opaque (within mask) to prevent framebuffer bleed.
                half3 frosted = lerp(blur.rgb * _BlurBrightness, input.color.rgb, input.color.a);
                half alpha = mainTex.a;

                // Premultiplied alpha (matches Blend One OneMinusSrcAlpha)
                half4 color = half4(frosted * alpha, alpha);

                #ifdef UNITY_UI_CLIP_RECT
                float clipFactor = UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
                color *= clipFactor;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDHLSL
        }
    }
}
