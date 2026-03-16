// ═══════════════════════════════════════════════════════════
//  TerminalWindow — Abstract base for all terminal panels
//  Row-based layout powered by CodeGamified.TUI primitives
//  Unified: SRUI dynamic resize + BNUI dual-column init
// ═══════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Abstract base class for terminal panels. Creates a grid of TerminalRow objects,
    /// handles dynamic resize, animation markers, and provides write/clear API.
    ///
    /// Layout (variable height, default 20 rows):
    /// ┌─────────────────────────────────────┐
    /// │ ROW 0:  Header (subtitle / title)   │
    /// │ ROW 1…N-3: Content area             │
    /// │ ROW N-2: ── separator ──            │
    /// │ ROW N-1: Actions / hints            │
    /// └─────────────────────────────────────┘
    ///
    /// Hover behavior: ROW 0 shows subtitle by default.
    /// When the mouse hovers over the panel, ROW 0 shows the title instead.
    /// For multi-column panels, hover is tracked per-column via _hoveredColumn.
    ///
    /// Subclass and override Render() for custom per-frame rendering.
    /// Use InitializeDualColumns() for split-pane layouts (from BitNaughts).
    /// </summary>
    public abstract class TerminalWindow : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        // ── Layout constants ────────────────────────────────────
        public const int ROW_HEADER = 0;
        public const int ROW_CONTENT_START = 1;

        [Header("UI References")]
        [SerializeField] protected TMP_Text contentText;     // seed font/size
        [SerializeField] protected Image backgroundImage;

        [Header("Window")]
        [SerializeField] protected string windowTitle = "TERMINAL";
        [SerializeField] protected string windowSubtitle = "";
        [SerializeField] protected int totalRows = 20;

        /// <summary>Set the window title shown in the header row.</summary>
        public void SetTitle(string title) => windowTitle = title;

        /// <summary>Set the subtitle shown when the panel is not hovered.</summary>
        public void SetSubtitle(string subtitle) => windowSubtitle = subtitle;

        // ── Hover state ─────────────────────────────────────────
        protected bool _isHovered;

        /// <summary>
        /// Index of the column currently under the mouse pointer (-1 if none).
        /// Updated each frame when _isHovered is true and _hoverColumnPositions is set.
        /// </summary>
        protected int _hoveredColumn = -1;

        /// <summary>
        /// Column boundaries in character positions (e.g. {0, col2Start, col3Start}).
        /// Set by subclasses to enable per-column hover detection.
        /// </summary>
        protected int[] _hoverColumnPositions;

        [Header("Blur (Ultra quality, URP only)")]
        [Tooltip("Material using CodeGamified/UIBackgroundBlur shader. Leave empty for auto-setup.")]
        [SerializeField] protected Material blurMaterial;
        bool _blurEnabled;

        /// <summary>
        /// Global blur material shared by all terminals without a manual override.
        /// Set automatically by TUIBlurManager when CodeGamified.TUI.Blur is present.
        /// </summary>
        public static Material SharedBlurMaterial { get; set; }
        static bool _sharedBlurEnabled;

        // Row grid
        protected List<TerminalRow> rows = new();
        protected bool rowsReady;
        protected int totalChars = 50;
        protected float windowAge;

        // Resize tracking
        float lastMeasuredWidth;
        float lastMeasuredHeight;
        const float RESIZE_THRESHOLD = 2f;

        // Scrollback (for streaming terminals like event log)
        protected List<string> scrollback = new(128);

        // Minimize state
        bool _minimized;
        Vector2 _savedAnchorMin;
        Vector2 _savedAnchorMax;
        float _minimizedHeight = 1f; // fraction of canvas for collapsed title bar

        // Dual-column state (from BitNaughts)
        protected int dividerPos = 26;
        protected int leftColWidth = 25;
        protected int rightColWidth = 24;
        protected string separator;
        protected string leftSeparator;
        protected string rightSeparator;
        protected bool columnsInitialized;

        // Column draggers for intra-panel resizing
        protected List<TUIColumnDragger> columnDraggers;

        /// <summary>Get a column dragger by index (0-based). Returns null if not ready.</summary>
        public TUIColumnDragger GetColumnDragger(int index)
        {
            if (columnDraggers == null || index < 0 || index >= columnDraggers.Count) return null;
            return columnDraggers[index];
        }

        // Derived indices
        protected int RowSepBot   => totalRows - 2;
        protected int RowActions  => totalRows - 1;
        protected int ContentEnd  => totalRows - 3;
        protected int ContentRows => ContentEnd - ROW_CONTENT_START + 1;

        /// <summary>Whether this panel is currently minimized to a title bar.</summary>
        public bool IsMinimized => _minimized;

        // ── Animation markers ───────────────────────────────────
        protected const string M_SPIN  = "[SPIN]";
        protected const string M_PROG  = "[PROG]";
        protected const string M_PULSE = "[PULSE]";        protected const string M_BLINK = "[BLINK]";
        // ── Lifecycle ───────────────────────────────────────────

        protected virtual void Awake() { }

        protected virtual void Start() => Initialize();

        protected virtual void Update()
        {
            windowAge += Time.deltaTime;
            if (rowsReady)
            {
                CheckResize();
                UpdateHoveredColumn();
                Render();
            }
        }

        // ── Initialization ──────────────────────────────────────

        public void Initialize()
        {
            if (rowsReady) return;
            BuildRows();
            StartCoroutine(MeasureAfterLayout());
        }

        /// <summary>
        /// Programmatic setup — call instead of relying on Inspector wiring.
        /// Creates a hidden seed TMP_Text (for font/size reference) and
        /// initializes the row grid.
        /// </summary>
        public void InitializeProgrammatic(TMP_FontAsset font, float fontSize, Image bg = null)
        {
            if (contentText == null)
            {
                var seedGO = new GameObject("_FontSeed");
                seedGO.transform.SetParent(transform, false);
                var tmp = seedGO.AddComponent<TextMeshProUGUI>();
                if (font != null) tmp.font = font;
                tmp.fontSize = fontSize;
                tmp.raycastTarget = false;
                contentText = tmp;
            }
            if (bg != null) backgroundImage = bg;
            Initialize();
        }

        void BuildRows()
        {
            if (contentText == null) return;
            contentText.gameObject.SetActive(false);

            var font = contentText.font;
            float size = contentText.fontSize;
            var parent = contentText.transform.parent;

            for (int i = 0; i < totalRows; i++)
                rows.Add(TerminalRow.Create(parent, font, size, i));

            if (backgroundImage != null)
            {
                ApplyBackgroundStyle();
            }

            rowsReady = true;
            EnsureRaycastTarget();
            OnRowsReady();
        }

        IEnumerator MeasureAfterLayout()
        {
            yield return new WaitForEndOfFrame();
            if (rows.Count > 0) totalChars = rows[0].GetTotalCharacters();
            CacheContainerSize();
            OnLayoutReady();
        }

        void CacheContainerSize()
        {
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                lastMeasuredWidth = rt.rect.width;
                lastMeasuredHeight = rt.rect.height;
            }
        }

        void CheckResize()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            float w = rt.rect.width;
            float h = rt.rect.height;
            if (Mathf.Abs(w - lastMeasuredWidth) < RESIZE_THRESHOLD &&
                Mathf.Abs(h - lastMeasuredHeight) < RESIZE_THRESHOLD)
                return;

            lastMeasuredWidth = w;
            lastMeasuredHeight = h;

            if (rows.Count > 0)
                totalChars = rows[0].GetTotalCharacters();

            float rowHeight = rows.Count > 0 ? rows[0].RowHeight : 18f;
            int newRowCount = Mathf.Max(6, Mathf.FloorToInt(h / rowHeight));

            if (newRowCount != totalRows)
            {
                var font = contentText != null ? contentText.font : null;
                float fontSize = contentText != null ? contentText.fontSize : 13f;
                var parent = rows.Count > 0 ? rows[0].transform.parent : transform;

                while (rows.Count < newRowCount)
                    rows.Add(TerminalRow.Create(parent, font, fontSize, rows.Count));

                for (int i = newRowCount; i < rows.Count; i++)
                    rows[i].gameObject.SetActive(false);
                for (int i = 0; i < newRowCount; i++)
                    rows[i].gameObject.SetActive(true);

                totalRows = newRowCount;
            }

            OnLayoutReady();
        }

        // ── Blur ─────────────────────────────────────────────

        /// <summary>
        /// Enable or disable glassmorphic blur on this terminal's background.
        /// Requires: TUIBlurFeature on the URP renderer + a blur material.
        /// Typically called when quality tier changes (e.g. Ultra = on, others = off).
        /// </summary>
        public void SetBlurEnabled(bool enabled)
        {
            _blurEnabled = enabled;
            ApplyBackgroundStyle();
        }

        /// <summary>
        /// Enable or disable blur globally on all living terminals.
        /// Called automatically by TUIBlurManager based on quality tier.
        /// </summary>
        public static void SetSharedBlurEnabled(bool enabled)
        {
            _sharedBlurEnabled = enabled;
            foreach (var tw in FindObjectsByType<TerminalWindow>(FindObjectsSortMode.None))
                tw.ApplyBackgroundStyle();
        }

        /// <summary>Whether blur is currently active on this terminal.</summary>
        public bool BlurEnabled
        {
            get
            {
                var mat = blurMaterial != null ? blurMaterial : SharedBlurMaterial;
                return (_blurEnabled || _sharedBlurEnabled) && mat != null;
            }
        }

        void ApplyBackgroundStyle()
        {
            if (backgroundImage == null) return;
            var mat = blurMaterial != null ? blurMaterial : SharedBlurMaterial;
            bool blur = (_blurEnabled || _sharedBlurEnabled) && mat != null;
            if (blur)
            {
                backgroundImage.material = mat;
                backgroundImage.color = new Color(0, 0, 0, 0.7f);
            }
            else
            {
                backgroundImage.material = null;
                backgroundImage.color = new Color(0, 0, 0, 0.7f);
            }
        }

        /// <summary>Called once rows exist (before layout measurement).</summary>
        protected virtual void OnRowsReady() { }

        /// <summary>Called once character widths are measured (and on resize).</summary>
        protected virtual void OnLayoutReady() { }

        // ── Dual-column initialization (from BitNaughts) ────────

        /// <summary>
        /// Initialize dual-column layout after Unity layout settles.
        /// Call from Awake/OnRowsReady if your terminal needs split-pane display.
        /// </summary>
        protected void InitializeDualColumns(float splitRatio = 0.5f)
        {
            StartCoroutine(InitializeColumnsAfterLayout(splitRatio));
        }

        IEnumerator InitializeColumnsAfterLayout(float splitRatio)
        {
            yield return new WaitForEndOfFrame();
            if (rows.Count == 0) yield break;

            totalChars = rows[0].GetTotalCharacters();
            dividerPos = Mathf.RoundToInt(totalChars * splitRatio);
            leftColWidth = dividerPos - 1;
            rightColWidth = totalChars - dividerPos - 1;
            separator = new string('─', Mathf.Max(1, totalChars - 4));
            leftSeparator = new string('─', Mathf.Max(1, leftColWidth - 2));
            rightSeparator = new string('─', Mathf.Max(1, rightColWidth - 1));

            foreach (var row in rows)
                row.SetDualColumnMode(true, dividerPos);

            columnsInitialized = true;
            OnColumnsInitialized();
        }

        /// <summary>Called after dual-column layout is computed. Override for custom setup.</summary>
        protected virtual void OnColumnsInitialized() { }

        // ── Column draggers (intra-panel resize) ────────────────

        /// <summary>
        /// Create a draggable column divider at the given character position.
        /// Call after layout is ready (in OnLayoutReady or later).
        /// </summary>
        protected TUIColumnDragger AddColumnDragger(int charPos, int minPos, int maxPos, System.Action<int> onChanged)
        {
            if (columnDraggers == null) columnDraggers = new();
            float cw = rows.Count > 0 ? rows[0].CharWidth : 10f;
            var parentRT = GetComponent<RectTransform>();
            var dragger = TUIColumnDragger.Create(parentRT, cw, charPos, minPos, maxPos, onChanged);
            columnDraggers.Add(dragger);
            return dragger;
        }

        /// <summary>Update dual-column divider position on all rows.</summary>
        protected void ApplyDualColumnResize(int newDivider)
        {
            dividerPos = newDivider;
            leftColWidth = dividerPos - 1;
            rightColWidth = totalChars - dividerPos - 1;
            foreach (var row in rows)
                row.SetDualColumnMode(true, dividerPos);
        }

        /// <summary>Update three-panel column positions on all rows.</summary>
        protected void ApplyThreePanelResize(int newCol2, int newCol3)
        {
            foreach (var row in rows)
                row.SetThreePanelMode(true, newCol2, newCol3);
        }

        /// <summary>Update N-panel column positions on all rows.</summary>
        protected void ApplyNPanelResize(int[] colPositions)
        {
            foreach (var row in rows)
                row.SetNPanelMode(true, colPositions);
        }

        // ── Row API ─────────────────────────────────────────────

        protected TerminalRow Row(int i)
            => i >= 0 && i < rows.Count ? rows[i] : null;

        protected void SetRow(int i, string text) => Row(i)?.SetText(text);

        protected void ClearAllRows()
        {
            foreach (var r in rows) r.Clear();
        }

        // ── Scrollback write (for streaming terminals) ──────────

        public void WriteLine(string text)           => scrollback.Add(text);
        public void WriteColored(string text, Color32 c)
            => scrollback.Add(TUIColors.Fg(c, text));
        public void WriteSuccess(string t) => scrollback.Add(TUIColors.Fg(TUIColors.BrightGreen, $"{TUIGlyphs.Check} {t}"));
        public void WriteError(string t)   => scrollback.Add(TUIColors.Fg(TUIColors.Red, $"{TUIGlyphs.Cross} {t}"));
        public void WriteInfo(string t)    => scrollback.Add(TUIColors.Fg(TUIColors.BrightCyan, $"{TUIGlyphs.Info} {t}"));
        public void WriteDivider()         => scrollback.Add(TUIColors.Dimmed(TUIWidgets.Divider(totalChars)));
        public void Clear()               { scrollback.Clear(); ClearAllRows(); }

        // ── Rendering ───────────────────────────────────────────

        /// <summary>Override to provide custom per-frame rendering.</summary>
        protected virtual void Render()
        {
            RenderHeader();
            RenderScrollback();
        }

        protected void RenderHeader()
        {
            string indicator = TUIWidgets.SpinnerFrame(windowAge,
                TUIGlyphs.PulseDiamond, 0.2f);
            Color32 c = TUIGradient.Sample(0f);
            string display = _isHovered || string.IsNullOrEmpty(windowSubtitle)
                ? windowTitle : windowSubtitle;
            SetRow(ROW_HEADER, $"{TUIColors.Fg(c, indicator)} {TUIColors.Bold(display)}");
        }

        protected void RenderScrollback()
        {
            int start = Mathf.Max(0, scrollback.Count - ContentRows);
            for (int r = ROW_CONTENT_START; r <= ContentEnd; r++)
            {
                int idx = start + (r - ROW_CONTENT_START);
                string line = idx < scrollback.Count ? scrollback[idx] : "";
                SetRow(r, ProcessMarkers(line));
            }
        }

        protected string ProcessMarkers(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (text.Contains(M_SPIN))
                text = text.Replace(M_SPIN, TUIWidgets.SpinnerFrame(windowAge));
            if (text.Contains(M_PROG))
                text = text.Replace(M_PROG, TUIWidgets.ProgressBar(
                    Mathf.PingPong(windowAge * 0.5f, 1f), 8, false));
            if (text.Contains(M_PULSE))
                text = text.Replace(M_PULSE, TUIWidgets.SpinnerFrame(
                    windowAge, TUIGlyphs.PulseCircle, 0.12f));
            if (text.Contains(M_BLINK))
                text = text.Replace(M_BLINK, TUIEffects.BlinkingText(
                    TUIGlyphs.CircleFilled, windowAge, 0.5f));
            return text;
        }

        // ── Minimize ─────────────────────────────────────────────

        /// <summary>
        /// Toggle between minimized (title bar only) and expanded.
        /// Stores/restores anchors so the panel shrinks to a thin strip.
        /// </summary>
        public void ToggleMinimize()
        {
            var rt = GetComponent<RectTransform>();
            if (rt == null) return;

            if (_minimized)
            {
                // Restore
                rt.anchorMin = _savedAnchorMin;
                rt.anchorMax = _savedAnchorMax;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _minimized = false;
                foreach (var r in rows) r.gameObject.SetActive(true);
            }
            else
            {
                // Save and collapse
                _savedAnchorMin = rt.anchorMin;
                _savedAnchorMax = rt.anchorMax;
                float rowFraction = rows.Count > 0 ? rows[0].RowHeight : 20f;
                var canvas = rt.GetComponentInParent<Canvas>();
                float canvasH = canvas != null
                    ? ((RectTransform)canvas.transform).rect.height
                    : Screen.height;
                _minimizedHeight = Mathf.Max(0.02f, rowFraction / canvasH);
                rt.anchorMin = new Vector2(rt.anchorMin.x, rt.anchorMax.y - _minimizedHeight);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                _minimized = true;
                // Show only header row
                for (int i = 0; i < rows.Count; i++)
                    rows[i].gameObject.SetActive(i == 0);
            }
        }

        /// <summary>Render a single-row minimized title bar. Call from subclass Render().</summary>
        protected void RenderMinimized()
        {
            Color32 accent = TUIGradient.Sample(0.3f);
            SetRow(ROW_HEADER,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(windowTitle)} {TUIColors.Dimmed("[+]")}");
        }

        // ── Separator helpers ───────────────────────────────────

        protected string Separator(int width = -1)
        {
            width = width > 0 ? width : Mathf.Max(1, totalChars - 4);
            return TUIColors.Dimmed(TUIWidgets.Divider(width));
        }

        // ── Hover handlers ──────────────────────────────────────

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            _hoveredColumn = -1;
        }

        /// <summary>Check if a specific column index is currently hovered.</summary>
        protected bool IsColumnHovered(int colIndex) => _hoveredColumn == colIndex;

        /// <summary>
        /// Compute which column the mouse is over based on _hoverColumnPositions.
        /// Called each frame from Update() when hovered.
        /// </summary>
        private void UpdateHoveredColumn()
        {
            if (!_isHovered || _hoverColumnPositions == null || _hoverColumnPositions.Length == 0)
            {
                _hoveredColumn = _isHovered ? 0 : -1;
                return;
            }

            var rt = GetComponent<RectTransform>();
            if (rt == null) { _hoveredColumn = -1; return; }

            float cw = rows.Count > 0 ? rows[0].CharWidth : 0f;
            if (cw <= 0) { _hoveredColumn = 0; return; }

            var canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, Input.mousePosition, cam, out Vector2 localPoint))
            {
                _hoveredColumn = -1;
                return;
            }

            float x = localPoint.x - rt.rect.xMin;
            int charPos = Mathf.FloorToInt(x / cw);

            _hoveredColumn = 0;
            for (int i = _hoverColumnPositions.Length - 1; i >= 0; i--)
            {
                if (charPos >= _hoverColumnPositions[i])
                {
                    _hoveredColumn = i;
                    break;
                }
            }
        }

        /// <summary>
        /// Ensure a raycast-target Graphic exists on this GameObject
        /// so that IPointerEnterHandler / IPointerExitHandler fire.
        /// </summary>
        protected void EnsureRaycastTarget()
        {
            // If the background image is on our own GO, just enable raycast.
            if (backgroundImage != null && backgroundImage.gameObject == gameObject)
            {
                backgroundImage.raycastTarget = true;
                return;
            }
            // Otherwise add a transparent Image as a raycast catcher.
            var img = GetComponent<Image>();
            if (img == null)
            {
                img = gameObject.AddComponent<Image>();
                img.color = Color.clear;
            }
            img.raycastTarget = true;
        }
    }
}
