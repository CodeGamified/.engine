// CodeGamified.TUI.Blur — Glassmorphic acrylic blur for TUI panels (URP only)
// MIT License
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace CodeGamified.TUI.Blur
{
    /// <summary>
    /// URP Renderer Feature — multi-pass Kawase blur → <c>_TUIBlurTexture</c>.
    ///
    /// Requires the Forward (Universal) renderer, NOT Renderer2D.
    /// Runs at <c>BeforeRenderingTransparents</c> (450): all opaque geometry
    /// is in the framebuffer, but transparent objects and ScreenSpaceCamera UI
    /// have not rendered yet. <c>SetGlobalTextureAfterPass</c> makes the blur
    /// available same-frame for UI panels.
    /// </summary>
    public class TUIBlurFeature : ScriptableRendererFeature
    {
        public static bool BlurEnabled { get; set; } = true;

        /// <summary>
        /// Runtime overrides for blur quality. When non-null, these take
        /// precedence over the serialized Settings values.
        /// </summary>
        public static int? IterationsOverride { get; set; }
        public static int? DownsampleOverride { get; set; }

        [System.Serializable]
        public class Settings
        {
            public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            [Range(1, 8)] public int iterations = 4;
            [Range(1, 4)] public int downsample = 2;
            public Material blurMaterial;
        }

        public Settings settings = new();
        TUIBlurPass _pass;

        public override void Create()
        {
            _pass = new TUIBlurPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!BlurEnabled || settings.blurMaterial == null)
                return;
            _pass.renderPassEvent = settings.renderPassEvent;
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }
    }

    class TUIBlurPass : ScriptableRenderPass, System.IDisposable
    {
        static readonly int OffsetID     = Shader.PropertyToID("_Offset");
        static readonly int SourceSizeID = Shader.PropertyToID("_SourceSize");
        static readonly int BlurTexID    = Shader.PropertyToID("_TUIBlurTexture");

        readonly TUIBlurFeature.Settings _settings;

        public TUIBlurPass(TUIBlurFeature.Settings settings)
        {
            _settings = settings;
        }

        class BlurIterationData
        {
            public Material material;
            public TextureHandle source;
            public float offset;
            public int width;
            public int height;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_settings.blurMaterial == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            var cameraData   = frameData.Get<UniversalCameraData>();

            var desc = cameraData.cameraTargetDescriptor;
            int iterations = TUIBlurFeature.IterationsOverride ?? _settings.iterations;
            int downsample = TUIBlurFeature.DownsampleOverride ?? _settings.downsample;
            int w = Mathf.Max(1, desc.width  / downsample);
            int h = Mathf.Max(1, desc.height / downsample);

            var texDesc = new TextureDesc(w, h)
            {
                colorFormat     = desc.graphicsFormat,
                depthBufferBits = DepthBits.None,
                msaaSamples     = MSAASamples.None,
                filterMode      = FilterMode.Bilinear,
                wrapMode        = TextureWrapMode.Clamp,
                clearBuffer     = false,
            };

            texDesc.name = "_TUIBlurPingA";
            var pingA = renderGraph.CreateTexture(texDesc);
            texDesc.name = "_TUIBlurPingB";
            var pingB = renderGraph.CreateTexture(texDesc);

            TextureHandle current = resourceData.activeColorTexture;
            TextureHandle dst = pingA;
            bool dstIsA = true;

            for (int i = 0; i < iterations; i++)
            {
                // Gentle offset ramp: 1.0, 1.0, 1.5, 1.5, 2.0, 2.0, 2.5, 2.5...
                // Each step used twice — avoids the faceted lens look of large offsets.
                float offset = 1.0f + (i / 2) * 0.5f;
                bool isLast = (i == iterations - 1);

                AddBlurIteration(renderGraph, current, dst, offset, w, h, isLast);

                current = dst;
                dstIsA = !dstIsA;
                dst = dstIsA ? pingA : pingB;
            }
        }

        void AddBlurIteration(RenderGraph rg, TextureHandle src, TextureHandle dst,
            float offset, int w, int h, bool isLast)
        {
            using (var builder = rg.AddRasterRenderPass<BlurIterationData>(
                "TUI Blur Iteration", out var data))
            {
                data.material = _settings.blurMaterial;
                data.source   = src;
                data.offset   = offset;
                data.width    = w;
                data.height   = h;

                builder.UseTexture(src, AccessFlags.Read);
                builder.SetRenderAttachment(dst, 0, AccessFlags.WriteAll);

                if (isLast)
                    builder.SetGlobalTextureAfterPass(dst, BlurTexID);

                builder.AllowPassCulling(false);

                builder.SetRenderFunc((BlurIterationData d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetViewport(new Rect(0, 0, d.width, d.height));
                    d.material.SetFloat(OffsetID, d.offset);
                    d.material.SetVector(SourceSizeID,
                        new Vector4(1f / d.width, 1f / d.height, d.width, d.height));
                    Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1f, 1f, 0f, 0f), d.material, 0);
                });
            }
        }

#pragma warning disable CS0618, CS0672
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) { }
#pragma warning restore CS0618, CS0672

        public void Dispose() { }
    }
}
