// ═══════════════════════════════════════════════════════════
//  TUI.Widgets — ProgressBar, Spinner, Divider, Box
//  Python source: TUI.py § Widgets
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Composite TUI widgets — returns rich-text strings for display.
    /// Python: progress_bar(), spinner_frame(), divider(), header_line(), box()
    /// </summary>
    public static class TUIWidgets
    {
        /// <summary>
        /// Smooth sub-character progress bar using Unicode eighths.
        /// Filled portion colored along the brand gradient.
        /// </summary>
        public static string ProgressBar(float progress, int length = 20, bool showPct = true)
        {
            progress = Mathf.Clamp01(progress);
            float filledExact = progress * length;
            int filledFull = (int)filledExact;
            float frac = filledExact - filledFull;
            int eighth = (int)(frac * 8);

            var sb = new StringBuilder();
            for (int i = 0; i < length; i++)
            {
                float t = length > 1 ? (float)i / (length - 1) : 0f;
                Color32 c = TUIGradient.CyanMagenta(t);
                string co = TUIColors.FgOpen(c.r, c.g, c.b);

                if (i < filledFull)
                    sb.Append($"{co}{TUIGlyphs.BlockFull}{TUIColors.FgClose}");
                else if (i == filledFull && eighth > 0)
                    sb.Append($"{co}{TUIGlyphs.BlockEighths[eighth]}{TUIColors.FgClose}");
                else
                    sb.Append(' ');
            }

            string bar = sb.ToString();
            string dim = TUIColors.Dimmed("[");
            string dimClose = TUIColors.Dimmed("]");

            if (showPct)
                return $"{dim}{bar}{dimClose}{(int)(progress * 100),3}%";
            return $"{dim}{bar}{dimClose}";
        }

        /// <summary>
        /// Get current spinner frame string for given age.
        /// </summary>
        public static string SpinnerFrame(float age, string[] frames = null, float speed = 0.08f)
        {
            frames ??= TUIGlyphs.BrailleSpin;
            int idx = (int)(age / speed) % frames.Length;
            return frames[idx];
        }

        /// <summary>
        /// Horizontal divider line with tee connectors.
        /// </summary>
        public static string Divider(int length,
            string left = null, string mid = null, string right = null)
        {
            left  ??= TUIGlyphs.BoxTeeR;
            mid   ??= TUIGlyphs.BoxH;
            right ??= TUIGlyphs.BoxTeeL;

            if (length <= 2) return Repeat(mid, length);
            return TUIColors.Dimmed($"{left}{Repeat(mid, length - 2)}{right}");
        }

        /// <summary>
        /// Centered header line with diamond corners and gradient text.
        /// </summary>
        public static string HeaderLine(string text, int width)
        {
            int pad = Mathf.Max(0, (width - text.Length - 4) / 2);
            string lp = Repeat(TUIGlyphs.BoxH, pad);
            string rp = Repeat(TUIGlyphs.BoxH, width - text.Length - 4 - pad);
            Color32 accent = TUIConfig.Gradient[0];
            string ac = TUIColors.FgOpen(accent.r, accent.g, accent.b);
            return $"{ac}{TUIGlyphs.DiamondFilled}{lp}{TUIColors.FgClose} " +
                   $"{TUIColors.Bold(text)} " +
                   $"{ac}{rp}{TUIGlyphs.DiamondFilled}{TUIColors.FgClose}";
        }

        /// <summary>
        /// Gradient-bordered box. Border color sweeps around the perimeter.
        /// Returns a multi-line rich-text string.
        /// </summary>
        public static string Box(string[] lines, int width)
        {
            int inner = width - 2;
            int nLines = lines.Length;
            int perim = inner + nLines + inner + nLines;
            var loop = TUIGradient.MakeLoop(TUIConfig.Gradient);

            string BorderChar(int idx, string ch)
            {
                Color32 c = TUIGradient.Lerp(loop, (float)idx / Mathf.Max(perim, 1));
                return TUIColors.Fg(c, ch);
            }

            var sb = new StringBuilder();

            // Top border
            sb.Append(BorderChar(0, TUIGlyphs.BoxDblTL));
            for (int i = 0; i < inner; i++)
                sb.Append(BorderChar(i, TUIGlyphs.BoxDblH));
            sb.Append(BorderChar(inner - 1, TUIGlyphs.BoxDblTR));
            sb.AppendLine();

            // Content rows
            int usable = Mathf.Max(0, inner - 2);
            for (int ri = 0; ri < nLines; ri++)
            {
                string ln = lines[ri];
                int vis = TUIText.VisibleLength(ln);
                if (vis > usable)
                {
                    ln = TUIText.Truncate(ln, usable);
                    vis = TUIText.VisibleLength(ln);
                }
                int rpad = Mathf.Max(0, inner - vis - 1);

                int rightIdx = inner + ri;
                int leftIdx = perim - 1 - ri;
                string leftV  = BorderChar(leftIdx, TUIGlyphs.BoxDblV);
                string rightV = BorderChar(rightIdx, TUIGlyphs.BoxDblV);
                sb.Append($"{leftV} {ln}{new string(' ', rpad)}{rightV}");
                sb.AppendLine();
            }

            // Bottom border
            int botStart = inner + nLines;
            sb.Append(BorderChar(botStart + inner - 1, TUIGlyphs.BoxDblBL));
            for (int j = 0; j < inner; j++)
                sb.Append(BorderChar(botStart + (inner - 1 - j), TUIGlyphs.BoxDblH));
            sb.Append(BorderChar(botStart, TUIGlyphs.BoxDblBR));

            return sb.ToString();
        }

        // ── Indicators (ported from BitNaughts TextAnimUtils) ──────

        /// <summary>
        /// Signal strength indicator (0-4 bars).
        /// </summary>
        public static string SignalStrength(int bars)
        {
            bars = Mathf.Clamp(bars, 0, 4);
            return bars switch
            {
                0 => "▁   ",
                1 => "▁▃  ",
                2 => "▁▃▅ ",
                3 => "▁▃▅▇",
                4 => "▁▃▅█",
                _ => "    "
            };
        }

        /// <summary>
        /// Battery indicator with level.
        /// </summary>
        public static string BatteryIndicator(float level)
        {
            level = Mathf.Clamp01(level);
            if (level > 0.75f) return "█▌";
            if (level > 0.50f) return "▓▌";
            if (level > 0.25f) return "▒▌";
            if (level > 0.10f) return "░▌";
            return " ▌";
        }

        /// <summary>
        /// Temperature gauge using block characters.
        /// </summary>
        public static string TemperatureGauge(float normalized, int length = 5)
        {
            normalized = Mathf.Clamp01(normalized);
            int filled = Mathf.FloorToInt(normalized * length);
            char fillChar = normalized > 0.8f ? '▓' : (normalized > 0.5f ? '▒' : '░');
            return $"[{new string(fillChar, filled)}{new string(' ', length - filled)}]";
        }

        /// <summary>
        /// Adaptive slider row that adjusts layout to available column width.
        /// Tiers:  w&lt;6: "- +"  |  w&lt;10: "[-] [+]"  |  w&lt;16: "[-] LBL [+]"
        ///         w&lt;22: "[-] LBL VAL [+]"  |  w>=22: "[-] LBL BAR VAL [+]"
        /// </summary>
        public static string AdaptiveSliderRow(int colWidth, string label, float norm, string valueStr, bool showPct = false)
        {
            int w = colWidth - 1;
            string minus = TUIColors.Fg(TUIColors.BrightCyan, "[-]");
            string plus = TUIColors.Fg(TUIColors.BrightCyan, "[+]");

            if (w < 6)
                return $" {TUIColors.Fg(TUIColors.BrightCyan, "-")} {TUIColors.Fg(TUIColors.BrightCyan, "+")}";
            if (w < 10)
                return $" {minus} {plus}";
            if (w < 16)
                return $" {minus} {label} {plus}";

            int overhead = 10 + label.Length + valueStr.Length;
            if (w < overhead + 4)
                return $" {minus} {label} {valueStr} {plus}";

            int barLen = w - overhead;
            return $" {minus} {label}{ProgressBar(Mathf.Clamp01(norm), barLen, showPct)}{valueStr} {plus}";
        }

        static string Repeat(string s, int count)
        {
            if (count <= 0) return "";
            var sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }

        /// <summary>
        /// Build TUI-formatted register/state lines.
        /// Single source of truth for state display across all debugger panels.
        /// </summary>
        public static List<string> BuildStateLines(
            float[] registers, int lastRegModified,
            object flags, int pc, int stackCount,
            Dictionary<string, int> nameToAddress,
            Dictionary<string, float> memory)
        {
            var lines = new List<string>();

            int regCount = registers != null ? registers.Length : 0;
            for (int r = 0; r < regCount; r++)
            {
                bool modified = (r == lastRegModified);
                string rName = $"R{r:D2}";
                float val = registers[r];
                string rVal = val < 0 ? $"{val:F2}" : $" {val:F2}";
                if (modified)
                    lines.Add(TUIColors.Fg(TUIColors.BrightGreen, $" {rName} {rVal}"));
                else
                    lines.Add($" {TUIColors.Dimmed(rName)} {rVal}");
            }

            lines.Add("");

            lines.Add($" FG:  {flags}");
            lines.Add($" PC:  {pc}");
            lines.Add($" ST:  [{stackCount}]");

            if (nameToAddress != null && nameToAddress.Count > 0)
            {
                lines.Add("");
                lines.Add(TUIColors.Fg(TUIColors.BrightCyan, " VARS"));
                foreach (var kvp in nameToAddress)
                {
                    string name = kvp.Key;
                    string display = name.StartsWith("_mem")
                        ? "M" + int.Parse(name.Substring(4)).ToString("D2")
                        : name;
                    float val = (memory != null && memory.ContainsKey(name)) ? memory[name] : 0;
                    lines.Add($" {TUIColors.Dimmed(display)}  {val:F2}");
                }
            }

            return lines;
        }
    }
}
