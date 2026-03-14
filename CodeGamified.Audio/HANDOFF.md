# CodeGamified.Audio — Handoff

## Goal

New shared package: `CodeGamified.Audio`  
Provides a game-agnostic audio/haptic feedback layer that hooks **every module's
events** (Editor, Engine, Time, Persistence). Games decide which hooks get real
clips and which stay silent.

**Time-scale-gated**: each handler group has a `MaxTimeScale` threshold.
When `getTimeScale()` exceeds it, the sound is silently skipped.

## Hookable Events (all modules)

### Editor (CodeEditorWindow / CodeDocument)

| Event | Signature | Fires when |
|-------|-----------|------------|
| `OnOptionSelected` | `Action<string>` | Player taps any option |
| `OnUndoPerformed` | `Action` | Undo executed |
| `OnRedoPerformed` | `Action` | Redo executed |
| `OnCompileError` | `Action<int>` | Compilation fails |
| `OnDocumentChanged` | `Action` | Any document mutation |

### Engine (CodeExecutor)

| Event | Signature | Fires when |
|-------|-----------|------------|
| `OnInstructionExecuted` | `Action<Instruction, MachineState>` | Each instruction |
| `OnOutput` | `Action<GameEvent>` | Game event emitted |
| `OnHalted` | `Action` | Program stopped |
| `OnIOBlocked` | `Action<Instruction>` | I/O rejected |
| `OnWaitStateChanged` | `Action<bool, float>` | Wait entered/exited |

### Time (TimeWarpController)

| Event | Signature | Fires when |
|-------|-----------|------------|
| `OnWarpStateChanged` | `Action<WarpState>` | Warp state machine transition |
| `OnWarpArrived` | `Action` | Arrived at target time |
| `OnWarpCancelled` | `Action` | Warp cancelled |
| `OnWarpComplete` | `Action` | Hold elapsed, back to idle |

### Persistence (PersistenceBehaviour)

| Event | Signature | Fires when |
|-------|-----------|------------|
| `OnSaveStarted` | `Action` | Save began |
| `OnSaveCompleted` | `Action<GitResult>` | Save finished |
| `OnSyncCompleted` | `Action<GitResult>` | Sync finished |

## Architecture

```
CodeGamified.Audio/
├── CodeGamified.Audio.asmdef   (refs: none — zero-dep)
├── IAudioProvider.cs           23 hook methods (Editor/Engine/Time/Persistence)
├── IHapticProvider.cs          intensity-based haptics
├── NullAudioProvider.cs        silent default
├── NullHapticProvider.cs       no-op default
├── AudioBridge.cs              handler classes with time-scale gate
├── HapticBridge.cs             handler classes with time-scale gate
└── README.md
```

### Zero-dep design

Audio never references Editor, Engine, Time, or Persistence. Handler methods
use primitive signatures (`void`, `string`, `int`). For events with module-specific
types (e.g. `Action<Instruction, MachineState>`), the game writes a one-line lambda:

```csharp
executor.OnInstructionExecuted += (_, _) => engineAudio.InstructionStep();
```

### Time-scale gating

Each handler group inherits `GatedHandlers` which has:
- `MaxTimeScale` — mutable threshold (tune at runtime)
- `Func<float> getTimeScale` — injected at creation

Default thresholds:
| Group | Default | Rationale |
|-------|---------|-----------|
| Editor | ∞ | Always audible |
| Engine | 10 | Step-mode only |
| Time | ∞ | Warp sounds are the point |
| Persistence | ∞ | Always audible |

## Resolved Questions

| # | Question | Decision |
|---|----------|----------|
| 1 | Bridge in Audio or game? | **B) Zero-dep.** Audio returns handler classes, game does `+=` wiring. |
| 5 | Cover all modules or just Editor? | **All modules.** Every hookable event gets a slot in IAudioProvider. Game decides which get real clips. |
| 6 | Time-dependent control? | **Built-in.** `GatedHandlers.MaxTimeScale` + `Func<float> getTimeScale`. |

## Open Questions

| # | Question | Options |
|---|----------|---------|
| 2 | Distinguish insert vs delete vs replace in `OnDocumentChanged`? | Current event is bare `Action`. Could add `Action<EditKind>` enum if needed. |
| 3 | Pool `AudioSource` components or one-shot `PlayOneShot`? | `PlayOneShot` is simpler; pooling only if overlapping sounds clip. |
| 4 | Mobile haptics: Unity `Handheld.Vibrate()` vs native plugin? | `Handheld.Vibrate()` is coarse. iOS/Android native gives granular patterns. |

## Checklist

- [x] Create `.engine/CodeGamified.Audio/` directory
- [x] Create `CodeGamified.Audio.asmdef` (zero-dep, no Engine refs)
- [x] `IAudioProvider.cs` — 23 methods across all modules
- [x] `IHapticProvider.cs` — intensity-based interface
- [x] `NullAudioProvider.cs` — silent default
- [x] `NullHapticProvider.cs` — no-op default
- [x] `AudioBridge.cs` — per-module handler classes with time-scale gating
- [x] `HapticBridge.cs` — per-module handler classes with time-scale gating
- [x] `README.md`
- [ ] Integration example in a game project (e.g. Pong or Satellite)
- [ ] Verify no new compile errors in Editor or Engine
