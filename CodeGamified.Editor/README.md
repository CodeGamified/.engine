# CodeGamified.Editor

Mobile-friendly tap-to-code editor for educational programming games.  
Context-sensitive option tree → AST mutation → source regeneration.

## Architecture

```
Player taps option → OptionNode.Apply() → CodeDocument (AST) → ToSource()
                                              ↓
                                     PythonCompiler.Compile() → CodeExecutor
                                              ↑
                              OptionTreeBuilder queries IEditorExtension
                              for game-specific types, functions, methods
```

## Key Invariant

**Players edit AST nodes, not text.**  
Source is always regenerated from `CodeDocument.ToSource()`.  
Syntax errors are impossible — every option produces valid AST.

## Files

| File | Purpose |
|------|---------|
| `OptionNode.cs` | Tree node — label, glyph, children (branch) or Apply action (leaf) |
| `EditorCursor.cs` | Position tracking — display line, option stack, source + option scroll offsets |
| `CodeDocument.cs` | Mutable AST wrapper — undo/redo stack, body-aware insert/remove/move, `DisplayLine` view model, DeepClone, Duplicate, `ToSource()`, `ToJson()`/`FromJson()`, `Compile()` |
| `OptionTreeBuilder.cs` | Context-sensitive option trees — tier-gating, compound assignment `+=`/`-=`, elif/else insertion, game name suggestions, body-aware insert, clipboard copy/cut/paste, delete confirmation, multi-arg chaining, string literals |
| `IEditorExtension.cs` | Game-specific interface — types, functions, methods, variable name suggestions, loop tier-gating, string literal suggestions |
| `CodeEditorWindow.cs` | `TerminalWindow` subclass — display-line source view, scrollable option picker, inline compile errors, undo/redo + move buttons, audio/haptic event hooks |
| `Tests/EditorTests.cs` | Unit tests — roundtrip, undo/redo symmetry, body-aware ops, display lines, deep clone, duplicate, serialization, tier-gating, clipboard |

## Dependencies

```
CodeGamified.Editor
├── CodeGamified.Engine   (AST nodes, PythonCompiler, CompilerContext, ICompilerExtension)
└── CodeGamified.TUI      (TerminalWindow, TerminalRow button overlays, TUI primitives)
```

Engine and TUI remain decoupled from each other.

## Integration Pattern

### 1. Implement `IEditorExtension`

```csharp
public class SatelliteEditorExtension : IEditorExtension
{
    public List<EditorTypeInfo> GetAvailableTypes() => new()
    {
        new() { Name = "Radio", Hint = "RF transceiver" },
        new() { Name = "Geiger", Hint = "radiation sensor" }
    };

    public List<EditorFuncInfo> GetAvailableFunctions() => new()
    {
        new() { Name = "beep", Hint = "beep(freq)", ArgCount = 1 }
    };

    public List<EditorMethodInfo> GetMethodsForType(string typeName) => typeName switch
    {
        "Radio" => new()
        {
            new() { Name = "read", Hint = "read signal", ArgCount = 0, HasReturnValue = true },
            new() { Name = "beep", Hint = "beep(freq)", ArgCount = 1, HasReturnValue = false }
        },
        "Geiger" => new()
        {
            new() { Name = "read", Hint = "radiation μSv", ArgCount = 0, HasReturnValue = true }
        },
        _ => new()
    };

    // #6: domain-specific variable names
    public List<string> GetVariableNameSuggestions() => new()
    {
        "signal", "radiation", "heading", "altitude"
    };

    // #2: tier-gating — unlock loops at progression milestones
    public bool IsWhileLoopAllowed() => PlayerProgress.Tier >= 2;
    public bool IsForLoopAllowed() => PlayerProgress.Tier >= 3;
    public string GetWhileLoopGateReason() => "requires Chart Table tier";
    public string GetForLoopGateReason() => "requires Navigator's Office tier";
}
```

### 2. Open the Editor

```csharp
var editor = GetComponent<CodeEditorWindow>();
editor.OpenNew("satellite_01",
    new SatelliteCompilerExtension(),
    new SatelliteEditorExtension());

editor.OnCompileAndRun += (program) =>
{
    executor.LoadProgram(program);
};
```

### 3. Open with Existing Code

```csharp
editor.OpenSource(savedSource, "satellite_01",
    new SatelliteCompilerExtension(),
    new SatelliteEditorExtension());
```

## Mobile Interaction Flow

Example — player writes `x = r.read()` from scratch:

```
1. Tap [Insert below]     → see: Variable | Function | Object | while | for | if | Method call
2. Tap [Variable]         → see: [x] [y] [z] [i] ...
3. Tap [x]                → see: 0 | 1 | 2 | ... | var | Expression | Method result
4. Tap [Method result]    → see: [r.read()]
5. Tap [r.read()]         → line inserted: x = r.read()
```

5 taps. Zero typing. Every option was valid.

## Option Tree Structure

```
Statement level:
├── Variable       → name picker (game suggestions #6) → value picker
├── Increment      → var picker → value (#5: x += 1)
├── Decrement      → var picker → value (#5: x -= 1)
├── Function       → func picker → arg picker(s)
├── Object         → type picker (from IEditorExtension)
├── Method call    → object picker → method picker → arg picker(s)
├── while loop     → True | condition picker  (tier-gated #2)
├── for loop       → var picker → range end   (tier-gated #2)
└── if statement   → condition picker

Compound header (while/for/if body):
├── Add inside     → (statement options, inserts into body #3)
├── Add elif       → condition picker (#8, if-only)
├── Add else       → creates empty else block (#8, if-only)
├── Move up/down   → reorder in parent list (#10)
└── Delete

Value picker (expression slot):
├── Numbers        → 0, 1, 2, 3, 5, 10, 0.5, 100
├── Variables      → declared vars (includes nested scope)
├── Expression     → operator → left → right
├── Method result  → object → method (from IEditorExtension)
└── String         → game suggestions | common literals ("hello", "yes", ...)

Existing line:
├── Edit           → sub-expression editors
├── Insert above   → (statement options, body-aware)
├── Insert below   → (statement options, body-aware)
├── Move up        → swap with sibling above (#10)
├── Move down      → swap with sibling below (#10)
├── Delete         → confirm if compound has body (#5)
├── Duplicate      → deep-clone + insert below (#2)
├── Copy           → store clone in clipboard (#1)
├── Cut            → copy + delete (#1)
└── Paste below    → insert clipboard clone (#1)
```

## Undo / Redo (#1)

Every mutation (insert, remove, replace, swap) records a reversible `EditAction`.
`Ctrl+Z` / `[UNDO]` button reverts. `Ctrl+Y` / header `[REDO]` re-applies.
Stack clears on `LoadFromSource()` or `Clear()`.

## Compile Errors (#9)

`[RUN]` calls `Compile()`. If `program.Errors` is non-empty, errors render
inline in the source view as red `⚠ Line N: message` rows beneath the
offending line — no silent failures.

## Clipboard (Copy / Cut / Paste)

Copy stores a `DeepClone` of the current line's AST node in `EditorCursor.ClipboardNode`.
Cut does the same then removes the original. Paste inserts a fresh clone below the cursor.
Clipboard persists across cursor moves within the same session.

## Duplicate Line (#2)

Single-tap deep clone + insert below. Uses `CodeDocument.Duplicate(DisplayLine)`.
Undoable via the standard undo stack.

## Delete Confirmation (#5)

Deleting a `while`/`for`/`if` that has a non-empty body shows a sub-menu:
`Confirm delete (body will be lost)` | `Cancel`. Simple (bodyless) deletes skip confirmation.

## Multi-Arg Chaining (#9)

Functions and methods with `ArgCount > 1` present sequential arg pickers:
`arg 1/3 → arg 2/3 → arg 3/3 → confirm`. Each step shows the value picker.

## String Literals (#10)

`StringNode` added to Engine's `AstNodes`. Editor value picker offers a `String` option
with game-suggested phrases (`IEditorExtension.GetStringLiteralSuggestions()`) plus
common defaults. Parser handles `"..."` and `'...'` syntax.

## Serialization (#7)

`CodeDocument.ToJson()` serializes name + source. `CodeDocument.FromJson(json)` restores.
Uses source-roundtrip (not raw AST serialization) for simplicity and forward compatibility.

## Audio / Haptic Hooks (#8)

`CodeEditorWindow` exposes events for external audio/haptic systems:

| Event | Fires when |
|-------|-----------|
| `OnOptionSelected(label)` | Player taps any option |
| `OnUndoPerformed` | Undo executed |
| `OnRedoPerformed` | Redo executed |
| `OnCompileError(count)` | Compilation produces errors |
| `OnDocumentChanged` | Any document mutation (relay from `CodeDocument.OnDocumentChanged`) |

```csharp
editor.OnOptionSelected += (label) => AudioManager.PlayTap();
editor.OnUndoPerformed += () => AudioManager.PlayUndo();
editor.OnCompileError += (n) => Haptics.Buzz(0.1f);
```

## Live Preview (#3)

Subscribe to `OnDocumentChanged` to recompile and display machine state in a
side panel (e.g. `CodeDebuggerWindow`). The editor doesn't own the preview panel —
it fires the event, and the game wires it up.

## Tests

`Tests/EditorTests.cs` — 20+ NUnit tests covering:
- Source roundtrip (assignment, while, for, if/else, string literal)
- Undo/redo symmetry (insert, multi-op, swap)
- Body-aware operations (insert, remove, move)
- Display lines (compound header, pass placeholder)
- DeepClone independence (assignment, while-with-body, string)
- Duplicate
- Serialization (ToJson/FromJson roundtrip)
- Tier-gating (while/for disabled options)
- Document changed event
- Clipboard (copy + paste)
