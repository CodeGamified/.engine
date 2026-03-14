// ═══════════════════════════════════════════════════════════
//  TUI.Text — String utilities (rich-text aware)
//  Python source: TUI.py § Text + ANSI string utilities
// ═══════════════════════════════════════════════════════════
using System.Text;
using System.Text.RegularExpressions;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Rich-text string utilities — strip tags, truncate, sanitize.
    /// Python: strip_ansi → StripTags, truncate_ansi → Truncate,
    ///         sanitize_emoji → SanitizeEmoji, strip_html → StripHtml
    /// </summary>
    public static class TUIText
    {
        // TMP uses XML-style tags: <color=#FF0000>, <b>, </b>, etc.
        static readonly Regex RichTagRe = new(@"<\/?[a-zA-Z][^>]*>", RegexOptions.Compiled);

        // HTML tags (for settings identity.name etc.)
        static readonly Regex HtmlTagRe = new(@"<[^>]+>", RegexOptions.Compiled);

        /// <summary>
        /// Strip all rich-text / TMP tags. Python equivalent: strip_ansi().
        /// </summary>
        public static string StripTags(string text)
            => string.IsNullOrEmpty(text) ? text : RichTagRe.Replace(text, "");

        /// <summary>
        /// Visible character count (ignoring rich-text tags).
        /// </summary>
        public static int VisibleLength(string text)
            => string.IsNullOrEmpty(text) ? 0 : StripTags(text).Length;

        /// <summary>
        /// Truncate a rich-text string to maxVis visible characters.
        /// Appends ellipsis if truncated. Tags are preserved for visible portion.
        /// Python equivalent: truncate_ansi().
        /// </summary>
        public static string Truncate(string text, int maxVis, string ellipsis = "…")
        {
            if (string.IsNullOrEmpty(text) || maxVis <= 0) return "";

            var sb = new StringBuilder();
            int seen = 0;
            int ellLen = ellipsis.Length;
            int i = 0;

            while (i < text.Length)
            {
                // Rich-text tag — pass through without counting
                if (text[i] == '<')
                {
                    int close = text.IndexOf('>', i);
                    if (close > i)
                    {
                        sb.Append(text, i, close - i + 1);
                        i = close + 1;
                        continue;
                    }
                }

                // Visible character
                if (seen >= maxVis) break;

                // Check if we need to start ellipsis
                if (seen >= maxVis - ellLen)
                {
                    int remaining = VisibleLength(text[i..]);
                    if (remaining > maxVis - seen)
                    {
                        sb.Append(TUIColors.Dimmed(ellipsis));
                        break;
                    }
                }

                sb.Append(text[i]);
                seen++;
                i++;
            }

            return sb.ToString();
        }

        /// <summary>Strip HTML tags. Python equivalent: strip_html().</summary>
        public static string StripHtml(string text)
            => string.IsNullOrEmpty(text) ? text : HtmlTagRe.Replace(text, "");

        /// <summary>
        /// Strip emoji skin-tone modifiers, variation selectors, and ZWJ.
        /// Python equivalent: sanitize_emoji().
        /// </summary>
        public static string SanitizeEmoji(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                // Variation selectors U+FE0E, U+FE0F
                // Zero-width joiner U+200D
                if (c == '\uFE0E' || c == '\uFE0F' || c == '\u200D')
                    continue;
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
