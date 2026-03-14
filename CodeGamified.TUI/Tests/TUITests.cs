// ═══════════════════════════════════════════════════════════
//  TUITests — Unit tests for TUI static primitives
//  Run via Unity Test Runner (Edit Mode)
// ═══════════════════════════════════════════════════════════
using NUnit.Framework;
using UnityEngine;

namespace CodeGamified.TUI.Tests
{
    public class TUITests
    {
        // ── TUIText ─────────────────────────────────────────────

        [Test] public void StripTags_RemovesColorTags()
        {
            string input = "<color=#FF0000>Hello</color> <b>World</b>";
            Assert.AreEqual("Hello World", TUIText.StripTags(input));
        }

        [Test] public void StripTags_NullReturnsNull()
        {
            Assert.IsNull(TUIText.StripTags(null));
        }

        [Test] public void StripTags_EmptyReturnsEmpty()
        {
            Assert.AreEqual("", TUIText.StripTags(""));
        }

        [Test] public void StripTags_PlainTextUnchanged()
        {
            Assert.AreEqual("Hello World", TUIText.StripTags("Hello World"));
        }

        [Test] public void VisibleLength_IgnoresTags()
        {
            string input = "<color=#FF0000>AB</color>CD";
            Assert.AreEqual(4, TUIText.VisibleLength(input));
        }

        [Test] public void VisibleLength_NullReturnsZero()
        {
            Assert.AreEqual(0, TUIText.VisibleLength(null));
        }

        [Test] public void SanitizeEmoji_RemovesVariationSelectors()
        {
            string input = "A\uFE0FB\u200DC";
            Assert.AreEqual("ABC", TUIText.SanitizeEmoji(input));
        }

        [Test] public void StripHtml_RemovesTags()
        {
            Assert.AreEqual("test", TUIText.StripHtml("<b>test</b>"));
        }

        // ── TUIGradient ─────────────────────────────────────────

        [Test] public void Lerp_AtZero_ReturnsFirstStop()
        {
            var stops = new Color32[]
            {
                new(255, 0, 0, 255),
                new(0, 0, 255, 255)
            };
            Color32 c = TUIGradient.Lerp(stops, 0f);
            Assert.AreEqual(255, c.r);
            Assert.AreEqual(0, c.g);
            Assert.AreEqual(0, c.b);
        }

        [Test] public void Lerp_AtOne_ReturnsLastStop()
        {
            var stops = new Color32[]
            {
                new(255, 0, 0, 255),
                new(0, 0, 255, 255)
            };
            Color32 c = TUIGradient.Lerp(stops, 1f);
            Assert.AreEqual(0, c.r);
            Assert.AreEqual(255, c.b);
        }

        [Test] public void Lerp_AtHalf_Interpolates()
        {
            var stops = new Color32[]
            {
                new(0, 0, 0, 255),
                new(200, 100, 50, 255)
            };
            Color32 c = TUIGradient.Lerp(stops, 0.5f);
            Assert.AreEqual(100, c.r);
            Assert.AreEqual(50, c.g);
            Assert.AreEqual(25, c.b);
        }

        [Test] public void Lerp_EmptyStops_ReturnsWhite()
        {
            Color32 c = TUIGradient.Lerp(new Color32[] { }, 0.5f);
            Assert.AreEqual(255, c.r);
            Assert.AreEqual(255, c.g);
            Assert.AreEqual(255, c.b);
        }

        [Test] public void Lerp_SingleStop_ReturnsThatStop()
        {
            var stops = new Color32[] { new(42, 84, 128, 255) };
            Color32 c = TUIGradient.Lerp(stops, 0.7f);
            Assert.AreEqual(42, c.r);
            Assert.AreEqual(84, c.g);
            Assert.AreEqual(128, c.b);
        }

        [Test] public void Lerp_ClampsBeyondRange()
        {
            var stops = new Color32[]
            {
                new(0, 0, 0, 255),
                new(255, 255, 255, 255)
            };
            Color32 cBelow = TUIGradient.Lerp(stops, -1f);
            Color32 cAbove = TUIGradient.Lerp(stops, 2f);
            Assert.AreEqual(0, cBelow.r);
            Assert.AreEqual(255, cAbove.r);
        }

        [Test] public void MakeLoop_AppendsFirstStop()
        {
            var stops = new Color32[]
            {
                new(10, 20, 30, 255),
                new(40, 50, 60, 255)
            };
            var loop = TUIGradient.MakeLoop(stops);
            Assert.AreEqual(3, loop.Length);
            Assert.AreEqual(10, loop[2].r);
        }

        // ── TUIEffects ──────────────────────────────────────────

        [Test] public void ScrambleText_FullyResolved_ReturnsTarget()
        {
            string result = TUIEffects.ScrambleText("HELLO", 10f, 0.02f);
            Assert.AreEqual("HELLO", result);
        }

        [Test] public void ScrambleText_AtZero_AllScrambled()
        {
            string result = TUIEffects.ScrambleText("HELLO", 0f, 0.02f);
            Assert.AreEqual(5, result.Length);
            Assert.AreNotEqual("HELLO", result);
        }

        [Test] public void ScrambleText_PreservesSpaces()
        {
            string result = TUIEffects.ScrambleText("A B C", 0f, 0.02f);
            Assert.AreEqual(' ', result[1]);
            Assert.AreEqual(' ', result[3]);
        }

        [Test] public void IsResolved_ReturnsTrueWhenDone()
        {
            Assert.IsTrue(TUIEffects.IsResolved("HI", 1f, 0.02f));
        }

        [Test] public void IsResolved_ReturnsFalseWhenNotDone()
        {
            Assert.IsFalse(TUIEffects.IsResolved("HELLO WORLD", 0.01f, 0.02f));
        }

        [Test] public void BlinkingText_Visible()
        {
            string result = TUIEffects.BlinkingText("TEST", 0f);
            Assert.AreEqual("TEST", result);
        }

        [Test] public void BlinkingText_Hidden()
        {
            string result = TUIEffects.BlinkingText("TEST", 0.6f, 0.5f);
            Assert.AreEqual("    ", result);
        }

        [Test] public void TypewriterText_PartialReveal()
        {
            string result = TUIEffects.TypewriterText("ABCDE", 0.12f, 0.05f);
            Assert.AreEqual("AB", result);
        }

        // ── TUIFormat ───────────────────────────────────────────

        [Test] public void Duration_Seconds()
        {
            Assert.AreEqual("45s", TUIFormat.Duration(45f));
        }

        [Test] public void Duration_Minutes()
        {
            Assert.AreEqual("1m 04s", TUIFormat.Duration(64f));
        }

        [Test] public void Duration_Hours()
        {
            Assert.AreEqual("1h 01m 01s", TUIFormat.Duration(3661f));
        }

        [Test] public void Duration_Days()
        {
            Assert.AreEqual("1d 0h 00m", TUIFormat.Duration(86400f));
        }

        [Test] public void Duration_Zero()
        {
            Assert.AreEqual("0s", TUIFormat.Duration(0f));
        }

        [Test] public void TimeColor_SecondsRange()
        {
            Color32 c = TUIFormat.TimeColor(30f);
            // Should be first gradient stop (blue)
            Assert.AreEqual(TUIConfig.Gradient[0].r, c.r);
        }

        [Test] public void TimeColor_HoursRange()
        {
            Color32 c = TUIFormat.TimeColor(7200f);
            Assert.AreEqual(TUIConfig.Gradient[2].r, c.r);
        }

        // ── TUIColors ───────────────────────────────────────────

        [Test] public void Fg_ProducesRichText()
        {
            string result = TUIColors.Fg(255, 0, 128, "test");
            Assert.AreEqual("<color=#FF0080>test</color>", result);
        }

        [Test] public void Bold_WrapsText()
        {
            Assert.AreEqual("<b>x</b>", TUIColors.Bold("x"));
        }

        [Test] public void Dimmed_ProducesAlphaTag()
        {
            string result = TUIColors.Dimmed("dim");
            Assert.IsTrue(result.StartsWith("<color=#"));
            Assert.IsTrue(result.Contains("dim"));
        }

        // ── TUIWidgets ──────────────────────────────────────────

        [Test] public void ProgressBar_AtZero()
        {
            string bar = TUIWidgets.ProgressBar(0f, 10);
            Assert.IsTrue(bar.Contains("0%"));
        }

        [Test] public void ProgressBar_AtHundred()
        {
            string bar = TUIWidgets.ProgressBar(1f, 10);
            Assert.IsTrue(bar.Contains("100%"));
        }

        [Test] public void SpinnerFrame_ReturnsString()
        {
            string frame = TUIWidgets.SpinnerFrame(0f);
            Assert.IsNotNull(frame);
            Assert.IsTrue(frame.Length > 0);
        }

        [Test] public void SignalStrength_Clamped()
        {
            Assert.AreEqual("▁   ", TUIWidgets.SignalStrength(-1));
            Assert.AreEqual("▁▃▅█", TUIWidgets.SignalStrength(99));
        }

        [Test] public void Divider_MinLength()
        {
            string d = TUIWidgets.Divider(1);
            Assert.IsNotNull(d);
        }

        // ── TUIEasing ───────────────────────────────────────────

        [Test] public void Smoothstep_Endpoints()
        {
            Assert.AreEqual(0f, TUIEasing.Smoothstep(0f), 0.001f);
            Assert.AreEqual(1f, TUIEasing.Smoothstep(1f), 0.001f);
        }

        [Test] public void Smoothstep_Midpoint()
        {
            Assert.AreEqual(0.5f, TUIEasing.Smoothstep(0.5f), 0.001f);
        }

        [Test] public void Smootherstep_Endpoints()
        {
            Assert.AreEqual(0f, TUIEasing.Smootherstep(0f), 0.001f);
            Assert.AreEqual(1f, TUIEasing.Smootherstep(1f), 0.001f);
        }

        // ── TUIConfig ───────────────────────────────────────────

        [Test] public void Config_DefaultGradientHas4Stops()
        {
            TUIConfig.Reset();
            Assert.AreEqual(4, TUIConfig.Gradient.Length);
        }

        [Test] public void Config_Reset_RestoresDefaults()
        {
            TUIConfig.Gradient = new Color32[] { new(0, 0, 0, 255) };
            TUIConfig.Reset();
            Assert.AreEqual(4, TUIConfig.Gradient.Length);
        }

        // ── TUILayout ───────────────────────────────────────────

        [Test] public void CenterText_AddsLeftPadding()
        {
            string result = TUILayout.CenterText("AB", 20);
            Assert.IsTrue(result.StartsWith(" "));
        }

        [Test] public void RightAlign_PadsLeft()
        {
            string result = TUILayout.RightAlign("X", 20);
            Assert.IsTrue(result.Length > 1);
            Assert.IsTrue(result.EndsWith("X"));
        }
    }
}
