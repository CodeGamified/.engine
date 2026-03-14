// ═══════════════════════════════════════════════════════════
//  TerminalWindow — Abstract base for all terminal panels
//  Row-based layout powered by CodeGamified.TUI primitives
//  Unified: SRUI dynamic resize + BNUI dual-column init
// ═══════════════════════════════════════════════════════════
using UnityEngine;
using UnityEngine.UI;
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
    /// │ ROW 0:  Header / title              │
    /// │ ROW 1:  Subtitle / tabs             │
    /// │ ROW 2:  ── separator ──             │
    /// │ ROW 3…N-3: Content area             │
    /// │ ROW N-2: ── separator ──            │
    /// │ ROW N-1: Actions / hints            │
    /// └─────────────────────────────────────┘
    ///
    /// Subclass and override Render() for custom per-frame rendering.
    /// Use InitializeDualColumns() for split-pane layouts (from BitNaughts).
    /// </summary>
    public abstract class TerminalWindow : MonoBehaviour
    {
        // ── Layout constants ────────────────────────────────────
        public const int ROW_HEADER = 0;
        public const int ROW_SUBTITLE = 1;
        public const int ROW_SEP_TOP = 2;
        public const int ROW_CONTENT_START = 3;

        [Header("UI References")]
        [SerializeField] protected TMP_Text contentText;     // seed font/size
        [SerializeField] protected Image backgroundImage;

        [Header("Window")]
        [SerializeField] protected string windowTitle = "TERMINAL";
        [SerializeField] protected int totalRows = 20;

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

        // Dual-column state (from BitNaughts)
        protected int dividerPos = 26;
        protected int leftColWidth = 25;
        protected int rightColWidth = 24;
        protected string separator;
        protected string leftSeparator;
        protected string rightSeparator;
        protected bool columnsInitialized;

        // Derived indices
        protected int RowSepBot   => totalRows - 2;
        protected int RowActions  => totalRows - 1;
        protected int ContentEnd  => totalRows - 3;
        protected int ContentRows => ContentEnd - ROW_CONTENT_START + 1;

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
                backgroundImage.color = new Color(0.01f, 0.03f, 0.06f, 0.92f);

            rowsReady = true;
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
            SetRow(ROW_HEADER, $"{TUIColors.Fg(c, indicator)} {TUIColors.Bold(windowTitle)}");
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

        // ── Separator helpers ───────────────────────────────────

        protected string Separator(int width = -1)
        {
            width = width > 0 ? width : Mathf.Max(1, totalChars - 4);
            return TUIColors.Dimmed(TUIWidgets.Divider(width));
        }
    }
}
