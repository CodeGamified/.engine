// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Read-only interface for browsing another player's public repository.
    /// No auth required — reads from raw.githubusercontent.com (public repos).
    ///
    /// Used for the discovery/import flow:
    ///   Player browses registry → picks another player → reads their programs → imports locally.
    /// </summary>
    public interface IRemoteReader
    {
        /// <summary>Read raw JSON from a remote player's repo. Returns null if not found.</summary>
        string Load(string path);

        /// <summary>List files under a directory in a remote repo. May use GitHub API.</summary>
        IReadOnlyList<string> List(string directoryPath);

        /// <summary>Check if a path exists in the remote repo.</summary>
        bool Exists(string path);
    }
}
