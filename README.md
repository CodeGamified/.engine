# `.engine/` — Agent Bootstrapping Guide

Reusable Unity modules for CodeGamified games. Consumed via `git submodule`.
This document is the **minimum context an agent needs** to integrate these modules into a new game.

---

## Module Map

```
.engine/
├── Bootstrap/      Abstract MonoBehaviour base — logging, camera/time find-or-create, manager factory
├── Engine/         Python subset → AST → bytecode → time-scale-aware executor
├── Time/           Singleton simulation clock + time warp state machine
├── TUI/            Row-based monospace terminal UI → TMP rich-text → Canvas
├── Editor/         Mobile tap-to-code editor — AST mutation, option trees, zero typing
├── Persistence/    Git-backed CRUD — every save is a commit, zero hosting
├── Audio/          Provider interfaces + time-scale-gated bridge handlers
├── Camera/         Three-mode camera rig (Free/Orbit/Deck) + ambient sway + flashlight
├── Quality/        Static QualityBridge — publish/subscribe quality tier changes
├── Settings/       Static SettingsBridge — PlayerPrefs persistence + listener broadcast
├── Procedural/     Blueprint → part list → assembled GameObject hierarchy + visual state
```

## Dependency Graph

```
Audio, Camera, Quality, Settings, Procedural, Time, Engine  →  (no engine deps)
Bootstrap  →  Time
TUI        →  Unity.TextMeshPro
Editor     →  Engine + TUI + Unity.TextMeshPro
```

All other cross-module wiring happens in game code, not inside `.engine/`.

---

## Submodule Setup

```bash
git submodule add <engine-repo-url> Assets/Engine
```

All modules live in one submodule. Assembly definitions isolate compilation.

---

## 1. Bootstrap

**Extend:** `GameBootstrap` (abstract MonoBehaviour)

Provides: tagged debug logging, `EnsureCamera()`, `EnsureSimulationTime<T>()`,
`FindOrCreate<T>()`, `CreateManager<T>()`, `RunAfterFrames(action)`.

Does NOT define `Start()`/`Update()` — subclasses own their lifecycle.

```csharp
public class MyGameBootstrap : GameBootstrap
{
    protected override string LogTag => "MYGAME";

    void Start()
    {
        var cam = EnsureCamera();
        var time = EnsureSimulationTime<MySimulationTime>();
        var persistence = FindOrCreate<MyPersistence>();
        LogDivider();
        LogStatus("PLAYER", playerId);
        LogEnabled("AUDIO", audioEnabled);
        RunAfterFrames(() => StartGame());
    }
}
```

---

## 2. Engine

**Extend:** `IGameIOHandler`, `ICompilerExtension`, `ProgramBehaviour`

Pipeline: `Python source → PythonCompiler → AST → Instructions[] → CodeExecutor → MachineState`

### Implement `IGameIOHandler`

```csharp
public class MyIOHandler : IGameIOHandler
{
    public bool PreExecute(Instruction inst, MachineState state) => true;
    public void ExecuteIO(Instruction inst, MachineState state)
    {
        switch ((MyOp)(inst.Op - OpCode.CUSTOM_0))
        {
            case MyOp.SensorRead: /* read game state into register */ break;
            case MyOp.Output:     /* emit game event */ break;
        }
    }
    public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
    public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;
}
```

### Implement `ICompilerExtension`

```csharp
public class MyCompilerExtension : ICompilerExtension
{
    public void RegisterBuiltins(CompilerContext ctx)
    {
        ctx.KnownTypes.Add("Sensor");
    }
    public bool TryCompileCall(string fn, List<ExprNode> args, CompilerContext ctx, int line)
    {
        if (fn == "beep") { ctx.Emit(OpCode.CUSTOM_2, 0, 0, line); return true; }
        return false;
    }
    public bool TryCompileMethodCall(string obj, string method, ...) => false;
    public bool TryCompileObjectDecl(string type, string var, ...) => false;
}
```

### Subclass `ProgramBehaviour`

```csharp
public class MyProgram : ProgramBehaviour
{
    protected override IGameIOHandler CreateIOHandler() => new MyIOHandler();
    protected override CompiledProgram CompileSource(string source, string name)
        => PythonCompiler.Compile(source, name, new MyCompilerExtension());
    protected override void ProcessEvents() { /* handle output events */ }
}
```

### CUSTOM Opcode Convention

| Slot | Convention | Purpose |
|------|-----------|---------|
| `CUSTOM_0..2` | Messaging | Send/Recv/Peek between programs |
| `CUSTOM_3` | I/O read | Sensor reads, queries |
| `CUSTOM_4` | I/O write | Transmit, order |
| `CUSTOM_5` | Output | Beep, LED, signal |
| `CUSTOM_6` | Log | Debug output |
| `CUSTOM_7..31` | Free | Game-specific |

Convention only — games can remap freely.

---

## 3. Time

**Extend:** `SimulationTime` (abstract), optionally `TimeWarpController` (abstract)

### Subclass `SimulationTime`

```csharp
public class MySimulationTime : SimulationTime
{
    protected override float MaxTimeScale => 100f;
    protected override void OnInitialize()
    {
        timeScalePresets = new[] { 1f, 5f, 10f, 50f, 100f };
    }
    public override string GetFormattedTime()
    {
        int mins = (int)(simulationTime / 60);
        int secs = (int)(simulationTime % 60);
        return $"{mins:D2}:{secs:D2}";
    }
    // Optional sun model stubs: GetSunDirection(), GetTimeOfDay(), IsDaytime()
}
```

Built-in: `simulationTime` (double), `timeScale`/`isPaused`, Space=pause, +/-=presets,
events `OnSimulationTimeChanged`, `OnTimeScaleChanged`, `OnPausedChanged`.

### Subclass `TimeWarpController` (optional)

State machine: Idle → Accelerating → Cruising → Decelerating → Arrived.
Call `WarpToTime(double)`, `CancelWarp()`. Override hooks: `OnWarpStarting()`,
`OnWarpUpdating()`, `OnWarpArriving()`, `OnWarpCompleting()`.

---

## 4. TUI

**Extend:** `TerminalWindow` (abstract MonoBehaviour)

### Subclass `TerminalWindow`

```csharp
public class MyTerminal : TerminalWindow
{
    protected override void Awake() { base.Awake(); windowTitle = "LOG"; totalRows = 20; }
    protected override void Render()
    {
        RenderHeader();
        SetRow(ROW_SEP_TOP, Separator());
        RenderScrollback();
        SetRow(RowSepBot, Separator());
        SetRow(RowActions, TUIColors.Dimmed("  [ESC] close"));
    }
}
```

### Primitives (11 pure static classes, no MonoBehaviour)

| Class | Key Methods |
|-------|------------|
| `TUIColors` | `Fg(color,text)`, `Bold()`, `Dimmed()` — Color32 palette |
| `TUIGlyphs` | Box-drawing, blocks, spinners, status icons |
| `TUIConfig` | Brand gradient stops, `Load(json)`, `Reset()` |
| `TUIGradient` | `Lerp(stops,t)`, `Sample(t)`, `MakeLoop()` |
| `TUIText` | `StripTags()`, `VisibleLength()`, `Truncate()` |
| `TUIEasing` | `Smoothstep(t)`, `Smootherstep(t)` |
| `TUIEffects` | `ScrambleText(target,age)`, `GradientColorize(text)` |
| `TUILayout` | `CenterText(text,w)`, `RightAlign(text,w)` |
| `TUIWidgets` | `ProgressBar()`, `SpinnerFrame()`, `Divider()`, `Box()` |
| `TUIAnimation`| `DecodeRows()`, `FadeoutRows()`, `ProgressFrame()` |
| `TUIFormat` | `Duration(s)`, `TimeColor(s)`, `ColoredDuration(s)` |

### Column Modes (opt-in per row)

```csharp
InitializeDualColumns(splitRatio: 0.5f);                          // left + right
rows[i].SetThreePanelMode(true, col2Start, col3Start);            // code debugger
rows[i].SetTripleColumnMode(true);                                // status bar
```

### Slider/Button Overlays (opt-in per row)

```csharp
rows[i].CreateSliderOverlay(startChar: 10, widthChars: 14);
rows[i].CreateButtonOverlay("OK", charPos: 2, width: 6, onClick: Accept);
```

Also available: `CodeDebuggerWindow` (three-panel source/asm/state), `StatusBarBase` (triple-column).

### Glassmorphic Blur (zero-config, URP only)

Frosted-glass panel backgrounds. Lives in `TUI/Blur/` with its own asmdef (`CodeGamified.TUI.Blur`)
so core TUI stays URP-agnostic. Zero setup required — editor auto-adds the render feature to the URP
renderer, runtime auto-creates materials and toggles blur on Ultra quality via `QualityBridge`.
See TUI README for tuning parameters.

---

## 5. Editor

**Extend:** `IEditorExtension`

Depends on Engine + TUI. AST-based — players edit nodes, not text. Syntax errors impossible.

### Implement `IEditorExtension`

```csharp
public class MyEditorExtension : IEditorExtension
{
    public List<EditorTypeInfo> GetAvailableTypes() => new()
    {
        new() { Name = "Sensor", Hint = "environment sensor" }
    };
    public List<EditorFuncInfo> GetAvailableFunctions() => new()
    {
        new() { Name = "beep", Hint = "beep(freq)", ArgCount = 1 }
    };
    public List<EditorMethodInfo> GetMethodsForType(string typeName) => typeName switch
    {
        "Sensor" => new() { new() { Name = "read", ArgCount = 0, HasReturnValue = true } },
        _ => new()
    };
    public List<string> GetVariableNameSuggestions() => new() { "signal", "heading" };
    public bool IsWhileLoopAllowed() => true;
    public bool IsForLoopAllowed() => true;
}
```

### Open the Editor

```csharp
editor.OpenNew("program_01", new MyCompilerExtension(), new MyEditorExtension());
editor.OnCompileAndRun += program => executor.LoadProgram(program);
```

Features: undo/redo, clipboard copy/cut/paste, duplicate, inline compile errors,
tier-gated loops, multi-arg chaining, delete confirmation for compound statements.

---

## 6. Persistence

**Extend:** `IEntitySerializer<T>`, optionally `PersistenceBehaviour`

CRUD maps to git: Create=add+commit, Read=worktree, Update=overwrite+commit,
Delete=rm+commit, Share=push/pull. Three-tier: local→GitHub fork→public registry.

### Bootstrap the Save Repo

```csharp
var identity = new PlayerIdentity { PlayerId = "alice" };
var repo = new LocalGitProvider(Application.persistentDataPath + "/save-repo", identity);
repo.EnsureInitialized();
```

### Implement `IEntitySerializer<T>`

```csharp
public class ProgramSerializer : IEntitySerializer<PlayerProgram>
{
    public int SchemaVersion => 1;
    public string Serialize(PlayerProgram p) => JsonUtility.ToJson(p, true);
    public PlayerProgram Deserialize(string json) => JsonUtility.FromJson<PlayerProgram>(json);
}
```

### Use `EntityStore<T>`

```csharp
var programs = new EntityStore<PlayerProgram>(repo, new ProgramSerializer(), "programs");
programs.Save("alice", "autopilot", myProgram, "fixed bug");
var p = programs.Load("alice", "autopilot");
var names = programs.ListNames("alice");
```

### Opt into Sharing

```csharp
identity.GitHubUsername = "alice";
repo.SetRemote(identity.RemoteUrl);
repo.SyncNow(); // push to player's own fork
```

### Merge Strategies

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| `IncomingWins` | Remote wins | Default |
| `LocalWins` | Local wins | Player prefs |
| `HigherWins` | Larger number | High scores |
| `LowerWins` | Smaller number | Best time |

Providers: `LocalGitProvider` (desktop), `MemoryGitProvider` (tests),
`PublicRepoReader` (read other players' repos, no auth).

---

## 7. Audio

**Extend:** `IAudioProvider`, optionally `IHapticProvider`

Zero dependencies on other modules. Game code wires module events → bridge handlers.
Each handler group has a `MaxTimeScale` threshold — sounds auto-mute during fast time.

### Implement `IAudioProvider`

```csharp
public class MyAudio : MonoBehaviour, IAudioProvider
{
    [SerializeField] AudioClip tap, error;
    AudioSource _src;
    void Awake() => _src = GetComponent<AudioSource>();

    // Implement all methods from IAudioProvider:
    //   Editor:      PlayTap, PlayInsert, PlayDelete, PlayUndo, PlayRedo,
    //                PlayCompileSuccess, PlayCompileError, PlayNavigate
    //   Engine:      PlayInstructionStep, PlayOutput, PlayHalted, PlayIOBlocked, PlayWaitStateChanged
    //   Time:        PlayWarpStart..PlayWarpComplete (6 methods)
    //   Persistence: PlaySaveStarted, PlaySaveCompleted, PlaySyncCompleted
    // Leave methods empty for sounds you don't need.
}
```

### Wire Bridges in Bootstrap

```csharp
Func<float> ts = () => Time.timeScale;
var editorAudio = AudioBridge.ForEditor(audio, ts);
var engineAudio = AudioBridge.ForEngine(audio, ts);   // MaxTimeScale = 10
var timeAudio   = AudioBridge.ForTime(audio, ts);
var persistAudio = AudioBridge.ForPersistence(audio, ts);

editor.OnCompileError    += editorAudio.CompileError;
executor.OnHalted        += engineAudio.Halted;
warp.OnWarpArrived       += timeAudio.WarpArrived;
persistence.OnSaveStarted += persistAudio.SaveStarted;
```

Null providers: `NullAudioProvider`, `NullHapticProvider` — silent no-ops for tests.

---

## 8. Camera

**Use directly:** `CameraRig`, `CameraAmbientMotion`, `CameraFlashlight`

No abstract classes — configure via Inspector fields.

### `CameraRig` — Three Modes

| Mode | Input | Behavior |
|------|-------|----------|
| Free | WASD pan, scroll zoom, right-drag orbit | No target lock |
| Orbit | `SetTarget(transform)` | Auto-follow, dynamic zoom limits, auto-exit on zoom-out |
| Deck | `EnterDeckMode(transform)` | First-person on target, synced to pitch/roll/yaw, mouse look |

API: `SetTarget()`, `EnterDeckMode()`, `ClearTarget()`, `TrackObject()`.
Esc returns to Free mode. All transitions smoothly lerped.

### `CameraAmbientMotion` — Additive Sine Sway

Attach to any camera. Fields: `amplitudeX`, `amplitudeY`, `speed`, `lookAtTarget`.
Works standalone or alongside `CameraRig`.

### `CameraFlashlight` — Auto-Fading Spotlight

Set `Active = true/false`. Fades smoothly. Fields: `intensity`, `range`, `spotAngle`, `fadeSpeed`.

---

## 9. Quality

**Use directly:** `QualityBridge` (static), implement `IQualityResponsive`

```csharp
// Settings UI:
QualityBridge.SetTier(QualityTier.High);

// Any MonoBehaviour:
void OnEnable()  => QualityBridge.Register(this);
void OnDisable() => QualityBridge.Unregister(this);
public void OnQualityChanged(QualityTier tier) { /* rebuild mesh, adjust LOD */ }

// One-off:
QualityBridge.OnTierChanged += tier => UpdateShadowDistance(tier);
```

---

## 10. Settings

**Use directly:** `SettingsBridge` (static), implement `ISettingsListener`

Persists to PlayerPrefs. Categories: `Quality`, `Audio`, `Display`.

```csharp
// Set:
SettingsBridge.SetMasterVolume(0.8f);
SettingsBridge.SetFontSize(14f);
SettingsBridge.SetQualityLevel(2);

// Listen:
void OnEnable()  => SettingsBridge.Register(this);
void OnDisable() => SettingsBridge.Unregister(this);
public void OnSettingsChanged(SettingsSnapshot s, SettingsCategory c)
{
    if (c == SettingsCategory.Display) ResizeTerminal(s.FontSize);
}
```

Snapshot fields: `QualityLevel` (0-3), `MasterVolume`, `MusicVolume`, `SfxVolume`, `FontSize`.

---

## 11. Procedural

**Extend:** `IProceduralBlueprint`, create `ColorPalette` asset

Blueprint emits `ProceduralPartDef[]` → `ProceduralAssembler.Build()` → GameObject hierarchy.

### Implement `IProceduralBlueprint`

```csharp
public class MyBlueprint : IProceduralBlueprint
{
    public string DisplayName => "Player Ship";
    public string PaletteId => "nautical";
    public ProceduralLODHint LODHint => ProceduralLODHint.Standard;

    public ProceduralPartDef[] GetParts() => new[]
    {
        new ProceduralPartDef("hull", PrimitiveType.Cube,
            Vector3.zero, new Vector3(2, 0.5f, 4), "hull_wood"),
        new ProceduralPartDef("mast", PrimitiveType.Cylinder,
            new Vector3(0, 2, 0), new Vector3(0.1f, 2, 0.1f), "mast_dark")
        { ParentId = "hull", Collider = ColliderMode.Box }
    };
}
```

### Build

```csharp
var result = ProceduralAssembler.Build(blueprint, palette);
// result.Root = assembled GameObject
// result.Renderers = Dictionary<string, Renderer> keyed by part ID
```

### `ColorPalette`

ScriptableObject asset — Inspector-editable key→Color map.
Or create at runtime: `ColorPalette.CreateRuntime(dict)`.

### `ProceduralVisualState` — Animation

Attach to assembled root. Two modes:
- **Imperative:** `Pulse(partId, color, duration)`, `Throb(partId, scale, duration)`
- **Declarative:** `Bind(partId, channel, source, min, max)` — continuously drives
  `Emission`, `ScaleY`, `ColorAlpha`, `PositionY`, or `ColorTint` from a `Func<float>`.

### `ProceduralPartDef` Fields

`Id`, `Shape` (PrimitiveType), `CustomMesh`, `LocalPos`, `LocalScale`, `LocalRot`,
`ColorKey`, `Tag`, `Collider` (None/Box/Mesh/ConvexMesh), `ParentId`, `Layer`.

---

## Assembly Definitions

| Assembly | Dependencies |
|---|---|
| `CodeGamified.Bootstrap` | `CodeGamified.Time` |
| `CodeGamified.Engine` | — |
| `CodeGamified.Time` | — |
| `CodeGamified.TUI` | `Unity.TextMeshPro` |
| `CodeGamified.TUI.Blur` | `Unity.RenderPipelines.Universal.Runtime`, `Unity.RenderPipelines.Core.Runtime` |
| `CodeGamified.Editor` | `CodeGamified.Engine`, `CodeGamified.TUI`, `Unity.TextMeshPro` |
| `CodeGamified.Persistence` | — |
| `CodeGamified.Audio` | — (`noEngineReferences: true`) |
| `CodeGamified.Camera` | — |
| `CodeGamified.Quality` | — |
| `CodeGamified.Settings` | — |
| `CodeGamified.Procedural` | — |

Test assemblies: `*.Tests` — Editor-only, depend on parent + `nunit.framework`.

---

## Extension Point Summary

| What to Extend | Module | Game Provides |
|---|---|---|
| `GameBootstrap` | Bootstrap | Startup sequence, manager creation |
| `IGameIOHandler` | Engine | Custom opcode execution |
| `ICompilerExtension` | Engine | Game builtins, known types |
| `ProgramBehaviour` | Engine | MonoBehaviour lifecycle for programs |
| `SimulationTime` | Time | Max scale, presets, sun model, formatting |
| `TimeWarpController` | Time | Warp hooks (camera, spawning) |
| `TerminalWindow` | TUI | Concrete terminal panels |
| `CodeDebuggerWindow` | TUI | Three-panel debugger |
| `StatusBarBase` | TUI | Status bar sections |
| `IEditorExtension` | Editor | Types, functions, methods, variable names, tier gates |
| `IEntitySerializer<T>` | Persistence | Entity serialization |
| `PersistenceBehaviour` | Persistence | Autosave + sync |
| `IAudioProvider` | Audio | Sound effects per module event |
| `IHapticProvider` | Audio | Vibration feedback |
| `IQualityResponsive` | Quality | React to quality tier changes |
| `ISettingsListener` | Settings | React to any setting change |
| `IProceduralBlueprint` | Procedural | Part list for assembly |

See each module's own `README.md` for full API details.