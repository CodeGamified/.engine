// ═══════════════════════════════════════════════════════════
//  CodeDebuggerWindow — Shared three-panel code debugger base
//  SOURCE CODE │ MACHINE CODE │ REGISTERS & STATE
//  Unified from BitNaughts CodeWindow + SeaRauber CodeTerminal
// ═══════════════════════════════════════════════════════════
using UnityEngine;
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
            totalRows = 20;
        }

        protected override void OnLayoutReady()
        {
            col2Start = totalChars / 3;
            col3Start = (totalChars * 2) / 3;
            if (panelsEnabled)
            {
                panelsEnabled = false;
                EnableThreePanels();
            }
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

            // Header
            string indexTag = GetIndexTag();
            string status = GetStatusString();
            Set3(r++,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(GetProgramName())} {indexTag}",
                status,
                TUIColors.Dimmed($"C:{cycle}"));

            // Separator
            string sep1 = Separator(col2Start - 2);
            string sep2 = Separator(col3Start - col2Start - 2);
            string sep3 = Separator(totalChars - col3Start - 2);
            Set3(r++, sep1, sep2, sep3);

            // Column headers
            Set3(r++,
                TUIColors.Fg(accent, "SOURCE"),
                TUIColors.Fg(accent, "MACHINE CODE"),
                TUIColors.Fg(accent, "STATE"));
            Set3(r++, sep1, sep2, sep3);

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

            // Footer
            Set3(RowSepBot, sep1, sep2, sep3);
            Row(RowActions)?.SetThreePanelTexts(
                TUIColors.Dimmed($"  {TUIGlyphs.ArrowU}/{TUIGlyphs.ArrowD} scroll  [ESC] close"),
                "", "");
        }

        // ── Source-only fallback ────────────────────────────────

        void RenderSourceOnly()
        {
            DisableThreePanels();
            ClearAllRows();

            Color32 accent = TUIGradient.Sample(0.3f);
            SetRow(ROW_HEADER,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(GetProgramName())}");
            SetRow(ROW_SEP_TOP, Separator());

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

            SetRow(RowSepBot, Separator());
            SetRow(RowActions, TUIColors.Dimmed(
                $"  {TUIGlyphs.ArrowU}/{TUIGlyphs.ArrowD} scroll  [ESC] close"));
        }
    }
}
