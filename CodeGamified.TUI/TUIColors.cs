// ═══════════════════════════════════════════════════════════
//  TUI.Colors — Color palette & rich-text helpers
//  Python source: TUI.py § Colors
// ═══════════════════════════════════════════════════════════
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// ANSI-equivalent color constants as Color32 + rich-text helpers.
    /// Python: class C / fg(r,g,b) → C#: TUIColors.Fg(r,g,b)
    /// </summary>
    public static class TUIColors
    {
        // ── Named palette (matches Python class C) ──────────────
        public static readonly Color32 White   = new(255, 255, 255, 255);
        public static readonly Color32 Cyan    = new(0,   255, 255, 255);
        public static readonly Color32 Green   = new(0,   255, 0,   255);
        public static readonly Color32 Yellow  = new(255, 255, 0,   255);
        public static readonly Color32 Red     = new(255, 0,   0,   255);
        public static readonly Color32 Magenta = new(255, 0,   255, 255);

        // Bright variants (ANSI 90-97 range)
        public static readonly Color32 BrightWhite   = new(255, 255, 255, 255);
        public static readonly Color32 BrightCyan    = new(0,   255, 255, 255);
        public static readonly Color32 BrightGreen   = new(0,   255, 0,   255);
        public static readonly Color32 BrightYellow  = new(255, 255, 0,   255);
        public static readonly Color32 BrightMagenta = new(255, 0,   255, 255);

        // Dim (alpha-based dimming for UI)
        public static readonly Color32 Dim = new(180, 180, 180, 160);

        // ── Rich-text helpers ───────────────────────────────────

        /// <summary>Wrap text in TMP rich-text color tag. Python: fg(r,g,b) + text</summary>
        public static string Fg(byte r, byte g, byte b, string text)
            => $"<color=#{r:X2}{g:X2}{b:X2}>{text}</color>";

        /// <summary>Wrap text in TMP rich-text color tag using Color32.</summary>
        public static string Fg(Color32 c, string text)
            => $"<color=#{c.r:X2}{c.g:X2}{c.b:X2}>{text}</color>";

        /// <summary>Opening color tag only (for streaming char-by-char).</summary>
        public static string FgOpen(byte r, byte g, byte b)
            => $"<color=#{r:X2}{g:X2}{b:X2}>";

        /// <summary>Close color tag.</summary>
        public const string FgClose = "</color>";

        /// <summary>Bold rich-text wrapper.</summary>
        public static string Bold(string text) => $"<b>{text}</b>";

        /// <summary>Dim text (lower alpha).</summary>
        public static string Dimmed(string text)
            => $"<color=#{Dim.r:X2}{Dim.g:X2}{Dim.b:X2}{Dim.a:X2}>{text}</color>";
    }
}
