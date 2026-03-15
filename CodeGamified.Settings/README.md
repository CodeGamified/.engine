# CodeGamified.Settings

Static settings hub — PlayerPrefs persistence + publish/subscribe broadcast.  
Single source of truth for all user-facing settings across modules.

## Architecture

```
SettingsBridge (static)
├── Quality         SetQualityLevel(0-3) → QualitySettings + notify
├── Audio           SetMasterVolume(), SetMusicVolume(), SetSfxVolume() → AudioListener + notify
├── Display         SetFontSize(8-24) → notify
├── Persistence     Load() / Save() → PlayerPrefs
├── Listeners       Register/Unregister ISettingsListener
└── Event           OnChanged(SettingsSnapshot, SettingsCategory)

SettingsSnapshot (readonly struct)
├── QualityLevel    int (0=Low, 3=Ultra)
├── MasterVolume    float (0-1)
├── MusicVolume     float (0-1)
├── SfxVolume       float (0-1)
└── FontSize        float (8-24)

SettingsCategory (enum)
├── Quality
├── Audio
└── Display

ISettingsListener (interface)
└── OnSettingsChanged(SettingsSnapshot, SettingsCategory)
```

## Files

| File | Purpose |
|------|---------|
| `SettingsBridge.cs` | Static hub — state, PlayerPrefs load/save, setters, listener registry |
| `SettingsSnapshot.cs` | Readonly struct snapshot of all settings |
| `SettingsCategory.cs` | Enum: Quality, Audio, Display |
| `ISettingsListener.cs` | Interface for MonoBehaviours that react to setting changes |

## Dependencies

| Assembly | References |
|---|---|
| `CodeGamified.Settings` | — (no dependencies) |

## Integration Pattern

### Settings UI — Set Values

```csharp
using CodeGamified.Settings;

SettingsBridge.SetQualityLevel(2);       // High
SettingsBridge.SetMasterVolume(0.8f);
SettingsBridge.SetMusicVolume(0.5f);
SettingsBridge.SetSfxVolume(1.0f);
SettingsBridge.SetFontSize(14f);
```

### Read Current Values

```csharp
float vol = SettingsBridge.MasterVolume;
int quality = SettingsBridge.QualityLevel;
SettingsSnapshot snap = SettingsBridge.Snapshot;
```

### MonoBehaviour — React to Changes

```csharp
using CodeGamified.Settings;

public class MyTerminal : MonoBehaviour, ISettingsListener
{
    void OnEnable()  => SettingsBridge.Register(this);
    void OnDisable() => SettingsBridge.Unregister(this);

    public void OnSettingsChanged(SettingsSnapshot s, SettingsCategory c)
    {
        if (c == SettingsCategory.Display)
            ResizeFont(s.FontSize);
        if (c == SettingsCategory.Audio)
            mixer.SetFloat("Master", Mathf.Log10(s.MasterVolume) * 20);
    }
}
```

### One-Off Listener

```csharp
SettingsBridge.OnChanged += (snap, cat) => Debug.Log($"{cat} changed");
```

### Persistence

```csharp
SettingsBridge.Load();  // Read from PlayerPrefs (call at boot)
SettingsBridge.Save();  // Write to PlayerPrefs (auto-called by setters)
```

## Defaults

| Setting | Default | Range |
|---------|---------|-------|
| QualityLevel | 3 (Ultra) | 0–3 |
| MasterVolume | 1.0 | 0–1 |
| MusicVolume | 0.7 | 0–1 |
| SfxVolume | 1.0 | 0–1 |
| FontSize | 13 | 8–24 |

## PlayerPrefs Keys

`CG_QualityLevel`, `CG_MasterVolume`, `CG_MusicVolume`, `CG_SfxVolume`, `CG_FontSize`.
