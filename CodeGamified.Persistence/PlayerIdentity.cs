// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Player identity and sharing configuration.
    /// Stored locally — never transmitted to any server.
    ///
    /// Tier 0: Only PlayerId set (local saves, no sharing)
    /// Tier 1: GitHub fields set (push/pull enabled)
    /// </summary>
    [Serializable]
    public class PlayerIdentity
    {
        /// <summary>Local player identifier (used in commit author and path conventions).</summary>
        public string PlayerId;

        /// <summary>GitHub username. Null if player hasn't opted into sharing.</summary>
        public string GitHubUsername;

        /// <summary>GitHub repo name (default: "player-data").</summary>
        public string GitHubRepo = "player-data";

        /// <summary>Branch to push/pull (default: "main").</summary>
        public string Branch = "main";

        /// <summary>Whether the player has configured sharing (GitHub username + PAT stored).</summary>
        public bool IsSharingEnabled => !string.IsNullOrEmpty(GitHubUsername);

        /// <summary>Git author string for commits.</summary>
        public string GitAuthor => $"{PlayerId} <{PlayerId}@codegamified>";

        /// <summary>Remote URL for the player's fork.</summary>
        public string RemoteUrl =>
            IsSharingEnabled
                ? $"https://github.com/{GitHubUsername}/{GitHubRepo}.git"
                : null;
    }
}
