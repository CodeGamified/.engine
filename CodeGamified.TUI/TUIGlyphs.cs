// ═══════════════════════════════════════════════════════════
//  TUI.Glyphs — Unicode box-drawing, blocks, status icons
//  Python source: TUI.py § Glyphs
// ═══════════════════════════════════════════════════════════

namespace CodeGamified.TUI
{
    /// <summary>
    /// All TUI glyph constants — box drawing, blocks, spinners, status.
    /// Python: module-level constants → C#: TUIGlyphs.BoxH, etc.
    /// </summary>
    public static class TUIGlyphs
    {
        // ── Box drawing ─────────────────────────────────────────
        public const string BoxH    = "─"; public const string BoxV    = "│";
        public const string BoxTL   = "┌"; public const string BoxTR   = "┐";
        public const string BoxBL   = "└"; public const string BoxBR   = "┘";
        public const string BoxTeeR = "├"; public const string BoxTeeL = "┤";

        public const string BoxDblH  = "═"; public const string BoxDblV  = "║";
        public const string BoxDblTL = "╔"; public const string BoxDblTR = "╗";
        public const string BoxDblBL = "╚"; public const string BoxDblBR = "╝";

        // ── Block elements ──────────────────────────────────────
        public const string BlockFull   = "█"; public const string BlockLight  = "░";
        public const string BlockMedium = "▒"; public const string BlockDark   = "▓";

        public static readonly string[] BlockEighths =
        {
            " ", "▏", "▎", "▍", "▌", "▋", "▊", "▉", "█"
        };

        // ── Geometric shapes ────────────────────────────────────
        public const string DiamondEmpty  = "◇"; public const string DiamondFilled = "◆";
        public const string DiamondDot    = "◈";
        public const string CircleEmpty   = "○"; public const string CircleFilled  = "●";
        public const string CircleDot     = "◉";

        // ── Status indicators ───────────────────────────────────
        public const string Check  = "✓"; public const string Cross = "✗";
        public const string Warn   = "⚠"; public const string Info  = "ⓘ";
        public const string ArrowR = "→"; public const string ArrowL = "←";
        public const string ArrowU = "↑"; public const string ArrowD = "↓";

        // ── Spinner frame sequences ─────────────────────────────
        public static readonly string[] BrailleSpin =
        {
            "⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"
        };

        public static readonly string[] BlockSpin    = { "▖", "▘", "▝", "▗" };
        public static readonly string[] PulseBox     = { "·", "▪", "■", "▪" };
        public static readonly string[] PulseDiamond = { "·", "◇", "◈", "◆", "◈", "◇" };
        public static readonly string[] PulseCircle  = { "·", "○", "◉", "●", "◉", "○" };
        public static readonly string[] RadarSweep   = { "◜", "◝", "◞", "◟" };

        // ── Scramble charset ────────────────────────────────────
        public const string ScrambleChars =
            "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*<>░▒▓│┤╡╢╖╕╣║╗╝╜╛┐└┴┬├─┼╞╟╚╔╩╦╠═╬";
    }
}
