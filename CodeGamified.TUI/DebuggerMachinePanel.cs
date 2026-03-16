// ═══════════════════════════════════════════════════════════
//  DebuggerMachinePanel — Standalone machine code viewer
//  Consumes IDebuggerDataSource, renders only assembly lines.
//  Independently resizable, minimizable, draggable.
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CodeGamified.TUI
{
    public class DebuggerMachinePanel : TerminalWindow
    {
        private IDebuggerDataSource _data;

        protected override void Awake()
        {
            base.Awake();
            windowTitle = "MACHINE";
            windowSubtitle = "MACHINE";
            totalRows = 20;
        }

        public void Bind(IDebuggerDataSource data) => _data = data;

        protected override void Render()
        {
            if (IsMinimized) { RenderMinimized(); return; }
            ClearAllRows();

            Color32 accent = TUIGradient.Sample(0.5f);

            string display = _isHovered
                ? (_data != null && _data.HasLiveProgram ? TUIColors.Dimmed($"C:{_data.CycleCount}") : windowTitle)
                : windowSubtitle;
            SetRow(ROW_HEADER,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(display)}");
            Row(ROW_HEADER)?.SetAlignment(TextAlignmentOptions.Center);

            if (_data == null || !_data.HasLiveProgram)
            {
                SetRow(ROW_CONTENT_START, TUIColors.Dimmed("  No program loaded."));
                return;
            }

            int pc = _data.PC;
            var lines = _data.BuildMachineLines(pc, ContentRows);
            for (int i = 0; i < lines.Count && ROW_CONTENT_START + i <= ContentEnd; i++)
                SetRow(ROW_CONTENT_START + i, lines[i]);
        }
    }
}
