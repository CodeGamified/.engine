// CodeGamified.TUI.Blur — Runtime auto-setup for glassmorphic blur
// MIT License
using UnityEngine;
using CodeGamified.Quality;

namespace CodeGamified.TUI.Blur
{
    /// <summary>
    /// Zero-config runtime manager for TUI blur.
    ///
    /// Auto-initializes via [RuntimeInitializeOnLoadMethod]:
    ///   1. Loads the UI blur material from Resources (created by editor auto-setup)
    ///   2. Assigns it to TerminalWindow.SharedBlurMaterial
    ///   3. Listens to QualityBridge — enables blur on High+Ultra, disables on lower tiers
    ///
    /// No manual wiring, no bootstrap code, no MonoBehaviour required.
    /// Just having CodeGamified.TUI.Blur in your project enables the feature.
    /// </summary>
    public static class TUIBlurManager
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoInitialize()
        {
            // Load pre-created material from Resources (editor auto-setup puts it there)
            var mat = Resources.Load<Material>("TUIUIBlur");
            if (mat == null)
            {
                // Fallback: try creating from shader (works if shader is in build)
                var shader = Shader.Find("CodeGamified/UIBackgroundBlur");
                if (shader == null) return;
                mat = new Material(shader) { name = "TUI UI Blur (runtime)", hideFlags = HideFlags.HideAndDontSave };
            }

            // Set black default so the shader never samples uninitialized texture
            Shader.SetGlobalTexture("_TUIBlurTexture", Texture2D.blackTexture);

            // Ensure brightness is set (existing material may have stale default)
            mat.SetFloat("_BlurBrightness", 0.5f);

            TerminalWindow.SharedBlurMaterial = mat;

            // Material is always assigned to panels so blur appears instantly
            // when quality switches to Ultra. Quality gating happens at the
            // render feature level (TUIBlurFeature.BlurEnabled).
            TerminalWindow.SetSharedBlurEnabled(true);

            // Gate the render feature based on quality tier
            QualityBridge.OnTierChanged += OnQualityChanged;
            OnQualityChanged(QualityBridge.CurrentTier);
        }

        static void OnQualityChanged(QualityTier tier)
        {
            bool enabled = (tier >= QualityTier.High);
            TUIBlurFeature.BlurEnabled = enabled;

            if (enabled)
            {
                if (tier == QualityTier.Ultra)
                {
                    // Ultra: full-resolution, 8 iterations for maximum blur quality
                    TUIBlurFeature.IterationsOverride = 8;
                    TUIBlurFeature.DownsampleOverride = 1;
                }
                else
                {
                    // High: half-resolution, 4 iterations (current default)
                    TUIBlurFeature.IterationsOverride = 4;
                    TUIBlurFeature.DownsampleOverride = 2;
                }
            }

            // When blur is off, reset global texture to black so panels
            // show dark bg instead of stale blur data
            if (!enabled)
                Shader.SetGlobalTexture("_TUIBlurTexture", Texture2D.blackTexture);
        }
    }
}
