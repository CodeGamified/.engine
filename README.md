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
├── CodeGamified.Time/     Simulation time: singleton clock + time warp state machine
│   ├── SimulationTime.cs    Abstract base: time scale, pause, presets, events, sun stubs
│   └── TimeWarpController.cs  Abstract warp: accel → cruise → decel → arrive
│
├── CodeGamified.TUI/       Terminal UI: row-based monospace layout → TMP rich-text
│   ├── Primitives (11)        TUIColors, TUIGlyphs, TUIConfig, TUIGradient, TUIText,
│   │                          TUIEasing, TUIEffects, TUILayout, TUIWidgets, TUIAnimation,
│   │                          TUIFormat — pure static helpers, no MonoBehaviour
│   ├── Runtime (3)            TerminalWindow (abstract), TerminalRow, TUIEdgeDragger
│   ├── Bases (2)              CodeDebuggerWindow, StatusBarBase — abstract game scaffolds
│   ├── TUIConstants.cs        Shared layout constants (indent, tab width, bar width)
│   ├── Tests/                 40 NUnit edit-mode tests
│   ├── Migration/             migrate-namespace.ps1 for legacy → CodeGamified.TUI
│   └── py/TUI.py              Python source of truth (1:1 with C# statics)
│
└── CodeGamified.Persistence/ Git-backed persistence: CRUD → git ops, zero hosting
    ├── IGitRepository.cs      Core CRUD + sync interface (Save, Load, Delete, Push, Pull)
    ├── IEntitySerializer.cs   Serialize/deserialize contract with schema versioning
    ├── IRemoteReader.cs       Read-only interface for browsing other players' public repos
    ├── PlayerIdentity.cs      Player ID + optional GitHub sharing config
    ├── GitPath.cs             Path conventions + sanitization (players/{id}/programs/{name}.json)
    ├── GitMerge.cs            Three-way + two-way field-level JSON merge
    ├── EntityStore.cs         Typed CRUD wrapper: IGitRepository + IEntitySerializer<T>
    ├── PersistenceBehaviour.cs  Abstract MonoBehaviour: autosave, dirty tracking, sync
    ├── PlayerRegistry.cs      Client for auto-populated player index (search, browse, import)
    ├── Providers/             LocalGitProvider, PublicRepoReader, MemoryGitProvider
    ├── Registry/              GitHub Action: nightly fork scrape → registry.json
    └── Tests/                 NUnit edit-mode tests
```

## Integration

Each game repo adds these as submodules:

```bash
git submodule add <repo> Assets/CodeGamified.Engine
git submodule add <repo> Assets/CodeGamified.Time
git submodule add <repo> Assets/CodeGamified.TUI
git submodule add <repo> Assets/CodeGamified.Persistence
```

Games extend via interfaces/abstract classes — no forking required:

| Extension Point | Module | Game Implements |
|---|---|---|
| `IGameIOHandler` | Engine | Custom opcode execution (sensors, signals, orders) |
| `ICompilerExtension` | Engine | Game builtins, known types, method compilation |
| `ProgramBehaviour` | Engine | MonoBehaviour lifecycle for running programs |
| `SimulationTime` | Time | Game clock, max scale, sun model, time formatting |
| `TimeWarpController` | Time | Warp-to-event with camera/spawn hooks |
| `TerminalWindow` | TUI | Concrete terminal panels (ship log, nav chart, etc.) |
| `CodeDebuggerWindow` | TUI | Three-panel source/asm/state debugger |
| `StatusBarBase` | TUI | Game-specific status bar sections |
| `IGitRepository` | Persistence | Provider selection (local git, GitHub API, mock) |
| `IEntitySerializer<T>` | Persistence | Serialize game entities (programs, ships, configs) |
| `PersistenceBehaviour` | Persistence | Autosave hooks, dirty tracking, sync triggers |

## Assembly Definitions

| Assembly | Dependencies |
|---|---|
| `CodeGamified.Engine` | — |
| `CodeGamified.Time` | — |
| `CodeGamified.TUI` | `Unity.TextMeshPro` |
| `CodeGamified.TUI.Tests` | `CodeGamified.TUI`, `nunit.framework` (Editor-only) |
| `CodeGamified.Persistence` | — |
| `CodeGamified.Persistence.Tests` | `CodeGamified.Persistence`, `nunit.framework` (Editor-only) |

See each module's own `README.md` for API details and code examples.