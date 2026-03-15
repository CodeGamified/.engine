# CodeGamified.Camera

Three-mode camera controller + optional companion components.  
Inspector-configurable, no abstract classes — attach and tune.

## Architecture

```
CameraRig (MonoBehaviour)
├── Free mode      WASD pan, scroll zoom, right-drag orbit, Q/E rotate, middle-mouse pan
├── Orbit mode     SetTarget(transform) — auto-follow, dynamic zoom limits, auto-exit on zoom-out
├── Deck mode      EnterDeckMode(transform) — first-person on target, synced pitch/roll/yaw
├── Transitions    Smooth lerps between modes, configurable speed
└── Escape         Esc → Free mode (toggleable)

CameraAmbientMotion (MonoBehaviour)
├── Sine sway      amplitudeX/Y, speed Hz
└── LookAt         Optional override to always face a point

CameraFlashlight (MonoBehaviour)
├── Auto-fade      Active = true/false → smooth intensity ramp
└── Spotlight      intensity, range, spotAngle, fadeSpeed, color
```

Replaces: `OceanCameraController` (SeaRäuber), `SimpleCameraController` (BitNaughts),
`PongCameraSway` (Pong).

## Files

| File | Purpose |
|------|---------|
| `CameraRig.cs` | Three-mode camera controller |
| `CameraMode.cs` | Enum: `Free`, `Orbit`, `Deck` |
| `CameraAmbientMotion.cs` | Additive sine-based positional sway |
| `CameraFlashlight.cs` | Auto-fading spotlight child |

## Dependencies

| Assembly | References |
|---|---|
| `CodeGamified.Camera` | — (no dependencies) |

## Integration Pattern

### CameraRig — Attach + Configure

Add `CameraRig` to your camera GameObject. Configure via Inspector:

| Inspector Group | Key Fields |
|-----------------|-----------|
| Free Mode — Pan | `enableWASDPan`, `panSpeed`, `panSprintMultiplier` |
| Orbit — Mouse/Keyboard | `orbitSpeed`, `keyRotateSpeed`, `enableKeyboardRotate` |
| Zoom | `zoomSpeed`, `minZoomDistance`, `maxZoomDistance` |
| Pitch Limits | `minPitch`, `maxPitch` |
| Orbit Target | `defaultOrbitDistance`, `defaultOrbitPitch`, `autoZoomRadiusMultiplier`, `orbitExitDistance` |
| Deck Mode | `deckOffset`, `deckMotionSync`, `deckLookSpeed`, `deckMinPitch`, `deckMaxPitch` |
| Smoothing | `transitionSpeed`, `freeSmoothness`, `orbitSmoothness`, `zoomLerpSpeed` |
| Behavior | `enableEscapeToFree`, `clampLookTargetY`, `snapTimeScaleThreshold` |

### CameraRig — Runtime API

```csharp
var rig = camera.GetComponent<CameraRig>();

// Enter orbit around a target
rig.SetTarget(shipTransform);

// Enter first-person on a target
rig.EnterDeckMode(bridgeTransform);

// Return to free camera
rig.ClearTarget();

// Auto-rotate to track a moving object while orbiting
rig.TrackObject(missileTransform);
```

### CameraAmbientMotion — Subtle Breathing

```csharp
// Attach to any camera for idle sway
var sway = camera.gameObject.AddComponent<CameraAmbientMotion>();
sway.amplitudeX = 0.5f;
sway.amplitudeY = 0.2f;
sway.speed = 0.3f;
sway.lookAtTarget = Vector3.zero;

// Reposition base
sway.SetBasePosition(newPosition);
```

### CameraFlashlight — Proximity Light

```csharp
var flash = camera.gameObject.AddComponent<CameraFlashlight>();
flash.intensity = 1.5f;
flash.range = 5f;
flash.spotAngle = 60f;

// Toggle (fades smoothly)
flash.Active = true;
flash.Active = false;
```

## Mode Transition Table

| From | To | Trigger |
|------|----|---------|
| Free | Orbit | `SetTarget(transform)` |
| Free | Deck | `EnterDeckMode(transform)` |
| Orbit | Free | `ClearTarget()` / Esc / zoom past `orbitExitDistance` |
| Orbit | Deck | `EnterDeckMode(transform)` |
| Deck | Orbit | Scroll past `deckExitHeight` |
| Deck | Free | `ClearTarget()` / Esc |
