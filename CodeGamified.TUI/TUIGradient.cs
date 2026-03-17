// ═══════════════════════════════════════════════════════════
//  TUI.Gradient — Color interpolation along stop arrays
//  Python source: TUI.py § Gradient
// ═══════════════════════════════════════════════════════════
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Multi-stop RGB gradient interpolation.
    /// Python: lerp_gradient(stops, t) / gradient_rgb(t)
    /// </summary>
    public static class TUIGradient
    {
        /// <summary>
        /// Interpolate an RGB gradient at position t ∈ [0,1].
        /// </summary>
        public static Color32 Lerp(Color32[] stops, float t)
        {
            if (stops == null || stops.Length == 0)
                return new Color32(255, 255, 255, 255);
            if (stops.Length == 1)
                return stops[0];

            t = Mathf.Clamp01(t);
            float seg = t * (stops.Length - 1);
            int i = Mathf.Min((int)seg, stops.Length - 2);
            float f = seg - i;

            Color32 a = stops[i], b = stops[i + 1];
            return new Color32(
                (byte)(a.r + (b.r - a.r) * f),
                (byte)(a.g + (b.g - a.g) * f),
                (byte)(a.b + (b.b - a.b) * f),
                255
            );
        }

        /// <summary>
        /// Sample the brand gradient at position t ∈ [0,1].
        /// Python: gradient_rgb(t)
        /// </summary>
        public static Color32 Sample(float t)
            => Lerp(TUIConfig.Gradient, t);

        /// <summary>
        /// Build a looped gradient for perimeter sweeps (appends first stop at end).
        /// Used by Box border rendering.
        /// </summary>
        public static Color32[] MakeLoop(Color32[] stops)
        {
            if (stops == null || stops.Length == 0)
                return stops;
            var loop = new Color32[stops.Length + 1];
            stops.CopyTo(loop, 0);
            loop[stops.Length] = stops[0];
            return loop;
        }

        /// <summary>
        /// Cyan → Magenta accent gradient at position t ∈ [0,1].
        /// Used by ASCII art borders, progress bars, and title effects.
        /// </summary>
        public static Color32 CyanMagenta(float t)
        {
            t = Mathf.Clamp01(t);
            return new Color32(
                (byte)(TUIColors.BrightCyan.r + (TUIColors.BrightMagenta.r - TUIColors.BrightCyan.r) * t),
                (byte)(TUIColors.BrightCyan.g + (TUIColors.BrightMagenta.g - TUIColors.BrightCyan.g) * t),
                (byte)(TUIColors.BrightCyan.b + (TUIColors.BrightMagenta.b - TUIColors.BrightCyan.b) * t),
                255);
        }
    }
}
