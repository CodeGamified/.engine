// ═══════════════════════════════════════════════════════════
//  TUI.Layout — Text positioning within card boundaries
//  Python source: TUI.py § Layout
// ═══════════════════════════════════════════════════════════

namespace CodeGamified.TUI
{
    /// <summary>
    /// Layout utilities for positioning text within card/box boundaries.
    /// Python: center_text(text, card_w)
    /// </summary>
    public static class TUILayout
    {
        /// <summary>
        /// Center text within card inner width (cardWidth - 4 for box padding).
        /// </summary>
        public static string CenterText(string text, int cardWidth)
        {
            int usable = System.Math.Max(0, cardWidth - 4);
            int vis = TUIText.VisibleLength(text);
            int pad = System.Math.Max(0, (usable - vis) / 2);
            return pad > 0 ? new string(' ', pad) + text : text;
        }

        /// <summary>
        /// Right-align text within card inner width.
        /// </summary>
        public static string RightAlign(string text, int cardWidth)
        {
            int usable = System.Math.Max(0, cardWidth - 4);
            int vis = TUIText.VisibleLength(text);
            int pad = System.Math.Max(0, usable - vis);
            return pad > 0 ? new string(' ', pad) + text : text;
        }
    }
}
