// ═══════════════════════════════════════════════════════════
//  TUI.Animation — Decode/fadeout row animations (frame-based)
//  Python source: TUI.py § Animation
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Frame-based text animation primitives.
    /// Call each frame with current age — returns string arrays for display.
    /// Python: decode_rows(), fadeout_rows(), animate_scramble_reveal(), etc.
    /// </summary>
    public static class TUIAnimation
    {
        /// <summary>
        /// Result of a row-decode animation frame.
        /// </summary>
        public struct DecodeResult
        {
            public string[] Lines;
            public bool AllResolved;
        }

        /// <summary>
        /// Rows enter top-to-bottom with scramble-decode.
        /// Call each frame with increasing phaseAge.
        /// </summary>
        public static DecodeResult DecodeRows(
            string[] finalLines, float phaseAge,
            float decodeSeconds, float rowDelay = 0.08f)
        {
            int n = finalLines.Length;
            var plains = new string[n];
            int longest = 1;
            for (int i = 0; i < n; i++)
            {
                plains[i] = TUIText.StripTags(finalLines[i]);
                if (!string.IsNullOrWhiteSpace(plains[i]) && plains[i].Length > longest)
                    longest = plains[i].Length;
            }

            float charRate = Mathf.Max(0.005f, decodeSeconds / Mathf.Max(longest, 1));
            int visibleN = rowDelay > 0
                ? Mathf.Min(n, (int)(phaseAge / rowDelay) + 1)
                : n;

            bool allResolved = true;
            var content = new List<string>(visibleN);

            for (int ri = 0; ri < visibleN; ri++)
            {
                string plain = plains[ri];
                float rowAge = Mathf.Max(0f, phaseAge - ri * rowDelay);

                if (string.IsNullOrWhiteSpace(plain))
                {
                    content.Add(finalLines[ri]);
                }
                else if (rowAge / charRate >= plain.Length)
                {
                    content.Add(finalLines[ri]);
                }
                else
                {
                    allResolved = false;
                    string revealed = TUIEffects.ScrambleText(plain, rowAge, charRate);
                    content.Add(TUIEffects.GradientColorize(revealed));
                }
            }

            return new DecodeResult
            {
                Lines = content.ToArray(),
                AllResolved = allResolved
            };
        }

        /// <summary>
        /// Rows retract bottom-to-top, re-scrambling into noise.
        /// </summary>
        public static string[] FadeoutRows(
            string[] finalLines, float phaseAge,
            float fadeoutSeconds, float rowDelay = -1f)
        {
            int n = finalLines.Length;
            var plains = new string[n];
            int longest = 1;
            for (int i = 0; i < n; i++)
            {
                plains[i] = TUIText.StripTags(finalLines[i]);
                if (!string.IsNullOrWhiteSpace(plains[i]) && plains[i].Length > longest)
                    longest = plains[i].Length;
            }

            if (rowDelay < 0f)
                rowDelay = fadeoutSeconds * 0.35f / Mathf.Max(n - 1, 1);
            float scrambleTime = fadeoutSeconds * 0.65f;
            float charRate = Mathf.Max(0.005f, scrambleTime / Mathf.Max(longest, 1));

            var content = new List<string>();
            for (int ri = 0; ri < n; ri++)
            {
                string plain = plains[ri];
                float rowStart = (n - 1 - ri) * rowDelay;
                float rowAge = Mathf.Max(0f, phaseAge - rowStart);

                if (string.IsNullOrWhiteSpace(plain))
                {
                    if (rowAge < scrambleTime * 0.5f)
                        content.Add(finalLines[ri]);
                    continue;
                }

                float fullTime = plain.Length * charRate;
                float virtualAge = fullTime - rowAge;
                if (virtualAge <= 0f)
                    continue;
                if (virtualAge >= fullTime)
                {
                    content.Add(finalLines[ri]);
                }
                else
                {
                    string revealed = TUIEffects.ScrambleText(plain, virtualAge, charRate);
                    content.Add(TUIEffects.GradientColorize(revealed));
                }
            }

            return content.ToArray();
        }

        /// <summary>
        /// Single-line scramble reveal — returns current frame text and whether complete.
        /// </summary>
        public static (string text, bool done) ScrambleRevealFrame(
            string target, float age, float charRate = 0.02f)
        {
            string revealed = TUIEffects.ScrambleText(target, age, charRate);
            bool done = age / charRate >= target.Length;
            string colored = done ? target : TUIEffects.GradientColorize(revealed);
            return (colored, done);
        }

        /// <summary>
        /// Progress bar animation frame — returns formatted line and whether complete.
        /// </summary>
        public static (string text, bool done) ProgressFrame(
            string label, float age, float duration = 1.2f, int barWidth = 24)
        {
            float prog = Mathf.Clamp01(age / duration);
            string spin = TUIWidgets.SpinnerFrame(age);
            string bar = TUIWidgets.ProgressBar(prog, barWidth);
            bool done = prog >= 1f;

            Color32 spinColor = done
                ? TUIColors.BrightGreen
                : TUIColors.BrightMagenta;
            string icon = done
                ? TUIColors.Fg(TUIColors.BrightGreen, TUIGlyphs.Check)
                : TUIColors.Fg(spinColor, spin);
            string suffix = done ? TUIColors.Dimmed("done") : bar;
            string line = $"  {icon} {TUIColors.Dimmed(label)}  {suffix}";

            return (line, done);
        }

        /// <summary>
        /// Step indicator frame — radar sweep then check/cross.
        /// </summary>
        public static (string text, bool done) StepFrame(
            string label, float age, bool ok = true, int totalFrames = 12, float frameTime = 0.06f)
        {
            float totalDuration = totalFrames * frameTime;
            bool done = age >= totalDuration;

            if (done)
            {
                string mark = ok
                    ? TUIColors.Fg(TUIColors.BrightGreen, TUIGlyphs.Check)
                    : TUIColors.Fg(TUIColors.Red, TUIGlyphs.Cross);
                return ($"  {mark} {label}", true);
            }

            int frame = (int)(age / frameTime);
            string sweep = TUIGlyphs.RadarSweep[frame % TUIGlyphs.RadarSweep.Length];
            string line = $"  {TUIColors.Fg(TUIColors.BrightYellow, sweep)} {TUIColors.Dimmed(label)}";
            return (line, false);
        }
    }
}
