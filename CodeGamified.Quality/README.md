# CodeGamified.Quality

Static publish/subscribe hub for quality tier changes.  
One setter, many listeners — no per-component FindObjectByType scatter.

## Architecture

```
QualityBridge (static)
├── CurrentTier          QualityTier enum (Low, Medium, High, Ultra)
├── SetTier(tier)        Updates QualitySettings, notifies all listeners
├── Register/Unregister  IQualityResponsive listener list
└── OnTierChanged        Action<QualityTier> event for one-off listeners

IQualityResponsive (interface)
└── OnQualityChanged(tier)   Rebuild mesh, adjust LOD, toggle effects
```

## Files

| File | Purpose |
|------|---------|
| `QualityBridge.cs` | Static hub — tier state, listener registry, notification |
| `IQualityResponsive.cs` | Interface for MonoBehaviours that react to quality changes |

## Dependencies

| Assembly | References |
|---|---|
| `CodeGamified.Quality` | — (no dependencies) |

## Integration Pattern

### Settings UI — Set the Tier

```csharp
using CodeGamified.Quality;

QualityBridge.SetTier(QualityTier.High);
```

This calls `QualitySettings.SetQualityLevel()` and notifies all listeners.

### MonoBehaviour — React to Changes

```csharp
using CodeGamified.Quality;

public class ProceduralShip : MonoBehaviour, IQualityResponsive
{
    void OnEnable()  => QualityBridge.Register(this);
    void OnDisable() => QualityBridge.Unregister(this);

    public void OnQualityChanged(QualityTier tier)
    {
        ribCount = tier >= QualityTier.High ? 12 : 6;
        RebuildHullMesh();
    }
}
```

### One-Off Listener — Event

```csharp
QualityBridge.OnTierChanged += tier => UpdateShadowDistance(tier);
```

### Diagnostics

```csharp
Debug.Log($"Quality listeners: {QualityBridge.ListenerCount}");
```

## QualityTier Enum

| Value | Meaning |
|-------|---------|
| `Low` | Minimum geometry, no shadows |
| `Medium` | Reduced detail |
| `High` | Full detail, soft shadows |
| `Ultra` | Maximum (default) |

## Cleanup

`QualityBridge` auto-removes destroyed `UnityEngine.Object` listeners during notification.
No manual cleanup needed for objects that get destroyed.
