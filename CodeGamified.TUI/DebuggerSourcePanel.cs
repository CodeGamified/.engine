// ═══════════════════════════════════════════════════════════
//  DebuggerSourcePanel — Standalone source code viewer
//  Consumes IDebuggerDataSource, renders only source lines.
//  Independently resizable, minimizable, draggable.
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CodeGamified.TUI
{
    public class DebuggerSourcePanel : TerminalWindow
    {
        private IDebuggerDataSource _data;
        private int _scrollOffset;
        private int _cursorLine;

        protected override void Awake()
        {
            base.Awake();
            windowTitle = "SOURCE";
            windowSubtitle = "SOURCE";
            totalRows = 20;
        }

        public void Bind(IDebuggerDataSource data) => _data = data;

        protected override void Update()
        {
            base.Update();
            if (rowsReady && !IsMinimized) HandleScroll();
        }

        private void HandleScroll()
        {
            var src = _data?.SourceLines;
            if (src == null || src.Length == 0) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
                _cursorLine = Mathf.Max(0, _cursorLine - 1);
            if (Input.GetKeyDown(KeyCode.DownArrow))
                _cursorLine = Mathf.Min(src.Length - 1, _cursorLine + 1);

            if (_cursorLine < _scrollOffset) _scrollOffset = _cursorLine;
            if (_cursorLine >= _scrollOffset + ContentRows)
                _scrollOffset = _cursorLine - ContentRows + 1;
        }

        protected override void Render()
        {
            if (IsMinimized) { RenderMinimized(); return; }
            ClearAllRows();

            Color32 accent = TUIGradient.Sample(0.3f);
            string status = _data?.StatusString ?? "";

            string displayName = _isHovered ? (_data?.ProgramName ?? windowTitle) : windowSubtitle;
            SetRow(ROW_HEADER,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(displayName)}  {status}");
            Row(ROW_HEADER)?.SetAlignment(TextAlignmentOptions.Center);

            if (_data == null || !_data.HasLiveProgram)
            {
                SetRow(ROW_CONTENT_START, TUIColors.Dimmed("  No program loaded."));
                return;
            }

            int pc = _data.PC;
            var lines = _data.BuildSourceLines(pc, _scrollOffset, ContentRows);
            for (int i = 0; i < lines.Count && ROW_CONTENT_START + i <= ContentEnd; i++)
                SetRow(ROW_CONTENT_START + i, lines[i]);
        }
    }
}
