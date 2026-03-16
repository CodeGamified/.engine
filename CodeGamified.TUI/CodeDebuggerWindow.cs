// ═══════════════════════════════════════════════════════════
//  CodeDebuggerWindow — Shared three-panel code debugger base
//  SOURCE CODE │ MACHINE CODE │ REGISTERS & STATE
//  Unified from BitNaughts CodeWindow + SeaRauber CodeTerminal
// ═══════════════════════════════════════════════════════════
using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Abstract base for three-panel code debugger terminals.
    ///
    /// Layout:
    /// ┌──────────────┬──────────────┬──────────────┐
    /// │ SOURCE CODE  │ MACHINE CODE │ STATE        │
    /// │ ► line 1     │ ►0000: LOAD  │ R0: 0.30     │
    /// │   line 2     │  0001: OUT   │ R1: 1.00     │
    /// │              │  0002: WAIT  │ ────────     │
    /// │              │              │ STACK [2]    │
    /// └──────────────┴──────────────┴──────────────┘
    ///
    /// Subclass and provide game-specific program data by overriding:
    ///   - GetSourceLines(), GetProgramName()
    ///   - BuildSourceColumn(), BuildAsmColumn(), BuildStateColumn()
    ///   - HasLiveProgram, GetPC(), GetCycleCount()
    /// </summary>
    public abstract class CodeDebuggerWindow : TerminalWindow
    {
        // ── Column positions ────────────────────────────────────
        protected int col2Start = 28;
        protected int col3Start = 56;
        bool panelsEnabled;

        // ── Column dragger state ────────────────────────────────
        private TUIColumnDragger _col2Dragger;
        private TUIColumnDragger _col3Dragger;
        private float _col2Ratio = 1f / 3f;
        private float _col3Ratio = 2f / 3f;

        // ── Scroll state ────────────────────────────────────────
        protected int scrollOffset;
        protected int cursorLine;

        // ── Change detection ────────────────────────────────────
        protected int lastPC = -1;
        protected long lastCycle = -1;

        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CODE DEBUGGER";
            windowSubtitle = "CODE DEBUGGER";
            totalRows = 20;
        }

        protected override void OnLayoutReady()
        {
            col2Start = Mathf.Clamp(Mathf.RoundToInt(totalChars * _col2Ratio), 4, totalChars - 8);
            col3Start = Mathf.Clamp(Mathf.RoundToInt(totalChars * _col3Ratio), col2Start + 4, totalChars - 4);
            _hoverColumnPositions = new[] { 0, col2Start, col3Start };
            if (panelsEnabled)
            {
                panelsEnabled = false;
                EnableThreePanels();
            }
            SetupColumnDraggers();
        }

        private void SetupColumnDraggers()
        {
            if (_col2Dragger == null)
            {
                _col2Dragger = AddColumnDragger(col2Start, 4, col3Start - 4, OnCol2Dragged);
                _col3Dragger = AddColumnDragger(col3Start, col2Start + 4, totalChars - 4, OnCol3Dragged);
            }
            else
            {
                float cw = rows.Count > 0 ? rows[0].CharWidth : 10f;
                _col2Dragger.UpdateCharWidth(cw);
                _col2Dragger.UpdatePosition(col2Start);
                _col2Dragger.UpdateLimits(4, col3Start - 4);
                _col3Dragger.UpdateCharWidth(cw);
                _col3Dragger.UpdatePosition(col3Start);
                _col3Dragger.UpdateLimits(col2Start + 4, totalChars - 4);
            }
        }

        private void OnCol2Dragged(int newPos)
        {
            col2Start = newPos;
            _col2Ratio = (float)newPos / totalChars;
            _hoverColumnPositions = new[] { 0, col2Start, col3Start };
            _col3Dragger?.UpdateLimits(newPos + 4, totalChars - 4);
            ApplyThreePanelResize(col2Start, col3Start);
        }

        private void OnCol3Dragged(int newPos)
        {
            col3Start = newPos;
            _col3Ratio = (float)newPos / totalChars;
            _hoverColumnPositions = new[] { 0, col2Start, col3Start };
            _col2Dragger?.UpdateLimits(4, newPos - 4);
            ApplyThreePanelResize(col2Start, col3Start);
        }

        protected override void Update()
        {
            base.Update();
            HandleScrollInput();
        }

        void HandleScrollInput()
        {
            if (!rowsReady) return;
            var srcLines = GetSourceLines();
            if (srcLines == null || srcLines.Length == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
                cursorLine = Mathf.Max(0, cursorLine - 1);
            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
                cursorLine = Mathf.Min(srcLines.Length - 1, cursorLine + 1);

            if (cursorLine < scrollOffset) scrollOffset = cursorLine;
            if (cursorLine >= scrollOffset + ContentRows)
                scrollOffset = cursorLine - ContentRows + 1;
        }

        // ── Three-panel management ──────────────────────────────

        protected void EnableThreePanels()
        {
            if (panelsEnabled) return;
            panelsEnabled = true;
            foreach (var row in rows)
                row.SetThreePanelMode(true, col2Start, col3Start);
        }

        protected void DisableThreePanels()
        {
            if (!panelsEnabled) return;
            panelsEnabled = false;
            foreach (var row in rows)
                row.SetThreePanelMode(false, 0, 0);
        }

        protected void Set3(int r, string p1, string p2, string p3)
        {
            Row(r)?.SetThreePanelTexts(p1 ?? "", p2 ?? "", p3 ?? "");
        }

        // ── Abstract data interface ─────────────────────────────

        /// <summary>Source code lines to display.</summary>
        protected abstract string[] GetSourceLines();

        /// <summary>Program/file name for the header.</summary>
        protected abstract string GetProgramName();

        /// <summary>Whether a live executor is available for debug view.</summary>
        protected abstract bool HasLiveProgram { get; }

        /// <summary>Current program counter (instruction index).</summary>
        protected abstract int GetPC();

        /// <summary>Current cycle count.</summary>
        protected abstract long GetCycleCount();

        /// <summary>Build source column lines with current PC highlighting.</summary>
        protected abstract List<string> BuildSourceColumn(int pc);

        /// <summary>Build assembly/machine code column.</summary>
        protected abstract List<string> BuildAsmColumn(int pc);

        /// <summary>Build state/registers column.</summary>
        protected abstract List<string> BuildStateColumn();

        /// <summary>Status string (e.g., "RUN", "WAIT 1.2s", "HALTED").</summary>
        protected virtual string GetStatusString()
        {
            return TUIColors.Fg(TUIColors.BrightGreen, "RUN");
        }

        /// <summary>Optional multi-program index info (e.g., "[2/5]").</summary>
        protected virtual string GetIndexTag() => "";

        // ── Render dispatch ─────────────────────────────────────

        protected override void Render()
        {
            if (HasLiveProgram)
                RenderThreePanel();
            else
                RenderSourceOnly();
        }

        // ── Three-panel view ────────────────────────────────────

        void RenderThreePanel()
        {
            EnableThreePanels();
            ClearAllRows();

            int pc = GetPC();
            long cycle = GetCycleCount();

            int r = 0;
            Color32 accent = TUIGradient.Sample(0.3f);

            // Header — per-column hover: label (default) vs dynamic info (hovered)
            string indexTag = GetIndexTag();
            string status = GetStatusString();
            string col0 = IsColumnHovered(0)
                ? $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(GetProgramName())} {indexTag}"
                : TUIColors.Fg(accent, "SOURCE");
            string col1 = IsColumnHovered(1)
                ? status
                : TUIColors.Fg(accent, "MACHINE");
            string col2 = IsColumnHovered(2)
                ? TUIColors.Dimmed($"C:{cycle}")
                : TUIColors.Fg(accent, "STATE");
            Row(r)?.SetAlignment(TextAlignmentOptions.Left); // space-padded centering needs Left
            Row(r)?.SetThreePanelTextsCentered(col0, col1, col2);
            r++;

            // Content
            var srcLines   = BuildSourceColumn(pc);
            var asmLines   = BuildAsmColumn(pc);
            var stateLines = BuildStateColumn();

            int maxLines = Mathf.Max(srcLines.Count, Mathf.Max(asmLines.Count, stateLines.Count));
            for (int i = 0; i < maxLines && r <= ContentEnd; i++)
            {
                Set3(r++,
                    i < srcLines.Count   ? srcLines[i]   : "",
                    i < asmLines.Count   ? asmLines[i]   : "",
                    i < stateLines.Count ? stateLines[i] : "");
            }

            while (r <= ContentEnd) Set3(r++, "", "", "");
        }

        // ── Source-only fallback ────────────────────────────────

        void RenderSourceOnly()
        {
            DisableThreePanels();
            ClearAllRows();

            Color32 accent = TUIGradient.Sample(0.3f);
            string displayName = _isHovered ? GetProgramName() : windowTitle;
            SetRow(ROW_HEADER,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(displayName)}");
            Row(ROW_HEADER)?.SetAlignment(TextAlignmentOptions.Center);

            var srcLines = GetSourceLines();
            if (srcLines == null || srcLines.Length == 0)
            {
                SetRow(ROW_CONTENT_START, TUIColors.Dimmed("  No program loaded."));
            }
            else
            {
                for (int r = ROW_CONTENT_START; r <= ContentEnd; r++)
                {
                    int idx = scrollOffset + (r - ROW_CONTENT_START);
                    if (idx < srcLines.Length)
                    {
                        int ln = idx + 1;
                        bool active = idx == cursorLine;
                        string prefix = active
                            ? TUIColors.Fg(TUIColors.BrightGreen, TUIGlyphs.ArrowR)
                            : " ";
                        string num = TUIColors.Dimmed($"{ln,3}");
                        SetRow(r, $"{prefix}{num} {srcLines[idx]}");
                    }
                }
            }
        }
    }
}
