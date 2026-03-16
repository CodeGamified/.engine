// ═══════════════════════════════════════════════════════════
//  IDebuggerDataSource — Data interface for debugger panels
//  Games implement this to feed source/machine/state panels.
//  Engine panels consume it — zero game-specific knowledge.
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Data contract for code debugger panels.
    /// Implement once per program type; feed to DebuggerSourcePanel,
    /// DebuggerMachinePanel, and DebuggerStatePanel.
    /// </summary>
    public interface IDebuggerDataSource
    {
        string ProgramName { get; }
        string[] SourceLines { get; }
        bool HasLiveProgram { get; }
        int PC { get; }
        long CycleCount { get; }
        string StatusString { get; }

        /// <summary>Build source display lines. scrollOffset/maxRows for pagination.</summary>
        List<string> BuildSourceLines(int pc, int scrollOffset, int maxRows);

        /// <summary>Build machine/assembly display lines centered on PC.</summary>
        List<string> BuildMachineLines(int pc, int maxRows);

        /// <summary>Build register/state display lines.</summary>
        List<string> BuildStateLines();
    }
}
