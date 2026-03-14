// ═══════════════════════════════════════════════════════════
//  TUI.Easing — Interpolation curves
//  Python source: TUI.py § Easing
// ═══════════════════════════════════════════════════════════
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Easing functions for TUI animations.
    /// Python: ease(t) → C#: TUIEasing.Smoothstep(t)
    /// </summary>
    public static class TUIEasing
    {
        /// <summary>
        /// Smoothstep ease-in-out, clamped to [0,1].
        /// 3t² − 2t³
        /// </summary>
        public static float Smoothstep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Smoother Perlin variant (6t⁵ − 15t⁴ + 10t³).
        /// </summary>
        public static float Smootherstep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }
    }
}
