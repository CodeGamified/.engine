// ═══════════════════════════════════════════════════════════
//  TUI.Format — Duration & time-magnitude coloring
//  Python source: TUI.py § Format
// ═══════════════════════════════════════════════════════════
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Human-readable time formatting and gradient-colored durations.
    /// Python: fmt_duration() / time_color()
    /// </summary>
    public static class TUIFormat
    {
        /// <summary>
        /// Human-readable duration: 45s, 1m 04s, 5h 02m 16s, 2d 3h 15m.
        /// </summary>
        public static string Duration(float seconds)
        {
            int s = (int)seconds;
            if (s < 60) return $"{s}s";

            int m = s / 60; s %= 60;
            if (m < 60) return $"{m}m {s:D2}s";

            int h = m / 60; m %= 60;
            if (h < 24) return $"{h}h {m:D2}m {s:D2}s";

            int d = h / 24; h %= 24;
            return $"{d}d {h}h {m:D2}m";
        }

        /// <summary>
        /// Color by time magnitude using brand gradient.
        /// </summary>
        public static Color32 TimeColor(float seconds)
        {
            int s = (int)seconds;
            int idx;
            if      (s < 60)    idx = 0;
            else if (s < 3600)  idx = 1;
            else if (s < 86400) idx = 2;
            else                idx = 3;

            var grad = TUIConfig.Gradient;
            return grad[Mathf.Min(idx, grad.Length - 1)];
        }

        /// <summary>
        /// Duration string wrapped in time-magnitude color.
        /// </summary>
        public static string ColoredDuration(float seconds)
        {
            Color32 c = TimeColor(seconds);
            return TUIColors.Fg(c, Duration(seconds));
        }
    }
}
