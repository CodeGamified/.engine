// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeGamified.Persistence.Providers
{
    /// <summary>
    /// Filesystem-backed implementation of IGitRepository.
    /// Reads/writes JSON files on disk. Commit/push/pull operations
    /// delegate to git CLI via System.Diagnostics.Process.
    ///
    /// Suitable for desktop builds (Windows/Mac/Linux).
    /// For WebGL, use GitHubApiProvider instead.
    /// </summary>
    public class LocalGitProvider : IGitRepository
    {
        readonly string _repoPath;
        readonly PlayerIdentity _identity;

        /// <param name="repoPath">Absolute path to the local git repository root.</param>
        /// <param name="identity">Player identity for commit authorship. Null = default git config.</param>
        public LocalGitProvider(string repoPath, PlayerIdentity identity = null)
        {
            _repoPath = repoPath;
            _identity = identity;
        }

        // ═══════════════════════════════════════════════════════════════
        // BOOTSTRAP
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Ensure the repo exists. Creates directory + git init if needed.
        /// Call once at game startup. Safe to call repeatedly.
        /// </summary>
        public GitResult EnsureInitialized()
        {
            if (!Directory.Exists(_repoPath))
                Directory.CreateDirectory(_repoPath);

            // Already a git repo?
            if (Directory.Exists(Path.Combine(_repoPath, ".git")))
                return GitResult.Ok();

            var initResult = RunGit("init");
            if (!initResult.Success) return initResult;

            // Set identity for this repo (not global)
            if (_identity != null)
            {
                RunGit($"config user.name \"{EscapeMessage(_identity.PlayerId)}\"");
                RunGit($"config user.email \"{EscapeMessage(_identity.PlayerId)}@codegamified\"");
            }

            // Initial empty commit so the repo has a HEAD
            return RunGit("commit --allow-empty -m \"init: CodeGamified save repo\"");
        }

        /// <summary>
        /// Configure the remote origin for push/pull to player's GitHub fork.
        /// Call after the player opts into sharing and provides credentials.
        /// </summary>
        public GitResult SetRemote(string remoteUrl)
        {
            // Remove existing origin if present, then add
            RunGit("remote remove origin");
            return RunGit($"remote add origin \"{remoteUrl}\"");
        }

        /// <summary>True if a remote origin is configured.</summary>
        public bool HasRemote()
        {
            string output = RunGitOutput("remote get-url origin");
            return output != null;
        }

        public GitResult Save(string path, string json, string commitMessage)
        {
            string fullPath = Path.Combine(_repoPath, path.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(fullPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(fullPath, json);

            var addResult = RunGit($"add \"{path}\"");
            if (!addResult.Success) return addResult;

            return RunGitCommit(commitMessage);
        }

        public string Load(string path)
        {
            string fullPath = Path.Combine(_repoPath, path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath) ? File.ReadAllText(fullPath) : null;
        }

        public IReadOnlyList<string> List(string directoryPath)
        {
            string fullDir = Path.Combine(_repoPath, directoryPath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(fullDir))
                return Array.Empty<string>();

            var files = Directory.GetFiles(fullDir, "*.json", SearchOption.TopDirectoryOnly);
            var results = new List<string>(files.Length);
            foreach (var f in files)
            {
                // Return repo-relative paths with forward slashes
                string relative = f.Substring(_repoPath.Length).TrimStart(Path.DirectorySeparatorChar);
                results.Add(relative.Replace(Path.DirectorySeparatorChar, '/'));
            }
            return results;
        }

        public GitResult Delete(string path, string commitMessage)
        {
            string fullPath = Path.Combine(_repoPath, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                return GitResult.Fail($"Path not found: {path}");

            var rmResult = RunGit($"rm \"{path}\"");
            if (!rmResult.Success) return rmResult;

            return RunGitCommit(commitMessage);
        }

        public bool Exists(string path)
        {
            string fullPath = Path.Combine(_repoPath, path.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(fullPath);
        }

        public GitResult Push() => RunGit("push");

        public GitResult Pull() => RunGit("pull --rebase");

        public IReadOnlyList<GitCommitInfo> GetHistory(string path, int maxCount = 10)
        {
            var result = RunGitOutput($"log --format=%H|%s|%an|%aI -n {maxCount} -- \"{path}\"");
            if (string.IsNullOrEmpty(result))
                return Array.Empty<GitCommitInfo>();

            var lines = result.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var commits = new List<GitCommitInfo>(lines.Length);
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '|' }, 4);
                if (parts.Length < 4) continue;
                commits.Add(new GitCommitInfo
                {
                    Hash = parts[0],
                    Message = parts[1],
                    Author = parts[2],
                    Timestamp = DateTime.TryParse(parts[3], out var dt) ? dt : DateTime.MinValue
                });
            }
            return commits;
        }

        public string LoadAtCommit(string path, string commitHash)
        {
            return RunGitOutput($"show {commitHash}:\"{path}\"");
        }

        // ═══════════════════════════════════════════════════════════════
        // GIT CLI HELPERS
        // ═══════════════════════════════════════════════════════════════

        GitResult RunGit(string arguments)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = _repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    string error = proc.StandardError.ReadToEnd();
                    proc.WaitForExit();

                    if (proc.ExitCode != 0)
                        return GitResult.Fail(error.Length > 0 ? error.Trim() : $"git exited with code {proc.ExitCode}");

                    // Extract commit hash from output if present
                    string hash = null;
                    if (output.Contains("[") && output.Contains("]"))
                    {
                        int start = output.IndexOf(' ') + 1;
                        int end = output.IndexOf(']');
                        if (start > 0 && end > start)
                            hash = output.Substring(start, end - start);
                    }

                    return GitResult.Ok(hash);
                }
            }
            catch (Exception ex)
            {
                return GitResult.Fail(ex.Message);
            }
        }

        string RunGitOutput(string arguments)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = _repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    return proc.ExitCode == 0 ? output.TrimEnd() : null;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Commit with player identity if configured.</summary>
        GitResult RunGitCommit(string commitMessage)
        {
            string authorArg = _identity != null
                ? $" --author=\"{EscapeMessage(_identity.GitAuthor)}\""
                : "";
            return RunGit($"commit{authorArg} -m \"{EscapeMessage(commitMessage)}\"");
        }

        static string EscapeMessage(string msg) =>
            msg.Replace("\"", "\\\"").Replace("\n", " ");
    }
}
