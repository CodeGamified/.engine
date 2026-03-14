# CodeGamified.Audio

Game-agnostic audio and haptic feedback layer for the tap-to-code editor.

## Design

**Zero dependencies** — this package has no references to Editor, Engine, or TUI.
It exposes provider interfaces and bridge helpers that return handler delegates.
The **game project** wires Editor events → Audio/Haptic handlers.

```
Game
├── CodeGamified.Editor   (refs Engine + TUI)
├── CodeGamified.Audio    (refs nothing)
└── wiring: editor events += bridge handlers
```

## Quick Start

### 1. Implement `IAudioProvider`

```csharp
public class MyAudio : MonoBehaviour, IAudioProvider
{
    [SerializeField] AudioClip tapClip;
    AudioSource _src;

    void Awake() => _src = GetComponent<AudioSource>();

    public void PlayTap()            => _src.PlayOneShot(tapClip);
    public void PlayInsert()         => _src.PlayOneShot(tapClip);
    public void PlayDelete()         => _src.PlayOneShot(tapClip);
    public void PlayUndo()           => _src.PlayOneShot(tapClip);
    public void PlayRedo()           => _src.PlayOneShot(tapClip);
    public void PlayCompileSuccess() => _src.PlayOneShot(tapClip);
    public void PlayCompileError()   => _src.PlayOneShot(tapClip);
    public void PlayNavigate()       => _src.PlayOneShot(tapClip);
}
```

### 2. Wire in your bootstrapper

```csharp
using CodeGamified.Audio;

void Start()
{
    var audioHandlers  = AudioBridge.CreateHandlers(myAudioProvider);
    var hapticHandlers = HapticBridge.CreateHandlers(myHapticProvider);

    editor.OnOptionSelected  += audioHandlers.OptionSelected;
    editor.OnUndoPerformed   += audioHandlers.UndoPerformed;
    editor.OnRedoPerformed   += audioHandlers.RedoPerformed;
    editor.OnCompileError    += audioHandlers.CompileError;
    editor.OnDocumentChanged += audioHandlers.DocumentChanged;

    editor.OnOptionSelected  += hapticHandlers.OptionSelected;
    editor.OnUndoPerformed   += hapticHandlers.UndoPerformed;
    editor.OnRedoPerformed   += hapticHandlers.RedoPerformed;
    editor.OnCompileError    += hapticHandlers.CompileError;
    editor.OnDocumentChanged += hapticHandlers.DocumentChanged;
}
```

### Null providers

`NullAudioProvider` and `NullHapticProvider` are silent no-ops — useful for
tests, platforms without sound, or as defaults before the game configures audio.

## Interfaces

| Interface | Methods |
|-----------|---------|
| `IAudioProvider` | `PlayTap`, `PlayInsert`, `PlayDelete`, `PlayUndo`, `PlayRedo`, `PlayCompileSuccess`, `PlayCompileError`, `PlayNavigate` |
| `IHapticProvider` | `TapLight`, `TapMedium`, `TapHeavy`, `Buzz(float)` |

## Open Questions

See [HANDOFF.md](HANDOFF.md) for design decisions still TBD.
