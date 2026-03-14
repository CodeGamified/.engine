# CodeGamified.Time

Shared simulation time framework for educational programming games.  
Singleton time management + time warp state machine.

## Architecture

```
SimulationTime (abstract MonoBehaviour)
├── simulationTime          double, seconds since game epoch
├── timeScale / isPaused    float + bool, clamped to MaxTimeScale
├── timeScalePresets[]      Inspector-configurable speed presets
├── Events                  OnSimulationTimeChanged, OnTimeScaleChanged, OnPausedChanged
├── Input                   Space=pause, +/-=scale presets
├── Virtual stubs           GetSunDirection(), GetSunAltitude(), IsDaytime(), GetTimeOfDay()
└── Abstract                MaxTimeScale (property), GetFormattedTime() (display string)

TimeWarpController (abstract MonoBehaviour)
├── State machine           Idle → Accelerating → Cruising → Decelerating → Arrived → Idle
├── Smooth curves           Cubic ease-in acceleration, cubic ease-out deceleration
├── WarpToTime(double)      Start warp to target simulation time
├── CancelWarp()            Abort and restore previous time scale
├── GetTimeRemaining()      Sim-seconds to target + human-readable formatter
├── Events                  OnWarpStateChanged, OnWarpArrived, OnWarpCancelled, OnWarpComplete
└── Virtual hooks           OnWarpStarting(), OnWarpUpdating(), OnWarpArriving(), OnWarpCompleting()
```

## Integration Pattern

### 1. Subclass `SimulationTime`

```csharp
using CodeGamified.Time;

public class PirateSimulationTime : SimulationTime
{
    [Header("Day/Night")]
    public float dayLengthSeconds = 600f;
    public float startingHour = 10f;

    protected override float MaxTimeScale => 100f;

    protected override void OnInitialize()
    {
        timeScalePresets = new[] { 1f, 2f, 5f, 10f, 25f, 50f, 100f };
        simulationTime = (startingHour / 24f) * dayLengthSeconds;
    }

    public override float GetTimeOfDay()
    {
        float progress = (float)(simulationTime % dayLengthSeconds) / dayLengthSeconds;
        return progress * 24f;
    }

    public override Vector3 GetSunDirection()
    {
        float hour = GetTimeOfDay();
        float angle = ((hour - 6f) / 24f) * 2f * Mathf.PI;
        return new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.3f).normalized;
    }

    public override string GetFormattedTime()
    {
        int day = (int)(simulationTime / dayLengthSeconds);
        float hour = GetTimeOfDay();
        return $"Day {day + 1} — {(int)hour:D2}:{(int)((hour % 1) * 60):D2}";
    }
}
```

### 2. Subclass `TimeWarpController` (optional)

```csharp
using CodeGamified.Time;

public class LaunchWarpController : TimeWarpController
{
    public SatelliteData targetLaunch;

    public bool WarpToLaunch(SatelliteData launch)
    {
        targetLaunch = launch;
        double targetJ2000 = CalculateLaunchTime(launch);
        return WarpToTime(targetJ2000);
    }

    protected override void OnWarpStarting(double targetTime)
    {
        FocusCameraOnLaunchSite(targetLaunch);
    }

    protected override void OnWarpArriving()
    {
        SpawnLaunchVehicle(targetLaunch);
    }

    protected override void OnWarpCompleting()
    {
        targetLaunch = null;
    }
}
```

### 3. Wire to `IGameIOHandler`

```csharp
// In your IGameIOHandler implementation:
public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;
```

## Game Implementations

| Game | MaxTimeScale | Presets | Sun Model | Time Format |
|------|-------------|---------|-----------|-------------|
| Pong | 1,000 | 0–1000 (11 steps) | Stub (always noon) | `MM:SS` |
| SeaRäuber | 100 | 1–100 (7 steps) | XY-plane orbit, Z tilt | `Day N — HH:MM` |
| BitNaughts | 100,000,000 | 1–100M (9 steps) | Fixed +X, Earth rotates | `Day NNN HH:MM:SS` + calendar |

## Assembly Definition

| Assembly | Dependencies |
|---|---|
| `CodeGamified.Time` | — |
