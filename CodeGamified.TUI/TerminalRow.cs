// ═══════════════════════════════════════════════════════════
//  TerminalRow — Single row in a monospace terminal grid
//  One TMP_Text per row → precise overlay alignment
//  Unified: SRUI clean factory + BNUI slider/button overlays
// ═══════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CodeGamified.TUI
{
    /// <summary>
    /// A single row in a terminal grid. Each row is its own GameObject
    /// with a TMP_Text, enabling precise character-level positioning.
    ///
    /// Column modes (all opt-in, mutually exclusive):
    ///   • Dual-column:  left + right text (split-pane list + detail)
    ///   • Triple-column: left/center/right justified (status bars)
    ///   • Three-panel:  3 fixed-position left-aligned panels (code debugger)
    ///
    /// Overlays (opt-in, from BitNaughts):
    ///   • Slider overlay for invisible input over TUI progress bars
    ///   • Button overlays for invisible hit areas over TUI text
    /// </summary>
    public class TerminalRow : MonoBehaviour
    {
        [SerializeField] TMP_Text textComponent;
        [SerializeField] TMP_Text rightTextComponent;
        [SerializeField] TMP_Text centerTextComponent;
        [SerializeField] RectTransform rectTransform;

        float charWidth = 10f;
        float rowHeight = 18f;
        int rowIndex;

        // Column mode state
        bool dualColumnMode;
        bool tripleColumnMode;
        bool threePanelActive;
        int dividerCharPos = 24;
        int col1Chars, col2Chars, col3Chars;

        // Overlay state
        Slider slider;
        Button[] buttons;
        Button[] rightButtons;
        bool buttonsCreated;
        bool rightButtonsCreated;
        bool debugVisible;

        // ── Public accessors ────────────────────────────────────

        public TMP_Text Text => textComponent;
        public TMP_Text RightText => rightTextComponent;
        public TMP_Text CenterText => centerTextComponent;
        public RectTransform Rect => rectTransform;
        public float CharWidth => charWidth;
        public float RowHeight => rowHeight;
        public int Index => rowIndex;
        public Slider Slider => slider;
        public bool HasSlider => slider != null;
        public bool HasButtons => buttonsCreated && buttons != null && buttons.Length > 0;
        public bool IsDualColumn => dualColumnMode;
        public bool IsTripleColumn => tripleColumnMode;
        public bool IsThreePanel => threePanelActive;
        public int DividerPosition => dividerCharPos;

        // ── Factory ─────────────────────────────────────────────

        /// <summary>Create a row as child of parent at vertical index.</summary>
        public static TerminalRow Create(
            Transform parent, TMP_FontAsset font, float fontSize,
            int index, bool debugOverlays = false)
        {
            var go = new GameObject($"Row_{index:D2}");
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);

            var row = go.AddComponent<TerminalRow>();
            row.rowIndex = index;
            row.debugVisible = debugOverlays;
            row.Setup(font, fontSize);
            return row;
        }

        void Setup(TMP_FontAsset font, float fontSize)
        {
            charWidth = fontSize * 0.55f; // monospace ratio
            rowHeight = fontSize * 1.1f;

            // RectTransform — full-width, stacked from top
            rectTransform = GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(1, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(0, -rowIndex * rowHeight);
            rectTransform.sizeDelta = new Vector2(0, rowHeight);

            // TMP_Text (panel 1 / left column)
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(transform, false);

            textComponent = textGO.AddComponent<TextMeshProUGUI>();
            textComponent.font = font;
            textComponent.fontSize = fontSize;
            textComponent.alignment = TextAlignmentOptions.Left;
            textComponent.enableWordWrapping = false;
            textComponent.overflowMode = TextOverflowModes.Overflow;
            textComponent.richText = true;
            textComponent.color = Color.white;
            textComponent.raycastTarget = false;

            var tr = textComponent.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        public void SetCharWidth(float width) => charWidth = width;

        // ═══════════════════════════════════════════════════════════
        //  TEXT API
        // ═══════════════════════════════════════════════════════════

        /// <summary>Set row text (rich-text OK). Truncates to row width.</summary>
        public void SetText(string text)
        {
            if (textComponent != null)
                textComponent.text = TruncateRichText(text, GetTotalCharacters());
        }

        /// <summary>Set the right column text (dual or triple-column mode).</summary>
        public void SetRightText(string text)
        {
            if (rightTextComponent != null)
                rightTextComponent.text = text;
        }

        /// <summary>Set the center column text (triple-column mode only).</summary>
        public void SetCenterText(string text)
        {
            if (centerTextComponent != null)
                centerTextComponent.text = text;
        }

        /// <summary>Set both columns at once (dual-column mode).</summary>
        public void SetBothTexts(string leftText, string rightText)
        {
            SetText(leftText);
            SetRightText(rightText);
        }

        /// <summary>Set all three columns at once (triple-column mode).</summary>
        public void SetTripleTexts(string left, string center, string right)
        {
            SetText(left);
            SetCenterText(center);
            SetRightText(right);
        }

        /// <summary>Set all three panels at once (three-panel mode, truncates per column).</summary>
        public void SetThreePanelTexts(string p1, string p2, string p3)
        {
            if (textComponent != null)
                textComponent.text = TruncateRichText(p1, col1Chars);
            if (centerTextComponent != null)
                centerTextComponent.text = TruncateRichText(p2, col2Chars);
            if (rightTextComponent != null)
                rightTextComponent.text = TruncateRichText(p3, col3Chars);
        }

        /// <summary>Clear all columns.</summary>
        public void Clear()
        {
            if (textComponent != null) textComponent.text = "";
            if (rightTextComponent != null) rightTextComponent.text = "";
            if (centerTextComponent != null) centerTextComponent.text = "";
        }

        // ═══════════════════════════════════════════════════════════
        //  MEASUREMENT
        // ═══════════════════════════════════════════════════════════

        /// <summary>Total characters that fit in current width.</summary>
        public int GetTotalCharacters()
        {
            float w = rectTransform != null ? rectTransform.rect.width : 0f;
            if (w <= 0f)
            {
                var parent = transform.parent?.GetComponent<RectTransform>();
                if (parent != null) w = parent.rect.width;
            }
            return w > 0 ? Mathf.FloorToInt(w / charWidth) : 50;
        }

        /// <summary>Left column width in characters (dual-column mode).</summary>
        public int GetLeftColumnWidth() => dividerCharPos - 1;

        /// <summary>Right column width in characters (dual-column mode).</summary>
        public int GetRightColumnWidth() => GetTotalCharacters() - dividerCharPos - 1;

        /// <summary>Reposition row to a new vertical index.</summary>
        public void Reposition(int newIndex)
        {
            rowIndex = newIndex;
            rectTransform.anchoredPosition = new Vector2(0, -rowIndex * rowHeight);
        }

        // ═══════════════════════════════════════════════════════════
        //  DUAL-COLUMN MODE (split-pane list + detail)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Enable or disable dual-column mode for split-pane layouts.
        /// </summary>
        public void SetDualColumnMode(bool enabled, int dividerPos = -1)
        {
            dualColumnMode = enabled;
            tripleColumnMode = false;
            threePanelActive = false;

            if (dividerPos < 0)
                dividerCharPos = GetTotalCharacters() / 2;
            else
                dividerCharPos = dividerPos;

            if (enabled && rightTextComponent == null)
                rightTextComponent = CreateColumnText("RightText",
                    TextAlignmentOptions.Left, dividerCharPos * charWidth);

            if (rightTextComponent != null)
            {
                rightTextComponent.gameObject.SetActive(enabled);
                if (enabled)
                    rightTextComponent.rectTransform.offsetMin = new Vector2(dividerCharPos * charWidth, 0);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  TRIPLE-COLUMN MODE (left / center / right justified)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Enable triple-column mode with left/center/right justified text.
        /// Useful for status bars and headers.
        /// </summary>
        public void SetTripleColumnMode(bool enabled, float padding = 10f)
        {
            tripleColumnMode = enabled;
            dualColumnMode = false;
            threePanelActive = false;

            if (enabled)
            {
                // Left text — already exists, set alignment
                if (textComponent != null)
                {
                    textComponent.alignment = TextAlignmentOptions.Left;
                    var lr = textComponent.rectTransform;
                    lr.anchorMin = Vector2.zero;
                    lr.anchorMax = Vector2.one;
                    lr.offsetMin = new Vector2(padding, 0);
                    lr.offsetMax = new Vector2(-padding, 0);
                }

                // Center text
                if (centerTextComponent == null)
                    centerTextComponent = CreateColumnText("CenterText",
                        TextAlignmentOptions.Center, 0, 0);
                centerTextComponent.gameObject.SetActive(true);

                // Right text
                if (rightTextComponent == null)
                {
                    rightTextComponent = CreateColumnText("RightText",
                        TextAlignmentOptions.Right, padding, -padding);
                }
                else
                {
                    rightTextComponent.alignment = TextAlignmentOptions.Right;
                    var rr = rightTextComponent.rectTransform;
                    rr.anchorMin = Vector2.zero;
                    rr.anchorMax = Vector2.one;
                    rr.offsetMin = new Vector2(padding, 0);
                    rr.offsetMax = new Vector2(-padding, 0);
                }
                rightTextComponent.gameObject.SetActive(true);
            }
            else
            {
                if (centerTextComponent != null) centerTextComponent.gameObject.SetActive(false);
                if (rightTextComponent != null) rightTextComponent.gameObject.SetActive(false);
                if (textComponent != null)
                    textComponent.alignment = TextAlignmentOptions.Left;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  THREE-PANEL MODE (fixed-position debugger columns)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Enable three fixed-position left-aligned panels.
        /// Unlike triple-column (justified), all panels are left-aligned
        /// at fixed character positions — ideal for code debugger views.
        /// </summary>
        public void SetThreePanelMode(bool enabled, int col2CharPos, int col3CharPos)
        {
            threePanelActive = enabled;
            dualColumnMode = false;
            tripleColumnMode = false;

            if (enabled)
            {
                int total = GetTotalCharacters();
                col1Chars = col2CharPos;
                col2Chars = col3CharPos - col2CharPos;
                col3Chars = total - col3CharPos;

                if (centerTextComponent == null)
                    centerTextComponent = CreatePanelText("Panel2", col2CharPos);
                else
                    centerTextComponent.rectTransform.offsetMin = new Vector2(col2CharPos * charWidth, 0);

                if (rightTextComponent == null)
                    rightTextComponent = CreatePanelText("Panel3", col3CharPos);
                else
                    rightTextComponent.rectTransform.offsetMin = new Vector2(col3CharPos * charWidth, 0);

                centerTextComponent.gameObject.SetActive(true);
                rightTextComponent.gameObject.SetActive(true);
            }
            else
            {
                if (centerTextComponent != null) centerTextComponent.gameObject.SetActive(false);
                if (rightTextComponent != null) rightTextComponent.gameObject.SetActive(false);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SLIDER OVERLAY (opt-in, BitNaughts hybrid TUI+GUI)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Create an invisible slider overlay at the specified character position.
        /// The TUI progress bar text renders underneath; the slider captures input.
        /// </summary>
        public Slider CreateSliderOverlay(int startChar, int widthChars = 22)
        {
            if (slider != null) return slider;

            var sliderGO = new GameObject("SliderOverlay");
            sliderGO.transform.SetParent(transform, false);

            var rect = sliderGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(startChar * charWidth, 0);
            rect.sizeDelta = new Vector2(widthChars * charWidth, 0);

            var bgImg = sliderGO.AddComponent<Image>();
            bgImg.color = debugVisible
                ? new Color(0.15f, 0.15f, 0.2f, 0.7f)
                : new Color(0, 0, 0, 0.01f);

            slider = sliderGO.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.direction = Slider.Direction.LeftToRight;

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
            fillImg.color = debugVisible
                ? new Color(0.2f, 0.7f, 0.4f, 0.8f)
                : new Color(0.2f, 0.7f, 0.4f, 0.01f);
            fillImg.raycastTarget = false;
            slider.fillRect = fillRect;

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
            handleImg.color = debugVisible
                ? new Color(0.4f, 1f, 0.6f, 1f)
                : new Color(1, 1, 1, 0.01f);

            slider.targetGraphic = handleImg;
            slider.handleRect = handleRect;

            return slider;
        }

        /// <summary>Reposition an existing slider overlay to new character coordinates.</summary>
        public void RepositionSliderOverlay(int startChar, int widthChars = 22)
        {
            if (slider == null) return;
            var rect = slider.GetComponent<RectTransform>();
            if (rect == null) return;
            rect.anchoredPosition = new Vector2(startChar * charWidth, 0);
            rect.sizeDelta = new Vector2(widthChars * charWidth, 0);
        }

        /// <summary>Show or hide the slider overlay.</summary>
        public void SetSliderVisible(bool visible)
        {
            if (slider != null) slider.gameObject.SetActive(visible);
        }

        // ═══════════════════════════════════════════════════════════
        //  BUTTON OVERLAYS (opt-in, BitNaughts)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Create invisible button overlays at specified character positions.
        /// Only creates buttons once — use UpdateButtonHighlight to change selection.
        /// </summary>
        public Button[] CreateButtonOverlays(
            int[] startChars, int[] widthChars,
            System.Action<int>[] callbacks, bool forceRecreate = false)
        {
            if (buttonsCreated && !forceRecreate && buttons != null && buttons.Length == startChars.Length)
                return buttons;

            if (buttons != null)
                foreach (var btn in buttons)
                    if (btn != null) DestroyImmediate(btn.gameObject);

            buttons = new Button[startChars.Length];

            for (int i = 0; i < startChars.Length; i++)
            {
                var btnGO = new GameObject($"Button_{i}");
                btnGO.transform.SetParent(transform, false);

                var rect = btnGO.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2(startChars[i] * charWidth, 0);
                rect.sizeDelta = new Vector2(widthChars[i] * charWidth, 0);

                var img = btnGO.AddComponent<Image>();
                img.color = debugVisible
                    ? new Color(0.2f, 0.3f, 0.5f, 0.5f)
                    : new Color(0, 0, 0, 0.01f);

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = img;

                int capturedIndex = i;
                if (callbacks != null && capturedIndex < callbacks.Length && callbacks[capturedIndex] != null)
                {
                    var cb = callbacks[capturedIndex];
                    btn.onClick.AddListener(() => cb(capturedIndex));
                }

                buttons[i] = btn;
            }

            buttonsCreated = true;
            return buttons;
        }

        /// <summary>Update button visual state (highlight selected).</summary>
        public void UpdateButtonHighlight(int selectedIndex)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var img = buttons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = i == selectedIndex
                        ? new Color(0.2f, 0.6f, 0.3f, 0.9f)
                        : debugVisible
                            ? new Color(0.15f, 0.15f, 0.15f, 0.6f)
                            : new Color(0, 0, 0, 0.01f);
                }
            }
        }

        /// <summary>Reposition existing button overlays to updated character positions.</summary>
        public void RepositionButtonOverlays(int[] startChars, int[] widthChars)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length && i < startChars.Length; i++)
            {
                if (buttons[i] == null) continue;
                var rect = buttons[i].GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.anchoredPosition = new Vector2(startChars[i] * charWidth, 0);
                rect.sizeDelta = new Vector2(widthChars[i] * charWidth, 0);
            }
        }

        /// <summary>
        /// Create button overlays for the right column (dual-column mode).
        /// Character positions are relative to the right column start.
        /// </summary>
        public Button[] CreateRightButtonOverlays(
            int[] startChars, int[] widthChars,
            System.Action<int>[] callbacks, bool forceRecreate = false)
        {
            if (!dualColumnMode) return null;
            if (rightButtonsCreated && !forceRecreate && rightButtons != null &&
                rightButtons.Length == startChars.Length)
                return rightButtons;

            if (rightButtons != null)
                foreach (var btn in rightButtons)
                    if (btn != null) DestroyImmediate(btn.gameObject);

            rightButtons = new Button[startChars.Length];

            for (int i = 0; i < startChars.Length; i++)
            {
                var btnGO = new GameObject($"RightButton_{i}");
                btnGO.transform.SetParent(transform, false);

                var rect = btnGO.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2((dividerCharPos + startChars[i]) * charWidth, 0);
                rect.sizeDelta = new Vector2(widthChars[i] * charWidth, 0);

                var img = btnGO.AddComponent<Image>();
                img.color = debugVisible
                    ? new Color(0.5f, 0.3f, 0.2f, 0.5f)
                    : new Color(0, 0, 0, 0.01f);

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = img;

                int capturedIndex = i;
                if (callbacks != null && capturedIndex < callbacks.Length && callbacks[capturedIndex] != null)
                {
                    var cb = callbacks[capturedIndex];
                    btn.onClick.AddListener(() => cb(capturedIndex));
                }

                rightButtons[i] = btn;
            }

            rightButtonsCreated = true;
            return rightButtons;
        }

        /// <summary>Update right column button highlight state.</summary>
        public void UpdateRightButtonHighlight(int selectedIndex)
        {
            if (rightButtons == null) return;
            for (int i = 0; i < rightButtons.Length; i++)
            {
                if (rightButtons[i] == null) continue;
                var img = rightButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    img.color = i == selectedIndex
                        ? new Color(0.6f, 0.4f, 0.2f, 0.9f)
                        : debugVisible
                            ? new Color(0.15f, 0.15f, 0.15f, 0.6f)
                            : new Color(0, 0, 0, 0.01f);
                }
            }
        }

        /// <summary>Reposition existing right-column button overlays to updated character positions.</summary>
        public void RepositionRightButtonOverlays(int[] startChars, int[] widthChars)
        {
            if (rightButtons == null) return;
            for (int i = 0; i < rightButtons.Length && i < startChars.Length; i++)
            {
                if (rightButtons[i] == null) continue;
                var rect = rightButtons[i].GetComponent<RectTransform>();
                if (rect == null) continue;
                rect.anchoredPosition = new Vector2((dividerCharPos + startChars[i]) * charWidth, 0);
                rect.sizeDelta = new Vector2(widthChars[i] * charWidth, 0);
            }
        }

        /// <summary>
        /// Create button overlays for triple-column mode's RIGHT column.
        /// Positions are relative to the right TMP_Text's rendered text bounds.
        /// Since right-aligned text grows leftward, we use preferredWidth to find text start.
        /// </summary>
        public Button[] CreateTripleColumnRightButtons(
            int[] charOffsets, int[] widthChars,
            System.Action<int>[] callbacks, float padding = 10f,
            bool forceRecreate = false)
        {
            if (!tripleColumnMode || rightTextComponent == null) return null;
            if (rightButtonsCreated && !forceRecreate && rightButtons != null &&
                rightButtons.Length == charOffsets.Length)
                return rightButtons;

            if (rightButtons != null)
                foreach (var btn in rightButtons)
                    if (btn != null) DestroyImmediate(btn.gameObject);

            rightButtons = new Button[charOffsets.Length];

            float textWidth = rightTextComponent.preferredWidth;
            float rowWidth = rectTransform != null ? rectTransform.rect.width : 0f;
            if (rowWidth <= 0f)
            {
                var parentRect = transform.parent?.GetComponent<RectTransform>();
                if (parentRect != null) rowWidth = parentRect.rect.width;
            }

            float textStartX = rowWidth - padding - textWidth;

            for (int i = 0; i < charOffsets.Length; i++)
            {
                var btnGO = new GameObject($"RightBtn_{i}");
                btnGO.transform.SetParent(transform, false);

                var rect = btnGO.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
                rect.anchoredPosition = new Vector2(
                    textStartX + charOffsets[i] * charWidth, 0);
                rect.sizeDelta = new Vector2(widthChars[i] * charWidth, 0);

                var img = btnGO.AddComponent<Image>();
                img.color = debugVisible
                    ? new Color(0.5f, 0.2f, 0.5f, 0.5f)
                    : new Color(0, 0, 0, 0.01f);

                var btn = btnGO.AddComponent<Button>();
                btn.targetGraphic = img;

                int capturedIndex = i;
                if (callbacks != null && capturedIndex < callbacks.Length &&
                    callbacks[capturedIndex] != null)
                {
                    var cb = callbacks[capturedIndex];
                    btn.onClick.AddListener(() => cb(capturedIndex));
                }

                rightButtons[i] = btn;
            }

            rightButtonsCreated = true;
            return rightButtons;
        }

        // ═══════════════════════════════════════════════════════════
        //  INTERNAL HELPERS
        // ═══════════════════════════════════════════════════════════

        TMP_Text CreateColumnText(string name, TextAlignmentOptions align,
            float offsetMinX, float offsetMaxX = 0f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = textComponent.font;
            tmp.fontSize = textComponent.fontSize;
            tmp.alignment = align;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(offsetMinX, 0);
            rt.offsetMax = new Vector2(offsetMaxX, 0);

            return tmp;
        }

        TMP_Text CreatePanelText(string name, int charPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = textComponent.font;
            tmp.fontSize = textComponent.fontSize;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            var rt = tmp.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(charPos * charWidth, 0);
            rt.offsetMax = Vector2.zero;

            return tmp;
        }

        static string TruncateRichText(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || maxChars <= 0) return text ?? "";

            int visible = 0;
            bool inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '<') { inTag = true; continue; }
                if (text[i] == '>') { inTag = false; continue; }
                if (!inTag) visible++;
            }
            if (visible <= maxChars) return text;

            var sb = new System.Text.StringBuilder(text.Length);
            int count = 0;
            int cutAt = maxChars - 3;
            if (cutAt < 1) cutAt = 1;
            inTag = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<') { inTag = true; sb.Append(c); continue; }
                if (c == '>') { inTag = false; sb.Append(c); continue; }
                if (inTag) { sb.Append(c); continue; }
                if (count < cutAt) { sb.Append(c); count++; }
                else if (count == cutAt) { sb.Append("..."); break; }
            }
            return sb.ToString();
        }
    }
}
