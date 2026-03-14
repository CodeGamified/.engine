# `.engine/` — Shared Submodules

Reusable Unity modules consumed by all CodeGamified games (Pong, BitNaughts, SeaRauber, etc.)  
via `git submodule`. Improvements propagate to every game on `submodule update`.

## Modules

```
.engine/
├── CodeGamified.Engine/   Code execution: Python subset → AST → bytecode → executor
│   ├── Compiler/            PythonCompiler, AstNodes, CompilerContext, ICompilerExtension
│   ├── Runtime/             ProgramBehaviour (MonoBehaviour), IProgramDatabase
│   ├── CodeExecutor.cs      Step/batch update loop, CUSTOM_0..31 delegation
│   ├── IGameIOHandler.cs    Game-specific I/O (sensor reads, transmissions, orders)
│   ├── MachineState.cs      Registers, stack, memory, flags, PC
│   └── ...                  OpCode, Instruction, CpuFlags, GameEvent, CompiledProgram
│
└── CodeGamified.TUI/       Terminal UI: row-based monospace layout → TMP rich-text
    ├── Primitives (11)        TUIColors, TUIGlyphs, TUIConfig, TUIGradient, TUIText,
    │                          TUIEasing, TUIEffects, TUILayout, TUIWidgets, TUIAnimation,
    │                          TUIFormat — pure static helpers, no MonoBehaviour
    ├── Runtime (3)            TerminalWindow (abstract), TerminalRow, TUIEdgeDragger
    ├── Bases (2)              CodeDebuggerWindow, StatusBarBase — abstract game scaffolds
    ├── TUIConstants.cs        Shared layout constants (indent, tab width, bar width)
    ├── Tests/                 40 NUnit edit-mode tests
    ├── Migration/             migrate-namespace.ps1 for legacy → CodeGamified.TUI
    └── py/TUI.py              Python source of truth (1:1 with C# statics)
```

## Integration

Each game repo adds these as submodules:

```bash
git submodule add <repo> Assets/CodeGamified.Engine
git submodule add <repo> Assets/CodeGamified.TUI
```

Games extend via interfaces/abstract classes — no forking required:

| Extension Point | Module | Game Implements |
|---|---|---|
| `IGameIOHandler` | Engine | Custom opcode execution (sensors, signals, orders) |
| `ICompilerExtension` | Engine | Game builtins, known types, method compilation |
| `ProgramBehaviour` | Engine | MonoBehaviour lifecycle for running programs |
| `TerminalWindow` | TUI | Concrete terminal panels (ship log, nav chart, etc.) |
| `CodeDebuggerWindow` | TUI | Three-panel source/asm/state debugger |
| `StatusBarBase` | TUI | Game-specific status bar sections |

## Assembly Definitions

| Assembly | Dependencies |
|---|---|
| `CodeGamified.Engine` | — |
| `CodeGamified.TUI` | `Unity.TextMeshPro` |
| `CodeGamified.TUI.Tests` | `CodeGamified.TUI`, `nunit.framework` (Editor-only) |

See each module's own `README.md` for API details and code examples.