// CodeGamified.Persistence — Git-backed persistence framework
// MIT License

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Path conventions for the GitDB repository layout.
    /// All paths are relative to the repository root.
    ///
    /// Convention:
    ///   players/{playerId}/programs/{name}.json
    ///   players/{playerId}/ships/{name}.json
    ///   players/{playerId}/config.json
    ///   shared/programs/{name}.json
    /// </summary>
    public static class GitPath
    {
        public const string PLAYERS_DIR = "players";
        public const string SHARED_DIR = "shared";
        public const string PROGRAMS_DIR = "programs";
        public const string SHIPS_DIR = "ships";
        public const string CONFIG_FILE = "config.json";
        public const string JSON_EXT = ".json";

        // ═══════════════════════════════════════════════════════════════
        // PLAYER PATHS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Root directory for a player: players/{playerId}</summary>
        public static string PlayerDir(string playerId) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}";

        /// <summary>Player's program file: players/{playerId}/programs/{name}.json</summary>
        public static string PlayerProgram(string playerId, string programName) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}/{PROGRAMS_DIR}/{SanitizeName(programName)}{JSON_EXT}";

        /// <summary>Player's programs directory: players/{playerId}/programs</summary>
        public static string PlayerProgramsDir(string playerId) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}/{PROGRAMS_DIR}";

        /// <summary>Player's ship file: players/{playerId}/ships/{name}.json</summary>
        public static string PlayerShip(string playerId, string shipName) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}/{SHIPS_DIR}/{SanitizeName(shipName)}{JSON_EXT}";

        /// <summary>Player's ships directory: players/{playerId}/ships</summary>
        public static string PlayerShipsDir(string playerId) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}/{SHIPS_DIR}";

        /// <summary>Player's config file: players/{playerId}/config.json</summary>
        public static string PlayerConfig(string playerId) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}/{CONFIG_FILE}";

        // ═══════════════════════════════════════════════════════════════
        // SHARED PATHS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Shared program: shared/programs/{name}.json</summary>
        public static string SharedProgram(string programName) =>
            $"{SHARED_DIR}/{PROGRAMS_DIR}/{SanitizeName(programName)}{JSON_EXT}";

        /// <summary>Shared programs directory: shared/programs</summary>
        public static string SharedProgramsDir() =>
            $"{SHARED_DIR}/{PROGRAMS_DIR}";

        // ═══════════════════════════════════════════════════════════════
        // GAME-SPECIFIC (extensible)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Generic entity path: players/{playerId}/{category}/{name}.json</summary>
        public static string PlayerEntity(string playerId, string category, string entityName) =>
            $"{PLAYERS_DIR}/{SanitizeId(playerId)}/{SanitizeName(category)}/{SanitizeName(entityName)}{JSON_EXT}";

        /// <summary>Generic shared entity path: shared/{category}/{name}.json</summary>
        public static string SharedEntity(string category, string entityName) =>
            $"{SHARED_DIR}/{SanitizeName(category)}/{SanitizeName(entityName)}{JSON_EXT}";

        // ═══════════════════════════════════════════════════════════════
        // SANITIZATION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Sanitize an identifier for use in file paths.
        /// Allows alphanumeric, hyphens, and underscores only.
        /// </summary>
        public static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "_empty";
            var sb = new System.Text.StringBuilder(id.Length);
            foreach (char c in id)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.Length == 0 ? "_empty" : sb.ToString();
        }

        /// <summary>
        /// Sanitize a file name component.
        /// Allows alphanumeric, hyphens, underscores, and dots only.
        /// </summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "_empty";
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.')
                    sb.Append(c);
                else
                    sb.Append('_');
            }
            return sb.Length == 0 ? "_empty" : sb.ToString();
        }
    }
}
