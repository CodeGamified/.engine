# CodeGamified.Audio

Game-agnostic audio and haptic feedback layer for **all engine modules**.

## Design

**Zero dependencies** — no references to Editor, Engine, Time, or Persistence.
Exposes provider interfaces and bridge handler classes. The **game project** wires
module events → handlers.

**Time-scale-gated** — each handler group has a `MaxTimeScale` threshold.
When `getTimeScale()` exceeds it, calls are silently skipped. Games pass a
`Func<float>` (typically `() => Time.timeScale`) at creation.

```
Game
├── CodeGamified.Editor       (refs Engine + TUI)
├── CodeGamified.Engine       (execution, bytecode)
├── CodeGamified.Time         (time warp)
├── CodeGamified.Persistence  (git-backed saves)
├── CodeGamified.Audio        (refs nothing)
└── wiring: module events += bridge handlers
```

## Quick Start

### 1. Implement `IAudioProvider`

```csharp
public class MyAudio : MonoBehaviour, IAudioProvider
{
    [SerializeField] AudioClip tapClip, insertClip, errorClip;
    // leave unused methods empty — no sound = no clip assigned
    AudioSource _src;

    void Awake() => _src = GetComponent<AudioSource>();

    // Editor
    public void PlayTap()            => _src.PlayOneShot(tapClip);
    public void PlayInsert()         => _src.PlayOneShot(insertClip);
    public void PlayDelete()         { }
    public void PlayUndo()           => _src.PlayOneShot(tapClip);
    public void PlayRedo()           => _src.PlayOneShot(tapClip);
    public void PlayCompileSuccess() { }
    public void PlayCompileError()   => _src.PlayOneShot(errorClip);
    public void PlayNavigate()       { }

    // Engine
    public void PlayInstructionStep() { }
    public void PlayOutput()          { }
    public void PlayHalted()          { }
    public void PlayIOBlocked()       { }
    public void PlayWaitStateChanged(){ }

    // Time
    public void PlayWarpStart()      { }
    public void PlayWarpCruise()     { }
    public void PlayWarpDecelerate() { }
    public void PlayWarpArrived()    { }
    public void PlayWarpCancelled()  { }
    public void PlayWarpComplete()   { }

    // Persistence
    public void PlaySaveStarted()    { }
    public void PlaySaveCompleted()  { }
    public void PlaySyncCompleted()  { }
}
```

### 2. Wire in your bootstrapper

```csharp
using CodeGamified.Audio;

void Start()
{
    Func<float> ts = () => Time.timeScale;

    // ---- Audio ----
    var editorAudio  = AudioBridge.ForEditor(audio, ts);
    var engineAudio  = AudioBridge.ForEngine(audio, ts);   // MaxTimeScale = 10 by default
    var timeAudio    = AudioBridge.ForTime(audio, ts);
    var persistAudio = AudioBridge.ForPersistence(audio, ts);

    // Editor — direct wire (signatures match)
    editor.OnOptionSelected  += editorAudio.OptionSelected;
    editor.OnUndoPerformed   += editorAudio.UndoPerformed;
    editor.OnRedoPerformed   += editorAudio.RedoPerformed;
    editor.OnCompileError    += editorAudio.CompileError;
    editor.OnDocumentChanged += editorAudio.DocumentChanged;

    // Engine — lambda wrappers for types Audio can't reference
    executor.OnInstructionExecuted += (_, _) => engineAudio.InstructionStep();
    executor.OnOutput              += _      => engineAudio.Output();
    executor.OnHalted              += engineAudio.Halted;            // direct
    executor.OnIOBlocked           += _      => engineAudio.IOBlocked();
    executor.OnWaitStateChanged    += (_, _) => engineAudio.WaitStateChanged();

    // Time — WarpState dispatch + direct events
    warp.OnWarpStateChanged += state =>
    {
        switch (state)
        {
            case TimeWarpController.WarpState.Accelerating: timeAudio.WarpStart(); break;
            case TimeWarpController.WarpState.Cruising:     timeAudio.WarpCruise(); break;
            case TimeWarpController.WarpState.Decelerating: timeAudio.WarpDecelerate(); break;
        }
    };
    warp.OnWarpArrived   += timeAudio.WarpArrived;
    warp.OnWarpCancelled += timeAudio.WarpCancelled;
    warp.OnWarpComplete  += timeAudio.WarpComplete;

    // Persistence
    persistence.OnSaveStarted   += persistAudio.SaveStarted;
    persistence.OnSaveCompleted += _ => persistAudio.SaveCompleted();
    persistence.OnSyncCompleted += _ => persistAudio.SyncCompleted();

    // ---- Haptic (same pattern) ----
    var editorHaptic = HapticBridge.ForEditor(haptic, ts);
    editor.OnOptionSelected += editorHaptic.OptionSelected;
    // ... etc.
}
```

### 3. Tune thresholds at runtime

```csharp
// Engine sounds only in step-mode (default: 10x)
engineAudio.MaxTimeScale = 5f;

// Mute editor taps during warp
editorAudio.MaxTimeScale = 100f;

// Persistence sounds always play
persistAudio.MaxTimeScale = float.MaxValue; // (already the default)
```

### 4. Add an equalizer

```csharp
using CodeGamified.Audio;
using CodeGamified.TUI;

// ---- Implement provider (game layer) ----
public class SpectrumProvider : MonoBehaviour, IEqualizerProvider
{
    public int BandCount => 8;
    readonly float[] _spectrum = new float[256];

    public bool GetBands(float[] bands)
    {
        AudioListener.GetSpectrumData(_spectrum, 0, FFTWindow.BlackmanHarris);
        // Map 256-sample spectrum → 8 logarithmic bands
        for (int b = 0; b < bands.Length; b++)
        {
            int lo = (int)Mathf.Pow(2, b);
            int hi = (int)Mathf.Pow(2, b + 1);
            float sum = 0f;
            for (int s = lo; s < hi && s < _spectrum.Length; s++)
                sum += _spectrum[s];
            bands[b] = Mathf.Clamp01(sum * (b + 1) * 10f);
        }
        return true;
    }
}

// ---- Wire in bootstrapper ----
var eqHandler = EqualizerBridge.Create(spectrumProvider, () => Time.timeScale);

// ---- Each frame ----
eqHandler.Update(Time.deltaTime);
```

### 5. Render EQ in a TUI panel

```csharp
// Render to lines with customisable width × height
string[] eqLines = TUIEqualizer.Render(
    eqHandler.Equalizer.SmoothedBands,
    eqHandler.Equalizer.PeakBands,
    new TUIEqualizer.Config
    {
        Width      = 40,
        Height     = 12,
        Style      = TUIEqualizer.Style.Bars,   // or .Mirror
        ShowBorder = true,
        ShowPeaks  = true,
        ShowLabels = true,
        Title      = "EQ",
    });

// Display in a TerminalWindow, status bar, or any TMP text field
foreach (string line in eqLines)
    terminal.AppendLine(line);
```

### 6. Tune equalizer at runtime

```csharp
// Smoothing
eqHandler.Equalizer.RiseSpeed     = 12f;  // snappier attack
eqHandler.Equalizer.FallSpeed     = 4f;   // smooth decay
eqHandler.Equalizer.PeakHoldTime  = 0.4f; // seconds before peak falls
eqHandler.Equalizer.PeakFallSpeed = 1.5f; // peak descent rate

// Gate — skip during extreme time warp
eqHandler.MaxTimeScale = 100f;
```

### Null providers

`NullAudioProvider`, `NullHapticProvider`, and `NullEqualizerProvider` are
silent no-ops — useful for tests, platforms without sound, or as defaults
before the game configures audio.

## Interfaces

| Interface | Methods |
|-----------|---------|
| `IAudioProvider` | Editor: `PlayTap`, `PlayInsert`, `PlayDelete`, `PlayUndo`, `PlayRedo`, `PlayCompileSuccess`, `PlayCompileError`, `PlayNavigate` │ Engine: `PlayInstructionStep`, `PlayOutput`, `PlayHalted`, `PlayIOBlocked`, `PlayWaitStateChanged` │ Time: `PlayWarpStart`, `PlayWarpCruise`, `PlayWarpDecelerate`, `PlayWarpArrived`, `PlayWarpCancelled`, `PlayWarpComplete` │ Persistence: `PlaySaveStarted`, `PlaySaveCompleted`, `PlaySyncCompleted` |
| `IHapticProvider` | `TapLight`, `TapMedium`, `TapHeavy`, `Buzz(float)` |
| `IEqualizerProvider` | `BandCount`, `GetBands(float[])` |

## Handler defaults

| Handler group | Default `MaxTimeScale` | Rationale |
|---------------|------------------------|----------|
| `EditorHandlers` | `float.MaxValue` | Always audible |
| `EngineHandlers` | `10` | Only in step-through mode |
| `TimeHandlers` | `float.MaxValue` | Warp sounds are the point |
| `PersistenceHandlers` | `float.MaxValue` | Always audible |
| `GatedEqualizer` | `float.MaxValue` | Always active |

## Equalizer data model

| Class | Purpose |
|-------|---------|
| `Equalizer` | Pure data — reads `IEqualizerProvider`, applies smoothing + peak hold/decay. No Unity dependency. |
| `EqualizerBridge.GatedEqualizer` | Time-scale-gated wrapper (same pattern as AudioBridge/HapticBridge). |

### Tuning knobs

| Property | Default | Effect |
|----------|---------|--------|
| `RiseSpeed` | `12` | How fast bars snap to louder levels (units/sec) |
| `FallSpeed` | `4` | How fast bars decay when quiet (units/sec) |
| `PeakHoldTime` | `0.4s` | Seconds the peak marker lingers |
| `PeakFallSpeed` | `1.5` | Speed at which peak marker descends |

## TUI integration (`CodeGamified.TUI.TUIEqualizer`)

The TUI module provides `TUIEqualizer` — a 2D frequency-band widget with
customisable dimensions. It takes raw `float[]` arrays (no Audio dependency)
so the dependency chain stays clean.

### Render styles

| Style | Description |
|-------|-------------|
| `Bars` | Vertical bars rising from the bottom with sub-character precision (▁▂▃▄▅▆▇█) |
| `Mirror` | Symmetric bars growing outward from the centre line |

### Config options

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `Width` | `int` | `32` | Total width in characters (including border) |
| `Height` | `int` | `10` | Total height in rows (including border/labels) |
| `Style` | `Style` | `Bars` | `Bars` or `Mirror` |
| `ShowBorder` | `bool` | `true` | Box-drawing frame around the widget |
| `ShowPeaks` | `bool` | `true` | Peak-hold markers above bars |
| `ShowLabels` | `bool` | `false` | Band index labels at bottom |
| `Title` | `string` | `null` | Optional text in top border |

### Visual example (8 bands, 10×6, Bars)

```
┌──── EQ ────┐
│ ▇          │
│ █ ▅   ▃   │
│ █ █ ▇ █ ▂ │
│ █ █ █ █ █ │
│ █ █ █ █ █ │
└────────────┘
```

Colors use the **CyanMagenta** gradient across bands and the brand gradient
for borders.

## Open questions

See [HANDOFF.md](HANDOFF.md) for remaining decisions.
