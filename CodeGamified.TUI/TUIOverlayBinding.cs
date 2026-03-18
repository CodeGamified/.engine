// ═══════════════════════════════════════════════════════════
//  TUIOverlayBinding — Declarative slider & button overlays
//  Games describe overlays once; engine handles create,
//  reposition, sync, and visibility automatically.
//
//  Supports multiple sliders per row (unlike TerminalRow's
//  single-slider API) by creating sliders directly as children
//  of the row Transform.
//
//  Usage (game code):
//    _overlays = new TUIOverlayBinding();
//    _overlays.Slider(row: 1, col: 5,
//        get: () => Settings.Volume,
//        set: v => Settings.Volume = v);
//    _overlays.Button(row: 3, col: 0, charStart: 2, charWidth: 10,
//        callback: _ => LoadScript());
//    _overlays.Apply(rows, colPositions, totalChars);  // on resize
//    _overlays.Sync();                                 // each frame
// ═══════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Declarative overlay manager. Games register slider/button bindings
    /// by (row, column). The manager handles creation, repositioning on
    /// column drag/resize, value sync each frame, and visibility toggling.
    /// </summary>
    public class TUIOverlayBinding
    {
        // ── Binding descriptors ─────────────────────────────────

        public class SliderBinding
        {
            public int Row;
            public int Column;
            public int BarOffset;
            public int BarRightPad;
            public int MinWidth;
            public Func<float> Get;
            public Action<float> Set;
            /// <summary>When true, skip Get() sync in Sync() — for sliders where
            /// dragging and sync would fight (e.g. font size that triggers rebuild).</summary>
            public bool SkipSync;
            /// <summary>Normalized step for auto-generated [-][+] buttons (0-1 scale).</summary>
            public float Step;
            /// <summary>When true (default), auto-create [-] and [+] button overlays
            /// that step the slider value by Step.</summary>
            public bool AutoButtons;

            // Live instance
            internal Slider Slider;
        }

        public class ButtonBinding
        {
            public int Row;
            /// <summary>Column index for relative positioning.</summary>
            public int Column;
            /// <summary>Character offset within column (Column >= 0) or absolute position (Column == -1).</summary>
            public int CharStart;
            public int CharWidth;
            public Action<int> Callback;
            /// <summary>When set, overrides CharStart/CharWidth.
            /// Args: (colStart, colWidth) → (charStart, charWidth).</summary>
            public Func<int, int, (int start, int width)> LayoutFunc;

            // Live instance
            internal Button Button;
        }

        readonly List<SliderBinding> _sliderBindings = new();
        readonly List<ButtonBinding> _buttonBindings = new();
        bool _created;

        // Grouped buttons by row — needed for TerminalRow batch API
        Dictionary<int, List<ButtonBinding>> _buttonsByRow;

        // ── Standard button layouts matching AdaptiveSliderRow ──

        /// <summary>Layout for [-] button: colStart + 1, width 3.</summary>
        public static readonly Func<int, int, (int start, int width)> MinusBtnLayout =
            (cs, cw) => (cs + 1, 3);

        /// <summary>Layout for [+] button: 3 chars from column end.</summary>
        public static readonly Func<int, int, (int start, int width)> PlusBtnLayout =
            (cs, cw) => { int off = Mathf.Max(5, cw - 3); return (cs + off, Mathf.Max(3, cw - off)); };

        // ═══════════════════════════════════════════════════════════
        //  REGISTRATION API
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Register a slider overlay for a specific row and column.
        /// barOffset/barRightPad are in characters relative to column start/end.
        /// </summary>
        public SliderBinding Slider(int row, int column,
            Func<float> get, Action<float> set,
            int barOffset = 9, int barRightPad = 9, int minWidth = 4,
            bool skipSync = false, float step = 0.1f, bool autoButtons = true)
        {
            var sb = new SliderBinding
            {
                Row = row, Column = column,
                BarOffset = barOffset, BarRightPad = barRightPad,
                MinWidth = minWidth,
                Get = get, Set = set, SkipSync = skipSync,
                Step = step, AutoButtons = autoButtons
            };
            _sliderBindings.Add(sb);

            // Auto-register [-] and [+] button overlays for this slider
            if (autoButtons)
            {
                var binding = sb;
                Button(row, column, MinusBtnLayout,
                    _ => { if (binding.Get != null && binding.Set != null)
                               binding.Set(Mathf.Clamp01(binding.Get() - binding.Step)); });
                Button(row, column, PlusBtnLayout,
                    _ => { if (binding.Get != null && binding.Set != null)
                               binding.Set(Mathf.Clamp01(binding.Get() + binding.Step)); });
            }

            return sb;
        }

        /// <summary>
        /// Register a button overlay at a character offset within a column.
        /// </summary>
        public ButtonBinding Button(int row, int column, int charStart, int charWidth,
            Action<int> callback)
        {
            var bb = new ButtonBinding
            {
                Row = row, Column = column, CharStart = charStart,
                CharWidth = charWidth, Callback = callback
            };
            _buttonBindings.Add(bb);
            return bb;
        }

        /// <summary>
        /// Register a button overlay at an absolute character position.
        /// </summary>
        public ButtonBinding ButtonAt(int row, int charStart, int charWidth,
            Action<int> callback)
        {
            var bb = new ButtonBinding
            {
                Row = row, Column = -1, CharStart = charStart,
                CharWidth = charWidth, Callback = callback
            };
            _buttonBindings.Add(bb);
            return bb;
        }

        /// <summary>
        /// Register a button with dynamic layout based on column geometry.
        /// layoutFunc receives (colStart, colWidth) and returns (charStart, charWidth).
        /// </summary>
        public ButtonBinding Button(int row, int column,
            Func<int, int, (int start, int width)> layoutFunc, Action<int> callback)
        {
            var bb = new ButtonBinding
            {
                Row = row, Column = column, LayoutFunc = layoutFunc, Callback = callback
            };
            _buttonBindings.Add(bb);
            return bb;
        }

        /// <summary>Clear all bindings and destroy created overlays.</summary>
        public void Clear()
        {
            foreach (var sb in _sliderBindings)
                if (sb.Slider != null) UnityEngine.Object.Destroy(sb.Slider.gameObject);
            foreach (var bb in _buttonBindings)
                if (bb.Button != null) UnityEngine.Object.Destroy(bb.Button.gameObject);
            _sliderBindings.Clear();
            _buttonBindings.Clear();
            _buttonsByRow = null;
            _created = false;
        }

        // ═══════════════════════════════════════════════════════════
        //  APPLY — create or reposition all overlays
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Create or reposition all overlays to match current layout.
        /// Call after column drag, resize, or initial setup.
        /// </summary>
        public void Apply(IReadOnlyList<TerminalRow> rows, int[] colPositions, int totalChars)
        {
            if (!_created)
            {
                CreateAll(rows, colPositions, totalChars);
                _created = true;
            }
            else
            {
                RepositionAll(rows, colPositions, totalChars);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SYNC — update slider values each frame
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Sync all slider overlays to their current Get() values.
        /// Call each frame in Update/Render.
        /// </summary>
        public void Sync()
        {
            foreach (var sb in _sliderBindings)
            {
                if (sb.Slider == null || sb.Get == null || sb.SkipSync) continue;
                sb.Slider.SetValueWithoutNotify(sb.Get());
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  CREATE — first-time instantiation
        // ═══════════════════════════════════════════════════════════

        void CreateAll(IReadOnlyList<TerminalRow> rows, int[] colPositions, int totalChars)
        {
            // ── Sliders (created directly; supports multiple per row) ──
            foreach (var sb in _sliderBindings)
            {
                if (sb.Row >= rows.Count) continue;
                var row = rows[sb.Row];
                ComputeSliderLayout(sb, colPositions, totalChars,
                    out int startChar, out int widthChars, out bool visible);

                sb.Slider = CreateSliderDirect(row.transform, row.CharWidth,
                    startChar, Mathf.Max(1, widthChars));

                // Always wire listener so sliders that start hidden still
                // work when they become visible after resize / column drag.
                if (sb.Get != null) sb.Slider.SetValueWithoutNotify(sb.Get());
                if (sb.Set != null)
                {
                    var setter = sb.Set;
                    sb.Slider.onValueChanged.AddListener(v => setter(v));
                }

                if (!visible || widthChars < sb.MinWidth)
                    sb.Slider.gameObject.SetActive(false);
            }

            // ── Buttons (grouped by row for TerminalRow batch API) ──
            _buttonsByRow = new Dictionary<int, List<ButtonBinding>>();
            foreach (var bb in _buttonBindings)
            {
                if (!_buttonsByRow.TryGetValue(bb.Row, out var list))
                {
                    list = new List<ButtonBinding>();
                    _buttonsByRow[bb.Row] = list;
                }
                list.Add(bb);
            }

            foreach (var kvp in _buttonsByRow)
            {
                int rowIdx = kvp.Key;
                if (rowIdx >= rows.Count) continue;
                var row = rows[rowIdx];
                var group = kvp.Value;

                var starts = new int[group.Count];
                var widths = new int[group.Count];
                var cbs = new Action<int>[group.Count];

                for (int i = 0; i < group.Count; i++)
                {
                    var (s, w) = ResolveButtonLayout(group[i], colPositions, totalChars);
                    starts[i] = s;
                    widths[i] = w;
                    cbs[i] = group[i].Callback;
                }

                var btns = row.CreateButtonOverlays(starts, widths, cbs);
                for (int i = 0; i < group.Count && i < btns.Length; i++)
                    group[i].Button = btns[i];
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  REPOSITION — on column drag / resize
        // ═══════════════════════════════════════════════════════════

        void RepositionAll(IReadOnlyList<TerminalRow> rows, int[] colPositions, int totalChars)
        {
            // ── Sliders ──
            foreach (var sb in _sliderBindings)
            {
                if (sb.Slider == null) continue;
                ComputeSliderLayout(sb, colPositions, totalChars,
                    out int startChar, out int widthChars, out bool visible);

                if (visible && widthChars >= sb.MinWidth)
                {
                    sb.Slider.gameObject.SetActive(true);
                    float cw = sb.Row < rows.Count ? rows[sb.Row].CharWidth : 10f;
                    var rect = sb.Slider.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(startChar * cw, 0);
                        rect.sizeDelta = new Vector2(widthChars * cw, 0);
                    }
                }
                else
                {
                    sb.Slider.gameObject.SetActive(false);
                }
            }

            // ── Buttons ──
            if (_buttonsByRow == null) return;
            foreach (var kvp in _buttonsByRow)
            {
                int rowIdx = kvp.Key;
                if (rowIdx >= rows.Count) continue;
                var row = rows[rowIdx];
                var group = kvp.Value;

                var starts = new int[group.Count];
                var widths = new int[group.Count];
                for (int i = 0; i < group.Count; i++)
                {
                    var (s, w) = ResolveButtonLayout(group[i], colPositions, totalChars);
                    starts[i] = s;
                    widths[i] = w;
                }
                row.RepositionButtonOverlays(starts, widths);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SLIDER FACTORY (multi-per-row, bypasses TerminalRow limit)
        // ═══════════════════════════════════════════════════════════

        static Slider CreateSliderDirect(Transform parent, float charWidth,
            int startChar, int widthChars)
        {
            var sliderGO = new GameObject("OverlaySlider");
            sliderGO.transform.SetParent(parent, false);

            var rect = sliderGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(startChar * charWidth, 0);
            rect.sizeDelta = new Vector2(widthChars * charWidth, 0);

            var bgImg = sliderGO.AddComponent<Image>();
            bgImg.color = new Color(0, 0, 0, 0.01f);

            var s = sliderGO.AddComponent<Slider>();
            s.minValue = 0f;
            s.maxValue = 1f;
            s.wholeNumbers = false;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;

            // Fill area
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGO.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.2f);
            fillAreaRect.anchorMax = new Vector2(1, 0.8f);
            fillAreaRect.offsetMin = new Vector2(2, 0);
            fillAreaRect.offsetMax = new Vector2(-2, 0);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.2f, 0.7f, 0.4f, 0.01f);
            fillImg.raycastTarget = false;
            s.fillRect = fillRect;

            // Handle
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(sliderGO.transform, false);
            var handleAreaRect = handleArea.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(5, 0);
            handleAreaRect.offsetMax = new Vector2(-5, 0);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(10, 0);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = new Color(1, 1, 1, 0.01f);

            s.targetGraphic = handleImg;
            s.handleRect = handleRect;

            return s;
        }

        // ═══════════════════════════════════════════════════════════
        //  LAYOUT HELPERS
        // ═══════════════════════════════════════════════════════════

        static void ComputeSliderLayout(SliderBinding sb, int[] colPositions, int totalChars,
            out int startChar, out int widthChars, out bool visible)
        {
            int colStart = sb.Column < colPositions.Length ? colPositions[sb.Column] : 0;
            int colEnd = sb.Column + 1 < colPositions.Length
                ? colPositions[sb.Column + 1] : totalChars;
            int colWidth = colEnd - colStart;

            startChar = colStart + sb.BarOffset;
            widthChars = Mathf.Max(0, colWidth - sb.BarOffset - sb.BarRightPad);
            visible = widthChars >= sb.MinWidth;
        }

        static (int start, int width) ResolveButtonLayout(ButtonBinding bb, int[] colPositions, int totalChars)
        {
            int colStart = bb.Column >= 0 && bb.Column < colPositions.Length
                ? colPositions[bb.Column] : 0;
            int colEnd = bb.Column + 1 < colPositions.Length
                ? colPositions[bb.Column + 1] : totalChars;
            int colWidth = colEnd - colStart;

            if (bb.LayoutFunc != null)
                return bb.LayoutFunc(colStart, colWidth);

            int s = bb.Column >= 0 ? colStart + bb.CharStart : bb.CharStart;
            return (s, bb.CharWidth);
        }
    }
}
