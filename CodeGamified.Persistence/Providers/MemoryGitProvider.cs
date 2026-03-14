// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeGamified.Persistence.Providers
{
    /// <summary>
    /// In-memory mock implementation of IGitRepository for testing.
    /// No actual git operations — stores files in a dictionary.
    /// </summary>
    public class MemoryGitProvider : IGitRepository
    {
        readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        readonly List<GitCommitInfo> _commits = new List<GitCommitInfo>();
        readonly Dictionary<string, List<string>> _history = new Dictionary<string, List<string>>();
        int _commitCounter;

        public GitResult Save(string path, string json, string commitMessage)
        {
            _files[path] = json;
            string hash = $"mock-{++_commitCounter:D6}";
            var info = new GitCommitInfo
            {
                Hash = hash,
                Message = commitMessage,
                Author = "test",
                Timestamp = DateTime.UtcNow
            };
            _commits.Add(info);

            if (!_history.ContainsKey(path))
                _history[path] = new List<string>();
            _history[path].Add(hash);

            return GitResult.Ok(hash);
        }

        public string Load(string path)
        {
            return _files.TryGetValue(path, out string json) ? json : null;
        }

        public IReadOnlyList<string> List(string directoryPath)
        {
            var results = new List<string>();
            string prefix = directoryPath.EndsWith("/") ? directoryPath : directoryPath + "/";
            foreach (var key in _files.Keys)
            {
                if (key.StartsWith(prefix))
                    results.Add(key);
            }
            return results;
        }

        public GitResult Delete(string path, string commitMessage)
        {
            if (!_files.ContainsKey(path))
                return GitResult.Fail($"Path not found: {path}");

            _files.Remove(path);
            string hash = $"mock-{++_commitCounter:D6}";
            _commits.Add(new GitCommitInfo
            {
                Hash = hash,
                Message = commitMessage,
                Author = "test",
                Timestamp = DateTime.UtcNow
            });
            return GitResult.Ok(hash);
        }

        public bool Exists(string path) => _files.ContainsKey(path);

        public GitResult Push() => GitResult.Ok();

        public GitResult Pull() => GitResult.Ok();

        public IReadOnlyList<GitCommitInfo> GetHistory(string path, int maxCount = 10)
        {
            if (!_history.TryGetValue(path, out var hashes))
                return Array.Empty<GitCommitInfo>();

            var results = new List<GitCommitInfo>();
            int start = Math.Max(0, hashes.Count - maxCount);
            for (int i = hashes.Count - 1; i >= start; i--)
            {
                string hash = hashes[i];
                var commit = _commits.Find(c => c.Hash == hash);
                results.Add(commit);
            }
            return results;
        }

        public string LoadAtCommit(string path, string commitHash)
        {
            // Simplified: memory provider doesn't track snapshots per commit
            return Load(path);
        }

        // ═══════════════════════════════════════════════════════════════
        // TEST HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Total number of files in the mock repository.</summary>
        public int FileCount => _files.Count;

        /// <summary>Total number of commits recorded.</summary>
        public int CommitCount => _commits.Count;

        /// <summary>Clear all files and history.</summary>
        public void Clear()
        {
            _files.Clear();
            _commits.Clear();
            _history.Clear();
            _commitCounter = 0;
        }
    }
}
