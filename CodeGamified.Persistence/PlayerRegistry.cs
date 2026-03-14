// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// A single player entry in the registry index.
    /// Populated by the GitHub Action that scrapes all forks.
    /// </summary>
    [Serializable]
    public class PlayerRegistryEntry
    {
        public string username;
        public string repo;
        public string joined;
        public string[] programs;
        public string[] ships;
    }

    /// <summary>
    /// Client for reading the player registry — a JSON index of all known players
    /// hosted at codegamified/player-registry (populated by GitHub Action).
    ///
    /// The registry is a single JSON file: registry.json
    /// Read via raw.githubusercontent.com — no auth required.
    ///
    /// Usage:
    ///   var registry = new PlayerRegistry();
    ///   registry.Refresh();
    ///   var players = registry.Search("ali");
    ///   var reader = registry.GetReader("alice");
    ///   string json = reader.Load("programs/autopilot.json");
    /// </summary>
    public class PlayerRegistry
    {
        /// <summary>GitHub org/user that owns the registry repo.</summary>
        public const string REGISTRY_OWNER = "codegamified";

        /// <summary>Registry repo name.</summary>
        public const string REGISTRY_REPO = "player-registry";

        /// <summary>Path to the index file within the registry repo.</summary>
        public const string INDEX_PATH = "registry.json";

        readonly Providers.PublicRepoReader _registryReader;
        Dictionary<string, PlayerRegistryEntry> _entries = new Dictionary<string, PlayerRegistryEntry>();
        DateTime _lastRefresh;

        public PlayerRegistry()
        {
            _registryReader = new Providers.PublicRepoReader(REGISTRY_OWNER, REGISTRY_REPO);
        }

        /// <summary>Number of known players.</summary>
        public int PlayerCount => _entries.Count;

        /// <summary>When the registry was last fetched.</summary>
        public DateTime LastRefresh => _lastRefresh;

        // ═══════════════════════════════════════════════════════════════
        // FETCH
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Fetch the latest registry index from GitHub.
        /// Safe to call periodically — it's a single small JSON file.
        /// Returns true if the fetch succeeded.
        /// </summary>
        public bool Refresh()
        {
            string json = _registryReader.Load(INDEX_PATH);
            if (json == null) return false;

            _entries.Clear();
            ParseRegistry(json);
            _lastRefresh = DateTime.UtcNow;
            return true;
        }

        // ═══════════════════════════════════════════════════════════════
        // QUERY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get all known player entries.</summary>
        public IReadOnlyCollection<PlayerRegistryEntry> GetAllPlayers() => _entries.Values;

        /// <summary>Get a specific player's entry. Returns null if not found.</summary>
        public PlayerRegistryEntry GetPlayer(string username)
        {
            return _entries.TryGetValue(username, out var entry) ? entry : null;
        }

        /// <summary>Search players by username prefix (case-insensitive).</summary>
        public List<PlayerRegistryEntry> Search(string query)
        {
            var results = new List<PlayerRegistryEntry>();
            if (string.IsNullOrEmpty(query)) return results;

            string lower = query.ToLowerInvariant();
            foreach (var kvp in _entries)
            {
                if (kvp.Key.ToLowerInvariant().Contains(lower))
                    results.Add(kvp.Value);
            }
            return results;
        }

        /// <summary>
        /// Get a PublicRepoReader for a specific player's data repo.
        /// Use this to browse/import their programs, ships, etc.
        /// </summary>
        public Providers.PublicRepoReader GetReader(string username)
        {
            var entry = GetPlayer(username);
            if (entry == null) return null;

            // Parse owner from repo field ("alice/player-data" → owner="alice", repo="player-data")
            string[] parts = entry.repo.Split('/');
            if (parts.Length != 2) return null;

            return new Providers.PublicRepoReader(parts[0], parts[1]);
        }

        // ═══════════════════════════════════════════════════════════════
        // PARSE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Minimal JSON parse of the registry index.
        /// Expected format:
        /// {
        ///   "players": {
        ///     "alice": { "repo": "alice/player-data", "joined": "2026-03-14", "programs": ["autopilot","scanner"], "ships": ["frigate"] },
        ///     "bob":   { "repo": "bob/player-data",   "joined": "2026-03-10", "programs": ["nav-ai"] }
        ///   }
        /// }
        /// </summary>
        void ParseRegistry(string json)
        {
            // Find "players" object
            int playersIdx = json.IndexOf("\"players\"");
            if (playersIdx < 0) return;

            int braceStart = json.IndexOf('{', playersIdx);
            if (braceStart < 0) return;

            // Parse each player entry
            int pos = braceStart + 1;
            while (pos < json.Length)
            {
                // Find next username key
                int keyStart = json.IndexOf('"', pos);
                if (keyStart < 0) break;
                int keyEnd = json.IndexOf('"', keyStart + 1);
                if (keyEnd < 0) break;

                string username = json.Substring(keyStart + 1, keyEnd - keyStart - 1);

                // Find the value object
                int objStart = json.IndexOf('{', keyEnd);
                if (objStart < 0) break;
                int objEnd = FindMatchingBrace(json, objStart);
                if (objEnd < 0) break;

                string objJson = json.Substring(objStart, objEnd - objStart + 1);
                var entry = ParsePlayerEntry(username, objJson);
                if (entry != null)
                    _entries[username] = entry;

                pos = objEnd + 1;

                // Check if we've hit the closing brace of "players"
                int nextChar = FindNextNonWhitespace(json, pos);
                if (nextChar >= 0 && json[nextChar] == '}')
                    break;
            }
        }

        static PlayerRegistryEntry ParsePlayerEntry(string username, string json)
        {
            var entry = new PlayerRegistryEntry { username = username };
            entry.repo = ExtractStringValue(json, "repo") ?? $"{username}/player-data";
            entry.joined = ExtractStringValue(json, "joined") ?? "";
            entry.programs = ExtractStringArray(json, "programs");
            entry.ships = ExtractStringArray(json, "ships");
            return entry;
        }

        static string ExtractStringValue(string json, string key)
        {
            string search = $"\"{key}\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return null;

            int colonIdx = json.IndexOf(':', idx + search.Length);
            if (colonIdx < 0) return null;

            int valStart = json.IndexOf('"', colonIdx + 1);
            if (valStart < 0) return null;
            int valEnd = json.IndexOf('"', valStart + 1);
            if (valEnd < 0) return null;

            return json.Substring(valStart + 1, valEnd - valStart - 1);
        }

        static string[] ExtractStringArray(string json, string key)
        {
            string search = $"\"{key}\"";
            int idx = json.IndexOf(search);
            if (idx < 0) return Array.Empty<string>();

            int bracketStart = json.IndexOf('[', idx);
            if (bracketStart < 0) return Array.Empty<string>();
            int bracketEnd = json.IndexOf(']', bracketStart);
            if (bracketEnd < 0) return Array.Empty<string>();

            string inner = json.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);
            var items = new List<string>();
            int pos = 0;
            while (pos < inner.Length)
            {
                int qs = inner.IndexOf('"', pos);
                if (qs < 0) break;
                int qe = inner.IndexOf('"', qs + 1);
                if (qe < 0) break;
                items.Add(inner.Substring(qs + 1, qe - qs - 1));
                pos = qe + 1;
            }
            return items.ToArray();
        }

        static int FindMatchingBrace(string json, int openPos)
        {
            int depth = 0;
            for (int i = openPos; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        static int FindNextNonWhitespace(string json, int pos)
        {
            for (int i = pos; i < json.Length; i++)
                if (!char.IsWhiteSpace(json[i]) && json[i] != ',')
                    return i;
            return -1;
        }
    }
}
