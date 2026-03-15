# CodeGamified.Bootstrap

Abstract MonoBehaviour base class for game startup sequences.  
Logging, camera/time find-or-create, manager factory — no lifecycle opinions.

## Architecture

```
GameBootstrap (abstract MonoBehaviour)
├── Logging            Log(), LogDivider(), LogStatus(), LogEnabled() — tagged, conditional
├── Camera             EnsureCamera() — find Main Camera or create one with AudioListener
├── SimulationTime     EnsureSimulationTime<T>() — find or create typed singleton
├── Manager factory    CreateManager<T>(), FindOrCreate<T>() — GameObject + component
└── Boot sequencing    RunAfterFrames(action, N) — wait for Start() to settle
```

Does NOT define `Start()` / `Update()` / `OnDestroy()` — subclasses own their lifecycle.

## Files

| File | Purpose |
|------|---------|
| `GameBootstrap.cs` | Abstract base with all helpers |

## Dependencies

| Assembly | References |
|---|---|
| `CodeGamified.Bootstrap` | `CodeGamified.Time` |

## Integration Pattern

### Subclass `GameBootstrap`

```csharp
using CodeGamified.Bootstrap;

public class PongBootstrap : GameBootstrap
{
    protected override string LogTag => "PONG";

    void Start()
    {
        var cam = EnsureCamera();
        var time = EnsureSimulationTime<PongSimulationTime>();
        var persistence = FindOrCreate<PongPersistence>();

        LogDivider();
        LogStatus("PLAYER", playerId);
        LogEnabled("AUDIO", audioEnabled);
        LogEnabled("HAPTIC", hapticEnabled, "iPhone Taptic");

        RunAfterFrames(() =>
        {
            StartGame();
            Log("Boot complete.");
        });
    }
}
```

## API

| Method | Purpose |
|--------|---------|
| `Log(message)` | `Debug.Log` with `[TAG]` prefix, gated by `debugLogging` bool |
| `LogDivider()` | Separator line `────…` |
| `LogStatus(label, value)` | Formatted `LABEL │ value` |
| `LogEnabled(label, bool, detail?)` | `LABEL │ ✅ ACTIVE` or `── disabled` |
| `EnsureCamera(existing?)` | Find or create Main Camera + AudioListener |
| `EnsureSimulationTime<T>()` | Find or create typed SimulationTime singleton |
| `CreateManager<T>(name?)` | New GameObject + AddComponent |
| `FindOrCreate<T>(name?)` | FindAnyObjectByType or create |
| `RunAfterFrames(action, N=2)` | Coroutine delay — wait for all Start() to finish |
