Shader "Hidden/CodeGamified/KawaseBlur"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "KawaseBlur"
            ZTest Always ZWrite Off Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Offset;
            float4 _SourceSize;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 ts = _SourceSize.xy;
                float2 uv = input.texcoord;
                float  o  = _Offset;
                float  h  = o * 0.5;

                // 13-tap dual Kawase: center(4) + corners(4) + cardinal(4) + outer(1)
                half4 c  = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0) * 4.0;

                // 4 diagonal taps at offset
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( o,  o) * ts, 0);
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( o, -o) * ts, 0);
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-o,  o) * ts, 0);
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-o, -o) * ts, 0);

                // 4 cardinal taps at half-offset for smoothness
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( h,  0) * ts, 0);
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2(-h,  0) * ts, 0);
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( 0,  h) * ts, 0);
                c += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + float2( 0, -h) * ts, 0);

                return saturate(c / 12.0);
            }
            ENDHLSL
        }
    }
}
