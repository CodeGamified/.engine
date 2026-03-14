// ═══════════════════════════════════════════════════════════
//  TUI.Constants — Shared layout constants
//  Replaces BitNaughts TerminalStyle ScriptableObject statics
// ═══════════════════════════════════════════════════════════

namespace CodeGamified.TUI
{
    /// <summary>
    /// Shared character-layout constants for terminal UI.
    /// Ported from BitNaughts TerminalStyle (ScriptableObject eliminated).
    /// </summary>
    public static class TUIConstants
    {
        // ── Indentation ─────────────────────────────────────────
        public const int INDENT = 2;

        // ── Widget widths (characters) ──────────────────────────
        public const int PROGRESS_BAR_WIDTH = 22;
        public const int TAB_BUTTON_WIDTH = 12;
        public const int SETTINGS_BUTTON_WIDTH = 9;

        // ── Dual-column defaults ────────────────────────────────
        public const int DUAL_COLUMN_DIVIDER = 26;
        public const int LEFT_COLUMN_WIDTH = 25;
        public const int RIGHT_COLUMN_WIDTH = 25;
        public const string COLUMN_DIVIDER = "│";

        // ── Icon aliases (convenience strings) ──────────────────
        public const string ICON_CURSOR = "▶";
        public const string ICON_SCROLL_UP = "▲";
        public const string ICON_SCROLL_DOWN = "▼";
    }
}
