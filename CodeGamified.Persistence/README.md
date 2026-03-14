# CodeGamified.Persistence

Git-backed persistence framework for educational programming games.  
CRUD operations map to git operations — zero hosting costs, open-sourced data, version history for free.

## Architecture

```
Game Entity → [IEntitySerializer] → JSON → [IGitRepository] → git add/commit
                                                  ↑ LocalGitProvider (desktop: git CLI)
                                                  ↑ MemoryGitProvider (tests: in-memory)

PlayerIdentity — commit authorship + optional GitHub sharing config
EntityStore<T> — typed wrapper combining IGitRepository + IEntitySerializer
PersistenceBehaviour — optional MonoBehaviour for autosave + sync
GitPath — path conventions: players/{id}/programs/{name}.json
GitMerge — field-level JSON three-way merge (no raw conflict markers)
IRemoteReader / PublicRepoReader — read other players' public repos (no auth)
```

## CRUD → Git Mapping

| CRUD | Git Operation | Details |
|------|---------------|---------|
| Create | `add` + `commit` | JSON blob written to path convention |
| Read | Read worktree file | Deserialize from JSON |
| Update | Overwrite + `commit` | Full history retained |
| Delete | `rm` + `commit` | Recoverable via history |
| Share | `push` / `pull` | Player repos are real git repos |
| Merge | Three-way merge | Field-level JSON merge, configurable per-field strategy |

## Files

| File | Purpose |
|------|---------|
| `IGitRepository.cs` | Core CRUD + sync interface (Save, Load, List, Delete, Push, Pull, History) |
| `IEntitySerializer.cs` | Serialize/deserialize contract with schema versioning |
| `GitCommitInfo.cs` | Commit metadata struct (hash, message, author, timestamp) |
| `GitPath.cs` | Path conventions + sanitization (`players/{id}/programs/{name}.json`) |
| `GitMerge.cs` | Three-way + two-way field-level JSON merge with per-field strategies |
| `EntityStore.cs` | Typed CRUD wrapper combining `IGitRepository` + `IEntitySerializer<T>` |
| `PersistenceBehaviour.cs` | Abstract MonoBehaviour — autosave interval, dirty tracking, sync |
| `PlayerIdentity.cs` | Player ID + optional GitHub sharing config (never transmitted) |
| `IRemoteReader.cs` | Read-only interface for browsing other players' public repos |
| `Providers/LocalGitProvider.cs` | Desktop provider — filesystem + git CLI + auto-init + remote setup |
| `Providers/PublicRepoReader.cs` | Read-only provider — raw.githubusercontent.com (no auth) |
| `Providers/MemoryGitProvider.cs` | Test provider — in-memory dictionary |
| `PlayerRegistry.cs` | Client for the player registry index — search, browse, get reader |
| `Registry/scrape-forks.yml` | GitHub Action: nightly scrape of all forks → `registry.json` |
| `Tests/PersistenceTests.cs` | NUnit edit-mode tests |

## Repository Layout

```
repo-root/
├── players/
│   ├── alice/
│   │   ├── config.json
│   │   ├── programs/
│   │   │   ├── autopilot.json
│   │   │   └── scanner.json
│   │   └── ships/
│   │       └── frigate.json
│   └── bob/
│       └── programs/
│           └── nav-ai.json
└── shared/
    └── programs/
        └── hello-world.json
```

## User Story

### Tier 0 — Play (no account)

Player installs, plays, saves. Under the hood the game auto-creates a local git repo
at `persistentDataPath/save-repo/`. Every save is a local commit. No GitHub, no token,
no network required. The player doesn't know git exists — it's just a save system with
infinite undo.

### Tier 1 — Share (opt-in GitHub)

Player wants to share code. The game walks them through:
1. Create a GitHub account
2. Fork `codegamified/player-data` (a template repo)
3. Generate a fine-grained PAT (repo scope only)
4. Paste username + PAT into the game

The PAT is stored locally (OS keychain / PlayerPrefs) — never transmitted to any
CodeGamified server. The game pushes to the player's own fork on sync.

### Tier 2 — Discover (browse + import)

Player browses `codegamified/player-registry` (a JSON index of forks). Reading
other players' public repos requires zero auth via `raw.githubusercontent.com`.
Imported programs are merged locally using `GitMerge`.

### Who Commits Where?

| Scenario | Committer | Repo | Auth |
|---|---|---|---|
| Local save | `{playerId} <{playerId}@codegamified>` | Local filesystem | None |
| Push to GitHub | Player's own identity | `{player}/player-data` | Player's own PAT |
| Read others' code | N/A (read-only) | `raw.githubusercontent.com` | None (public) |

**No proxy account. No bot. No shared credentials.** Players commit to their own repos.

## Integration Pattern

### 1. Bootstrap the save repo

```csharp
// At game startup — creates local git repo if it doesn't exist
var identity = new PlayerIdentity { PlayerId = "alice" };
var repo = new LocalGitProvider(Application.persistentDataPath + "/save-repo", identity);
repo.EnsureInitialized();
```

### 2. Implement `IEntitySerializer<T>`

```csharp
public class ProgramSerializer : IEntitySerializer<PlayerProgram>
{
    public int SchemaVersion => 1;

    public string Serialize(PlayerProgram p) =>
        JsonUtility.ToJson(p, prettyPrint: true);

    public PlayerProgram Deserialize(string json) =>
        JsonUtility.FromJson<PlayerProgram>(json);
}
```

### 3. Create an `EntityStore<T>`

```csharp
var repo = new LocalGitProvider(savePath, identity);
repo.EnsureInitialized();

var programs = new EntityStore<PlayerProgram>(repo, new ProgramSerializer(), "programs");

// Save
programs.Save("alice", "autopilot", myProgram, "fixed heading bug");

// Load
PlayerProgram p = programs.Load("alice", "autopilot");

// List all of alice's programs
var names = programs.ListNames("alice");

// History
var history = programs.GetHistory("alice", "autopilot");

// Share to the shared directory
programs.SaveShared("hello-world", tutorialProgram, "community starter");
```

### 4. Opt into sharing (Tier 1)

```csharp
// Player provides GitHub username + PAT
identity.GitHubUsername = "alice";
repo.SetRemote(identity.RemoteUrl);
// PAT credential: git config credential.helper store, or OS keychain
repo.SyncNow(); // push local commits to their fork
```

### 5. Subclass `PersistenceBehaviour` (optional autosave)

```csharp
public class GamePersistence : PersistenceBehaviour
{
    EntityStore<PlayerProgram> _programs;
    List<PlayerProgram> _dirtyPrograms = new List<PlayerProgram>();

    void Start()
    {
        var identity = new PlayerIdentity { PlayerId = playerId };
        var repo = new LocalGitProvider(savePath, identity);
        repo.EnsureInitialized();
        Initialize(repo);
        _programs = new EntityStore<PlayerProgram>(repo, new ProgramSerializer(), "programs");
        autosaveInterval = 30f;
        syncInterval = 120f;
    }

    protected override GitResult PerformSave(IGitRepository repo)
    {
        GitResult last = GitResult.Ok();
        foreach (var p in _dirtyPrograms)
            last = _programs.Save(playerId, p.Name, p, $"autosave {p.Name}");
        _dirtyPrograms.Clear();
        return last;
    }
}
```

### 6. Browse + import via PlayerRegistry (Tier 2)

```csharp
// Fetch the auto-populated registry (built nightly by GitHub Action)
var registry = new PlayerRegistry();
registry.Refresh(); // reads codegamified/player-registry/registry.json

// Search for players
var results = registry.Search("bob");

// Get a reader for bob's public repo — no auth needed
var bobReader = registry.GetReader("bob");
string json = bobReader.Load("programs/autopilot.json");
if (json != null)
{
    var program = serializer.Deserialize(json);
    programs.Save(playerId, "imported-autopilot", program, "imported from bob");
}

// Browse what bob has published
var bobEntry = registry.GetPlayer("bob");
// bobEntry.programs = ["nav-ai"]
// bobEntry.ships = []
```

### 7. Use `GitMerge` for conflict resolution

```csharp
// Three-way merge: base (last sync) vs local vs incoming
var merged = GitMerge.ThreeWayMerge(
    baseFields, localFields, incomingFields,
    defaultStrategy: GitMerge.Strategy.IncomingWins,
    fieldStrategies: new Dictionary<string, GitMerge.Strategy>
    {
        { "highScore", GitMerge.Strategy.HigherWins },
        { "bestTime",  GitMerge.Strategy.LowerWins },
        { "shipName",  GitMerge.Strategy.LocalWins }
    });
```

## Merge Strategies

| Strategy | Behavior | Use Case |
|----------|----------|----------|
| `IncomingWins` | Remote value takes precedence | Default — latest shared version wins |
| `LocalWins` | Local value takes precedence | Player preferences, custom names |
| `HigherWins` | Larger numeric value wins | High scores, XP |
| `LowerWins` | Smaller numeric value wins | Best time, fewest moves |

## Why GitDB?

- **Zero cost**: No servers, databases, or auth services to host
- **Version history**: Every save is a commit — full undo across sessions
- **Open data**: Player repos are public — fork, star, PR each other's code
- **Offline-first**: Local clone works without network; sync when available
- **Educational**: Players learn git concepts (commit, branch, merge) by playing
- **Collaboration**: Sharing code is a `git push` — no custom sharing infrastructure

## Template Repo

Create `codegamified/player-data` as a GitHub template repo:

```
player-data/
├── README.md          "This is {player}'s CodeGamified save data"
├── .gitignore
├── programs/
├── ships/
└── config.json
```

Players fork it. The fork IS their save file. Their commit history IS their play history.

## Player Registry

The `codegamified/player-registry` repo hosts a `registry.json` index
built automatically by a GitHub Action (`Registry/scrape-forks.yml`).

The Action runs nightly and:
1. Calls `GET /repos/codegamified/player-data/forks` to find all forks
2. For each fork, reads `programs/` and `ships/` via Contents API
3. Builds `registry.json` with all players, their programs, and ships
4. Commits + pushes if changed

**Cost: $0** — uses GitHub's free Actions minutes + `GITHUB_TOKEN` (automatic).

```json
{
  "generated": "2026-03-14T04:00:00Z",
  "players": {
    "alice": {
      "repo": "alice/player-data",
      "joined": "2026-03-14",
      "programs": ["autopilot", "scanner"],
      "ships": ["frigate"]
    },
    "bob": {
      "repo": "bob/player-data",
      "joined": "2026-03-10",
      "programs": ["nav-ai"]
    }
  }
}
```

The game reads this index via `PlayerRegistry.Refresh()` — a single GET to
`raw.githubusercontent.com` (no auth, no API key).
