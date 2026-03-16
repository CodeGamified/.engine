// ═══════════════════════════════════════════════════════════
//  DebuggerStatePanel — Standalone register/state viewer
//  Consumes IDebuggerDataSource, renders only state lines.
//  Independently resizable, minimizable, draggable.
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace CodeGamified.TUI
{
    public class DebuggerStatePanel : TerminalWindow
    {
        private IDebuggerDataSource _data;

        protected override void Awake()
        {
            base.Awake();
            windowTitle = "STATE";
            windowSubtitle = "STATE";
            totalRows = 20;
        }

        public void Bind(IDebuggerDataSource data) => _data = data;

        protected override void Render()
        {
            if (IsMinimized) { RenderMinimized(); return; }
            ClearAllRows();

            Color32 accent = TUIGradient.Sample(0.7f);

            string display = _isHovered ? windowTitle : windowSubtitle;
            SetRow(ROW_HEADER,
                $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(display)}");
            Row(ROW_HEADER)?.SetAlignment(TextAlignmentOptions.Center);

            if (_data == null || !_data.HasLiveProgram)
            {
                SetRow(ROW_CONTENT_START, TUIColors.Dimmed("  No state."));
                return;
            }

            var lines = _data.BuildStateLines();
            for (int i = 0; i < lines.Count && ROW_CONTENT_START + i <= ContentEnd; i++)
                SetRow(ROW_CONTENT_START + i, lines[i]);
        }
    }
}
