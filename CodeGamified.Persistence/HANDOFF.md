# HANDOFF — Wiring `.data` into Persistence

How each game connects its `.data` submodule (the player's fork of `player-data`)
to `CodeGamified.Persistence`.

## The `.data` Submodule

Each game repo has a `.data/` folder that IS the player's git repo — the same repo
they fork from `codegamified/player-data`. The game reads/writes to it via
`LocalGitProvider`, and the Persistence framework handles commits, push, pull.

```
GameRepo/
├── Assets/
│   ├── .engine/                    ← shared submodule (this repo)
│   │   ├── CodeGamified.Engine/
│   │   ├── CodeGamified.Time/
│   │   ├── CodeGamified.TUI/
│   │   └── CodeGamified.Persistence/
│   ├── .data/                      ← player's fork (git submodule OR local repo)
│   │   ├── programs/
│   │   ├── {game-category}/        ← scripts | satellites | ships
│   │   └── config.json
│   └── Scripts/                    ← game-specific code
│       ├── Persistence/
│       │   ├── {Game}Serializer.cs
│       │   └── {Game}Persistence.cs
│       └── ...
```

## Per-Game Entity Map

| Game | `.data` Category | Entity Type | EntityStore Category | Template Repo Folder |
|------|-----------------|-------------|---------------------|---------------------|
| **Pong** | `scripts/` | `PongScript` | `"scripts"` | `scripts/` |
| **BitNaughts** | `satellites/` | `SatelliteConfig` | `"satellites"` | `satellites/` |
| **SeaRauber** | `ships/` | `ShipConfig` | `"ships"` | `ships/` |

All games also share `programs/` for user-written code (via Engine).

## Step-by-Step Wiring

### 1. Fork the template repo (one-time, per player)

The player forks `codegamified/player-data`. The game can create `.data/` as either:

- **A git submodule** pointing at the player's fork (if they've opted into sharing), OR
- **A local git repo** auto-created by `LocalGitProvider.EnsureInitialized()` (Tier 0)

```bash
# Option A: submodule (Tier 1 — player has a fork)
git submodule add https://github.com/alice/player-data.git Assets/.data

# Option B: local repo (Tier 0 — no GitHub, game auto-creates on first launch)
# Nothing to do — LocalGitProvider.EnsureInitialized() handles it
```

### 2. Add the game category to the template repo

Each game adds its own folder to the `codegamified/player-data` template:

```
player-data/               ← codegamified/player-data template
├── README.md
├── .gitignore
├── config.json
├── programs/              ← shared across all games (user code)
├── scripts/               ← Pong
├── satellites/            ← BitNaughts
└── ships/                 ← SeaRauber
```

### 3. Define the entity type (game-side)

```csharp
// ── Pong ────────────────────────────────────────────────
[Serializable]
public class PongScript
{
    public string name;
    public string source;        // Python source code
    public int tier;             // unlock tier
    public float bestScore;
}

// ── BitNaughts ──────────────────────────────────────────
[Serializable]
public class SatelliteConfig
{
    public string name;
    public string source;
    public string orbitType;     // "LEO", "GEO", "polar"
    public float altitude;
    public string[] instruments; // ["camera","spectrometer"]
}

// ── SeaRauber ───────────────────────────────────────────
[Serializable]
public class ShipConfig
{
    public string name;
    public string source;
    public string hullType;      // "sloop", "frigate", "galleon"
    public int cannonCount;
    public float cargoCapacity;
}
```

### 4. Implement `IEntitySerializer<T>` (game-side)

```csharp
// ── Pong ────────────────────────────────────────────────
public class PongScriptSerializer : IEntitySerializer<PongScript>
{
    public int SchemaVersion => 1;
    public string Serialize(PongScript s) => JsonUtility.ToJson(s, true);
    public PongScript Deserialize(string json) => JsonUtility.FromJson<PongScript>(json);
}

// ── BitNaughts ──────────────────────────────────────────
public class SatelliteSerializer : IEntitySerializer<SatelliteConfig>
{
    public int SchemaVersion => 1;
    public string Serialize(SatelliteConfig s) => JsonUtility.ToJson(s, true);
    public SatelliteConfig Deserialize(string json) => JsonUtility.FromJson<SatelliteConfig>(json);
}

// ── SeaRauber ───────────────────────────────────────────
public class ShipSerializer : IEntitySerializer<ShipConfig>
{
    public int SchemaVersion => 1;
    public string Serialize(ShipConfig s) => JsonUtility.ToJson(s, true);
    public ShipConfig Deserialize(string json) => JsonUtility.FromJson<ShipConfig>(json);
}
```

### 5. Subclass `PersistenceBehaviour` (game-side)

This is the main wiring point. Each game creates ONE MonoBehaviour that:
- Points `LocalGitProvider` at `.data/`
- Creates `EntityStore<T>` instances for its categories
- Defines what "save" means

```csharp
// ── Pong ────────────────────────────────────────────────
using CodeGamified.Persistence;
using CodeGamified.Persistence.Providers;

public class PongPersistence : PersistenceBehaviour
{
    public string playerId = "player";

    EntityStore<PongScript> _scripts;
    LocalGitProvider _localRepo;

    // Track what needs saving
    readonly List<PongScript> _dirty = new List<PongScript>();

    void Start()
    {
        var identity = new PlayerIdentity { PlayerId = playerId };

        // Point at .data/ — this IS the git repo
        string dataPath = System.IO.Path.Combine(Application.dataPath, ".data");
        _localRepo = new LocalGitProvider(dataPath, identity);
        _localRepo.EnsureInitialized();

        Initialize(_localRepo);

        _scripts = new EntityStore<PongScript>(_localRepo, new PongScriptSerializer(), "scripts");
        autosaveInterval = 30f;
    }

    protected override GitResult PerformSave(IGitRepository repo)
    {
        GitResult last = GitResult.Ok();
        foreach (var s in _dirty)
            last = _scripts.Save(playerId, s.name, s, $"save {s.name}");
        _dirty.Clear();
        return last;
    }

    // ── Public API for game code ─────────────────────────
    public void SaveScript(PongScript s) { _dirty.Add(s); MarkDirty(); }
    public PongScript LoadScript(string name) => _scripts.Load(playerId, name);
    public IReadOnlyList<string> ListScripts() => _scripts.ListNames(playerId);
}
```

```csharp
// ── BitNaughts ──────────────────────────────────────────
public class BitNaughtsPersistence : PersistenceBehaviour
{
    public string playerId = "player";

    EntityStore<SatelliteConfig> _satellites;
    LocalGitProvider _localRepo;
    readonly List<SatelliteConfig> _dirty = new List<SatelliteConfig>();

    void Start()
    {
        var identity = new PlayerIdentity { PlayerId = playerId };
        string dataPath = System.IO.Path.Combine(Application.dataPath, ".data");
        _localRepo = new LocalGitProvider(dataPath, identity);
        _localRepo.EnsureInitialized();
        Initialize(_localRepo);

        _satellites = new EntityStore<SatelliteConfig>(
            _localRepo, new SatelliteSerializer(), "satellites");
        autosaveInterval = 60f;
    }

    protected override GitResult PerformSave(IGitRepository repo)
    {
        GitResult last = GitResult.Ok();
        foreach (var s in _dirty)
            last = _satellites.Save(playerId, s.name, s, $"save satellite {s.name}");
        _dirty.Clear();
        return last;
    }

    public void SaveSatellite(SatelliteConfig s) { _dirty.Add(s); MarkDirty(); }
    public SatelliteConfig LoadSatellite(string name) => _satellites.Load(playerId, name);
    public IReadOnlyList<string> ListSatellites() => _satellites.ListNames(playerId);
}
```

```csharp
// ── SeaRauber ───────────────────────────────────────────
public class SeaRauberPersistence : PersistenceBehaviour
{
    public string playerId = "player";

    EntityStore<ShipConfig> _ships;
    LocalGitProvider _localRepo;
    readonly List<ShipConfig> _dirty = new List<ShipConfig>();

    void Start()
    {
        var identity = new PlayerIdentity { PlayerId = playerId };
        string dataPath = System.IO.Path.Combine(Application.dataPath, ".data");
        _localRepo = new LocalGitProvider(dataPath, identity);
        _localRepo.EnsureInitialized();
        Initialize(_localRepo);

        _ships = new EntityStore<ShipConfig>(
            _localRepo, new ShipSerializer(), "ships");
        autosaveInterval = 60f;
    }

    protected override GitResult PerformSave(IGitRepository repo)
    {
        GitResult last = GitResult.Ok();
        foreach (var s in _dirty)
            last = _ships.Save(playerId, s.name, s, $"save ship {s.name}");
        _dirty.Clear();
        return last;
    }

    public void SaveShip(ShipConfig s) { _dirty.Add(s); MarkDirty(); }
    public ShipConfig LoadShip(string name) => _ships.Load(playerId, name);
    public IReadOnlyList<string> ListShips() => _ships.ListNames(playerId);
}
```

### 6. Wire up Tier 1 sharing (game-side, optional)

When the player opts in, point the local repo at their fork:

```csharp
// Player provides GitHub username
identity.GitHubUsername = "alice";
_localRepo.SetRemote(identity.RemoteUrl);

// Trigger push/pull
SyncNow();
```

### 7. Wire up Tier 2 discovery (game-side, optional)

Browse and import from other players:

```csharp
// Fetch the registry (built nightly by GitHub Action)
var registry = new PlayerRegistry();
registry.Refresh();

// Search for players
var results = registry.Search("bob");

// Import bob's satellite config
var bobReader = registry.GetReader("bob");
string json = bobReader.Load("satellites/spy-sat.json");
var imported = satelliteSerializer.Deserialize(json);
_satellites.Save(playerId, "imported-spy-sat", imported, "imported from bob");
```

## Checklist

Per-game wiring checklist — everything lives in the game repo, not in `.engine/`:

```
□ 1. Define entity type(s)              [Serializable] class with game fields
□ 2. Implement IEntitySerializer<T>      JsonUtility roundtrip + SchemaVersion
□ 3. Subclass PersistenceBehaviour       Point at .data/, create EntityStore(s)
□ 4. Add .data/ to game repo            submodule (Tier 1) or local (Tier 0)
□ 5. Add category folder to template    PR to codegamified/player-data
□ 6. Wire sharing UI (optional)         SetRemote + SyncNow on opt-in
□ 7. Wire discovery UI (optional)       PlayerRegistry + import flow
```

## What Goes Where

| Component | Lives in | Why |
|-----------|----------|-----|
| `IGitRepository`, `EntityStore<T>`, etc. | `.engine/CodeGamified.Persistence/` | Shared framework — never changes per game |
| `PongScript`, `PongScriptSerializer` | `Pong/Scripts/Persistence/` | Game-specific types |
| `PongPersistence : PersistenceBehaviour` | `Pong/Scripts/Persistence/` | Game-specific wiring |
| `.data/` contents | Player's fork of `player-data` | Player-owned git repo |
| `scrape-forks.yml` | `codegamified/player-registry` | Infrastructure — runs as GitHub Action |
| Template folders | `codegamified/player-data` | Each game PRs its category folder |

## `.data/` Runtime Path

The game locates `.data/` differently based on context:

| Context | Path | Notes |
|---------|------|-------|
| Unity Editor | `Application.dataPath + "/.data"` | Inside `Assets/` |
| Desktop build | `Application.persistentDataPath + "/data"` | Copied on first run |
| Submodule mode | Wherever `git submodule` puts it | Player cloned the game repo |

```csharp
string GetDataPath()
{
#if UNITY_EDITOR
    return System.IO.Path.Combine(Application.dataPath, ".data");
#else
    return System.IO.Path.Combine(Application.persistentDataPath, "data");
#endif
}
```

## Resulting `.data/` Layout Per Game

```
# Pong player's .data/
.data/
├── config.json
├── programs/
│   ├── my-paddle-ai.json
│   └── wall-bounce.json
└── scripts/
    ├── beginner-paddle.json
    └── advanced-spin.json

# BitNaughts player's .data/
.data/
├── config.json
├── programs/
│   ├── autopilot.json
│   └── scanner.json
└── satellites/
    ├── weather-sat.json
    └── spy-sat.json

# SeaRauber player's .data/
.data/
├── config.json
├── programs/
│   ├── nav-ai.json
│   └── cannon-control.json
└── ships/
    ├── sloop-alpha.json
    └── frigate-revenge.json
```

Each save is a git commit. Each push shares it with the world.
