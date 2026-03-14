# CodeGamified.Audio — Handoff

## Goal

New shared package: `CodeGamified.Audio`  
Consumes the event hooks already wired in `CodeGamified.Editor` and provides a
game-agnostic audio/haptic feedback layer for the tap-to-code editor (and any
future TUI surface that needs sound).

## Existing Hooks (ready to consume)

### CodeEditorWindow events

| Event | Signature | Fires when |
|-------|-----------|-----------|
| `OnOptionSelected` | `Action<string>` | Player taps any option (arg = label) |
| `OnUndoPerformed` | `Action` | Undo executed |
| `OnRedoPerformed` | `Action` | Redo executed |
| `OnCompileError` | `Action<int>` | Compilation fails (arg = error count) |
| `OnDocumentChanged` | `Action` | Any document mutation |

### CodeDocument event

| Event | Signature | Fires when |
|-------|-----------|-----------|
| `OnDocumentChanged` | `Action` | Any mutation (insert, remove, replace, swap, undo, redo) |

`CodeEditorWindow.OnDocumentChanged` is a relay of `CodeDocument.OnDocumentChanged`.
Subscribe to the window event — it handles attachment/detachment on `Open()`.

### Where they fire

| Hook | Source file | Method |
|------|-----------|--------|
| `OnOptionSelected` | `CodeEditorWindow.cs` | `SelectOption()` |
| `OnUndoPerformed` | `CodeEditorWindow.cs` | `DoUndo()` |
| `OnRedoPerformed` | `CodeEditorWindow.cs` | `DoRedo()` |
| `OnCompileError` | `CodeEditorWindow.cs` | `CompileAndRun()` |
| `OnDocumentChanged` | `CodeDocument.cs` | `RecordAction()`, `Undo()`, `Redo()` |

## Proposed Architecture

```
CodeGamified.Audio/
├── CodeGamified.Audio.asmdef   (refs: none — pure audio, no Editor/Engine dep)
├── IAudioProvider.cs           interface games implement
├── AudioBridge.cs              static wiring helper: Subscribe(editor, provider)
├── HapticBridge.cs             static wiring helper: Subscribe(editor, provider)
└── README.md
```

### Why no Editor/Engine reference?

The Audio package should be **event-consumer only**. It receives `Action` / `Action<string>`
delegates — no need to know about AST nodes or TUI rows. The **game project** wires
Editor → Audio in a MonoBehaviour or bootstrapper. This keeps the dependency graph clean:

```
Game
├── CodeGamified.Editor  (refs Engine + TUI)
├── CodeGamified.Audio   (refs nothing)
└── wiring: editor.OnOptionSelected += audioProvider.PlayTap
```

## IAudioProvider (proposed)

```csharp
namespace CodeGamified.Audio
{
    public interface IAudioProvider
    {
        void PlayTap();              // option selected
        void PlayInsert();           // statement inserted (OnDocumentChanged)
        void PlayDelete();           // statement deleted
        void PlayUndo();             // undo
        void PlayRedo();             // redo
        void PlayCompileSuccess();   // valid program compiled
        void PlayCompileError();     // compilation failed
        void PlayNavigate();         // cursor moved / option drilled into
    }
}
```

Games implement with their own `AudioClip` assets. A `NullAudioProvider` ships as default.

## IHapticProvider (proposed)

```csharp
namespace CodeGamified.Audio
{
    public interface IHapticProvider
    {
        void TapLight();     // option tap
        void TapMedium();    // insert / delete
        void TapHeavy();     // compile error
        void Buzz(float duration);  // generic
    }
}
```

## AudioBridge (proposed wiring helper)

```csharp
public static class AudioBridge
{
    public static void Subscribe(CodeEditorWindow editor, IAudioProvider audio)
    {
        editor.OnOptionSelected += (_) => audio.PlayTap();
        editor.OnUndoPerformed  += ()  => audio.PlayUndo();
        editor.OnRedoPerformed  += ()  => audio.PlayRedo();
        editor.OnCompileError   += (_) => audio.PlayCompileError();
        editor.OnDocumentChanged += () => audio.PlayInsert();
    }
}
```

> **Note:** `AudioBridge` _does_ need an Editor ref if it takes `CodeEditorWindow`.
> Alternative: keep it in the game project and make Audio truly zero-dep.
> Decision TBD — either approach works.

## Open Questions

| # | Question | Options |
|---|----------|---------|
| 1 | Should `AudioBridge` live in Audio (adding Editor ref) or in the game project? | A) Audio refs Editor — convenient. B) Game wires manually — Audio stays zero-dep. |
| 2 | Distinguish insert vs delete vs replace in `OnDocumentChanged`? | Current event is a bare `Action`. Could add `Action<EditKind>` enum if needed. |
| 3 | Pool `AudioSource` components or one-shot `PlayOneShot`? | `PlayOneShot` is simpler; pooling only if overlapping sounds clip. |
| 4 | Mobile haptics: Unity `Handheld.Vibrate()` vs native plugin? | `Handheld.Vibrate()` is coarse. iOS/Android native gives granular patterns. |
| 5 | Should audio package also cover TUI terminal sounds (scrollback, typing)? | Scope creep risk. Start with Editor hooks, extend later. |

## Checklist

- [x] Create `.engine/CodeGamified.Audio/` directory
- [x] Create `CodeGamified.Audio.asmdef` (zero-dep, option B — no Editor/Engine refs)
- [x] `IAudioProvider.cs` — interface
- [x] `IHapticProvider.cs` — interface
- [x] `NullAudioProvider.cs` — silent default
- [x] `NullHapticProvider.cs` — no-op default
- [x] `AudioBridge.cs` — zero-dep wiring helper (returns handler delegates)
- [x] `HapticBridge.cs` — zero-dep wiring helper (returns handler delegates)
- [x] `README.md`
- [ ] Integration example in a game project (e.g. Pong or Satellite)
- [ ] Verify no new compile errors in Editor or Engine
