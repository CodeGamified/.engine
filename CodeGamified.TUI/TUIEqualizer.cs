// ═══════════════════════════════════════════════════════════
//  TUI.Equalizer — 2D frequency-band visualisation widget
//  Customisable width × height, multiple render styles.
//  Takes raw float[] arrays — no Audio dependency.
// ═══════════════════════════════════════════════════════════
using System;
using System.Text;
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Renders a 2D equalizer visualisation as rich-text lines.
    /// Feed it <c>float[] bands</c> and optional <c>float[] peaks</c>
    /// from <c>CodeGamified.Audio.Equalizer</c>.
    /// <code>
    /// string[] lines = TUIEqualizer.Render(eq.SmoothedBands, eq.PeakBands,
    ///     new TUIEqualizer.Config { Width = 40, Height = 12 });
    /// </code>
    /// </summary>
    public static class TUIEqualizer
    {
        // ── Vertical sub-block characters (bottom-aligned) ──────
        static readonly string[] VBar = { " ", "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█" };
        const string PeakChar = "─";

        /// <summary>Render style for the equalizer.</summary>
        public enum Style
        {
            /// <summary>Vertical bars rising from the bottom.</summary>
            Bars,
            /// <summary>Mirrored bars growing from the centre line.</summary>
            Mirror,
        }

        /// <summary>Configuration for <see cref="Render"/>.</summary>
        public struct Config
        {
            /// <summary>Total width in characters (including border).</summary>
            public int Width;
            /// <summary>Total height in rows (including border / label rows).</summary>
            public int Height;
            /// <summary>Render style.</summary>
            public Style Style;
            /// <summary>Draw a box-drawing border around the widget.</summary>
            public bool ShowBorder;
            /// <summary>Show peak-hold markers (requires peaks array).</summary>
            public bool ShowPeaks;
            /// <summary>Show a band-index label row at the bottom.</summary>
            public bool ShowLabels;
            /// <summary>
            /// Optional header text shown in the top border.
            /// Ignored when <see cref="ShowBorder"/> is false.
            /// </summary>
            public string Title;

            /// <summary>Sensible defaults: 32×10, Bars, bordered, peaks on.</summary>
            public static Config Default => new()
            {
                Width      = 32,
                Height     = 10,
                Style      = Style.Bars,
                ShowBorder = true,
                ShowPeaks  = true,
                ShowLabels = false,
                Title      = null,
            };
        }

        // ─────────────────────────────────────────────────────────
        //  Public API
        // ─────────────────────────────────────────────────────────

        /// <summary>
        /// Render with full configuration.
        /// </summary>
        public static string[] Render(float[] bands, float[] peaks, Config cfg)
        {
            if (bands == null || bands.Length == 0)
                return Array.Empty<string>();

            int w = Mathf.Max(cfg.Width, 3);
            int h = Mathf.Max(cfg.Height, 3);

            // Reserve rows for border and labels
            int borderV = cfg.ShowBorder ? 2 : 0;
            int labelH  = cfg.ShowLabels ? 1 : 0;
            int innerH  = Mathf.Max(1, h - borderV - labelH);
            int borderH = cfg.ShowBorder ? 2 : 0;
            int innerW  = Mathf.Max(1, w - borderH);

            // Compute bar geometry
            int bandCount = bands.Length;
            ComputeBarLayout(innerW, bandCount,
                out int barWidth, out int gap, out int visibleBands, out int leftPad);

            // Render inner rows
            string[] inner = cfg.Style == Style.Mirror
                ? RenderMirror(bands, peaks, visibleBands, barWidth, gap, innerW, innerH, leftPad, cfg.ShowPeaks)
                : RenderBars(bands, peaks, visibleBands, barWidth, gap, innerW, innerH, leftPad, cfg.ShowPeaks);

            // Assemble output
            int totalRows = innerH + borderV + labelH;
            var result = new string[totalRows];
            int ri = 0;

            if (cfg.ShowBorder)
                result[ri++] = TopBorder(innerW, cfg.Title);

            for (int i = 0; i < inner.Length; i++)
                result[ri++] = cfg.ShowBorder
                    ? $"{BorderV(0)}{inner[i]}{BorderV(1)}"
                    : inner[i];

            if (cfg.ShowLabels)
                result[ri++] = cfg.ShowBorder
                    ? $"{BorderV(0)}{LabelRow(visibleBands, barWidth, gap, innerW, leftPad)}{BorderV(1)}"
                    : LabelRow(visibleBands, barWidth, gap, innerW, leftPad);

            if (cfg.ShowBorder)
                result[ri++] = BottomBorder(innerW);

            return result;
        }

        /// <summary>
        /// Quick render with defaults — just supply width, height, and data.
        /// </summary>
        public static string[] Render(float[] bands, float[] peaks, int width, int height)
        {
            var cfg = Config.Default;
            cfg.Width  = width;
            cfg.Height = height;
            return Render(bands, peaks, cfg);
        }

        // ─────────────────────────────────────────────────────────
        //  Bar layout
        // ─────────────────────────────────────────────────────────

        static void ComputeBarLayout(int innerW, int bandCount,
            out int barWidth, out int gap, out int visibleBands, out int leftPad)
        {
            // Try with gap first (gap = 1 between bars)
            if (bandCount * 2 - 1 <= innerW)
            {
                gap = 1;
                barWidth = (innerW + gap) / bandCount - gap;
                barWidth = Mathf.Max(1, barWidth);
                visibleBands = bandCount;
            }
            else
            {
                gap = 0;
                barWidth = Mathf.Max(1, innerW / bandCount);
                visibleBands = Mathf.Min(bandCount, innerW / Mathf.Max(barWidth, 1));
            }

            int usedWidth = visibleBands * barWidth + Mathf.Max(0, visibleBands - 1) * gap;
            leftPad = (innerW - usedWidth) / 2;
        }

        // ─────────────────────────────────────────────────────────
        //  Bars style — bottom-up vertical bars
        // ─────────────────────────────────────────────────────────

        static string[] RenderBars(float[] bands, float[] peaks,
            int visibleBands, int barWidth, int gap, int innerW, int innerH,
            int leftPad, bool showPeaks)
        {
            var rows = new string[innerH];

            for (int row = 0; row < innerH; row++)
            {
                var sb = new StringBuilder(innerW);

                // Left padding
                sb.Append(' ', leftPad);

                for (int b = 0; b < visibleBands; b++)
                {
                    if (b > 0 && gap > 0) sb.Append(' ', gap);

                    float level = Clamp01(bands[b]);
                    float peakLevel = (peaks != null && b < peaks.Length) ? Clamp01(peaks[b]) : 0f;

                    // Total eighths of fill from bottom
                    int totalEighths = (int)(level * innerH * 8);
                    int rowFromBottom = innerH - 1 - row;
                    int eighthsHere = totalEighths - rowFromBottom * 8;

                    // Peak row (which row does the peak marker land on?)
                    int peakRow = showPeaks && peakLevel > 0f
                        ? innerH - 1 - Mathf.Min(innerH - 1, (int)(peakLevel * (innerH - 1)))
                        : -1;

                    // Band color from gradient (horizontal position)
                    float t = visibleBands > 1 ? (float)b / (visibleBands - 1) : 0.5f;
                    Color32 col = TUIGradient.CyanMagenta(t);
                    string cOpen = TUIColors.FgOpen(col.r, col.g, col.b);

                    string cell;
                    if (showPeaks && row == peakRow && eighthsHere < 8)
                    {
                        // Peak marker
                        cell = $"{cOpen}{PeakChar}{TUIColors.FgClose}";
                    }
                    else if (eighthsHere >= 8)
                    {
                        // Full block
                        cell = $"{cOpen}{VBar[8]}{TUIColors.FgClose}";
                    }
                    else if (eighthsHere > 0)
                    {
                        // Partial block
                        cell = $"{cOpen}{VBar[eighthsHere]}{TUIColors.FgClose}";
                    }
                    else
                    {
                        cell = " ";
                    }

                    // Repeat for bar width
                    for (int c = 0; c < barWidth; c++)
                        sb.Append(cell);
                }

                // Right padding
                int used = leftPad + visibleBands * barWidth
                    + Mathf.Max(0, visibleBands - 1) * gap;
                int rightPad = innerW - used;
                if (rightPad > 0) sb.Append(' ', rightPad);

                rows[row] = sb.ToString();
            }

            return rows;
        }

        // ─────────────────────────────────────────────────────────
        //  Mirror style — bars grow out from centre
        // ─────────────────────────────────────────────────────────

        static string[] RenderMirror(float[] bands, float[] peaks,
            int visibleBands, int barWidth, int gap, int innerW, int innerH,
            int leftPad, bool showPeaks)
        {
            int halfH = innerH / 2;
            var rows = new string[innerH];

            for (int row = 0; row < innerH; row++)
            {
                var sb = new StringBuilder(innerW);
                sb.Append(' ', leftPad);

                for (int b = 0; b < visibleBands; b++)
                {
                    if (b > 0 && gap > 0) sb.Append(' ', gap);

                    float level = Clamp01(bands[b]);
                    float peakLevel = (peaks != null && b < peaks.Length) ? Clamp01(peaks[b]) : 0f;
                    int totalEighths = (int)(level * halfH * 8);

                    // Distance from centre line (in rows)
                    int midRow = halfH;
                    int distFromCentre;
                    bool isUpper = row < midRow;
                    if (isUpper)
                        distFromCentre = midRow - 1 - row;  // rows above centre
                    else
                        distFromCentre = row - midRow;       // rows below centre

                    int eighthsHere = totalEighths - distFromCentre * 8;

                    // Peak row distance
                    int peakDist = showPeaks && peakLevel > 0f
                        ? Mathf.Min(halfH - 1, (int)(peakLevel * (halfH - 1)))
                        : -1;

                    float t = visibleBands > 1 ? (float)b / (visibleBands - 1) : 0.5f;
                    Color32 col = TUIGradient.CyanMagenta(t);
                    string cOpen = TUIColors.FgOpen(col.r, col.g, col.b);

                    string cell;
                    if (showPeaks && distFromCentre == peakDist && eighthsHere < 8)
                    {
                        cell = $"{cOpen}{PeakChar}{TUIColors.FgClose}";
                    }
                    else if (eighthsHere >= 8)
                    {
                        // For upper half, flip the sub-block direction
                        if (isUpper)
                            cell = $"{cOpen}{VBar[8]}{TUIColors.FgClose}";
                        else
                            cell = $"{cOpen}{VBar[8]}{TUIColors.FgClose}";
                    }
                    else if (eighthsHere > 0)
                    {
                        if (isUpper)
                        {
                            // Upper half: blocks grow downward, so use inverted eighths (▔ not available, use full)
                            // Use upper blocks: full block - eighths  → we approximate with same block chars
                            cell = $"{cOpen}{VBar[eighthsHere]}{TUIColors.FgClose}";
                        }
                        else
                        {
                            cell = $"{cOpen}{VBar[eighthsHere]}{TUIColors.FgClose}";
                        }
                    }
                    else
                    {
                        cell = " ";
                    }

                    for (int c = 0; c < barWidth; c++)
                        sb.Append(cell);
                }

                int used = leftPad + visibleBands * barWidth
                    + Mathf.Max(0, visibleBands - 1) * gap;
                int rightPad = innerW - used;
                if (rightPad > 0) sb.Append(' ', rightPad);

                rows[row] = sb.ToString();
            }

            return rows;
        }

        // ─────────────────────────────────────────────────────────
        //  Border helpers
        // ─────────────────────────────────────────────────────────

        static string TopBorder(int innerW, string title)
        {
            var sb = new StringBuilder();
            Color32 c0 = TUIConfig.Gradient[0];
            string ac = TUIColors.FgOpen(c0.r, c0.g, c0.b);

            sb.Append($"{ac}{TUIGlyphs.BoxTL}{TUIColors.FgClose}");

            if (!string.IsNullOrEmpty(title) && title.Length + 2 <= innerW)
            {
                int pad = (innerW - title.Length - 2) / 2;
                int rPad = innerW - title.Length - 2 - pad;
                sb.Append(TUIColors.Dimmed(Repeat(TUIGlyphs.BoxH, pad)));
                sb.Append($" {TUIColors.Bold(title)} ");
                sb.Append(TUIColors.Dimmed(Repeat(TUIGlyphs.BoxH, rPad)));
            }
            else
            {
                sb.Append(TUIColors.Dimmed(Repeat(TUIGlyphs.BoxH, innerW)));
            }

            sb.Append($"{ac}{TUIGlyphs.BoxTR}{TUIColors.FgClose}");
            return sb.ToString();
        }

        static string BottomBorder(int innerW)
        {
            Color32 c0 = TUIConfig.Gradient[0];
            string ac = TUIColors.FgOpen(c0.r, c0.g, c0.b);
            return $"{ac}{TUIGlyphs.BoxBL}{TUIColors.FgClose}" +
                   TUIColors.Dimmed(Repeat(TUIGlyphs.BoxH, innerW)) +
                   $"{ac}{TUIGlyphs.BoxBR}{TUIColors.FgClose}";
        }

        static string BorderV(int side)
        {
            Color32 c0 = TUIConfig.Gradient[0];
            return TUIColors.Fg(c0, TUIGlyphs.BoxV);
        }

        // ─────────────────────────────────────────────────────────
        //  Labels
        // ─────────────────────────────────────────────────────────

        static string LabelRow(int visibleBands, int barWidth, int gap, int innerW, int leftPad)
        {
            var sb = new StringBuilder(innerW);
            sb.Append(' ', leftPad);

            for (int b = 0; b < visibleBands; b++)
            {
                if (b > 0 && gap > 0) sb.Append(' ', gap);
                string label = (b + 1).ToString();
                // Centre label within barWidth
                int lPad = (barWidth - label.Length) / 2;
                int rPad = barWidth - label.Length - lPad;
                if (lPad > 0) sb.Append(' ', lPad);
                sb.Append(TUIColors.Dimmed(label.Length <= barWidth ? label : label[..barWidth]));
                if (rPad > 0) sb.Append(' ', rPad);
            }

            int used = leftPad + visibleBands * barWidth
                + Mathf.Max(0, visibleBands - 1) * gap;
            int rightPad = innerW - used;
            if (rightPad > 0) sb.Append(' ', rightPad);

            return sb.ToString();
        }

        // ─────────────────────────────────────────────────────────
        //  Utility
        // ─────────────────────────────────────────────────────────

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        static string Repeat(string s, int count)
        {
            if (count <= 0) return "";
            var sb = new StringBuilder(s.Length * count);
            for (int i = 0; i < count; i++) sb.Append(s);
            return sb.ToString();
        }
    }
}
