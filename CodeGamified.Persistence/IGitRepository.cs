// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Result of a git-backed persistence operation.
    /// </summary>
    public struct GitResult
    {
        public bool Success;
        public string CommitHash;
        public string Error;

        public static GitResult Ok(string hash = null) =>
            new GitResult { Success = true, CommitHash = hash };

        public static GitResult Fail(string error) =>
            new GitResult { Success = false, Error = error };
    }

    /// <summary>
    /// A single entry read from the repository.
    /// </summary>
    public struct GitEntry<T>
    {
        public string Path;
        public T Data;
        public string CommitHash;
        public DateTime Timestamp;
    }

    /// <summary>
    /// Core interface for git-backed CRUD operations.
    /// Games provide a concrete implementation (local git, GitHub API, mock).
    ///
    /// CRUD mapping:
    ///   Create → Save (add + commit)
    ///   Read   → Load (read worktree / show blob)
    ///   Update → Save (overwrite + commit)
    ///   Delete → Delete (rm + commit)
    ///   Share  → Push / Pull
    ///   Merge  → Import
    /// </summary>
    public interface IGitRepository
    {
        // ═══════════════════════════════════════════════════════════════
        // CRUD
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Serialize and commit an entity at the given path.</summary>
        GitResult Save(string path, string json, string commitMessage);

        /// <summary>Read raw JSON from the given path. Returns null if not found.</summary>
        string Load(string path);

        /// <summary>List all paths under a directory prefix.</summary>
        IReadOnlyList<string> List(string directoryPath);

        /// <summary>Delete a path and commit the removal.</summary>
        GitResult Delete(string path, string commitMessage);

        /// <summary>Check if a path exists in the repository.</summary>
        bool Exists(string path);

        // ═══════════════════════════════════════════════════════════════
        // SYNC
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Push local commits to the remote.</summary>
        GitResult Push();

        /// <summary>Pull remote changes into the local repository.</summary>
        GitResult Pull();

        // ═══════════════════════════════════════════════════════════════
        // HISTORY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get commit history for a specific path.</summary>
        IReadOnlyList<GitCommitInfo> GetHistory(string path, int maxCount = 10);

        /// <summary>Read the contents of a file at a specific commit.</summary>
        string LoadAtCommit(string path, string commitHash);
    }
}
