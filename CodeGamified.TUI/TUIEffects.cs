// ═══════════════════════════════════════════════════════════
//  TUI.Effects — Scramble-reveal & gradient colorization
//  Python source: TUI.py § Effects
// ═══════════════════════════════════════════════════════════
using System.Text;
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Text visual effects — scramble decode and per-character gradient.
    /// Python: scramble_text() / gradient_colorize()
    /// </summary>
    public static class TUIEffects
    {
        /// <summary>
        /// Scramble-reveal: characters resolve left-to-right over time.
        /// Returns the current frame's text.
        /// </summary>
        /// <param name="target">Final resolved text.</param>
        /// <param name="age">Seconds since animation start.</param>
        /// <param name="charRate">Seconds per character reveal.</param>
        /// <param name="scrambleRate">Seconds per scramble cycle.</param>
        public static string ScrambleText(
            string target, float age,
            float charRate = 0.02f, float scrambleRate = 0.05f)
        {
            int length = target.Length;
            int resolved = (int)(age / charRate);
            if (resolved >= length) return target;

            var sb = new StringBuilder(length);
            // Resolved portion
            sb.Append(target, 0, resolved);

            // Scrambling portion
            int stepT = (int)(age / scrambleRate);
            string chars = TUIGlyphs.ScrambleChars;
            int nChars = chars.Length;

            for (int i = resolved; i < length; i++)
            {
                char c = target[i];
                if (c == ' ' || c == '\n' || c == '\t')
                {
                    sb.Append(c);
                }
                else
                {
                    int idx = (i + stepT * 13 + (int)c * 7) % nChars;
                    if (idx < 0) idx += nChars;
                    sb.Append(chars[idx]);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Per-character horizontal gradient coloring using rich-text tags.
        /// Whitespace passes through uncolored.
        /// </summary>
        public static string GradientColorize(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            int n = text.Length;
            var sb = new StringBuilder(n * 30);

            for (int i = 0; i < n; i++)
            {
                char ch = text[i];
                if (ch == ' ' || ch == '\t' || ch == '\n')
                {
                    sb.Append(ch);
                }
                else
                {
                    float t = n > 1 ? (float)i / (n - 1) : 0f;
                    Color32 c = TUIGradient.Sample(t);
                    sb.Append($"<color=#{c.r:X2}{c.g:X2}{c.b:X2}>{ch}</color>");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Check if scramble animation is complete for given target.
        /// </summary>
        public static bool IsResolved(string target, float age, float charRate = 0.02f)
            => age / charRate >= target.Length;

        // ── Additional effects (ported from BitNaughts TextAnimUtils) ──

        /// <summary>
        /// Blink text on/off at interval.
        /// </summary>
        public static string BlinkingText(string text, float age, float interval = 0.5f)
        {
            bool visible = (int)(age / interval) % 2 == 0;
            return visible ? text : new string(' ', text.Length);
        }

        /// <summary>
        /// Typewriter effect — reveals characters one at a time.
        /// </summary>
        public static string TypewriterText(string text, float age, float charDelay = 0.05f)
        {
            int visibleChars = (int)(age / charDelay);
            if (visibleChars >= text.Length) return text;
            return text.Substring(0, Mathf.Max(0, visibleChars));
        }

        /// <summary>
        /// Blinking block cursor for text input.
        /// </summary>
        public static string Cursor(float age, float blinkSpeed = 0.5f)
        {
            bool visible = (int)(age / blinkSpeed) % 2 == 0;
            return visible ? TUIGlyphs.BlockFull : " ";
        }
    }
}
