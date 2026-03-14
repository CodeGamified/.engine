// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.Persistence.Providers
{
    /// <summary>
    /// Read-only provider that fetches files from a public GitHub repository
    /// via raw.githubusercontent.com. No auth required.
    ///
    /// Used for browsing/importing other players' published code.
    ///
    /// Usage:
    ///   var reader = new PublicRepoReader("alice", "player-data");
    ///   string json = reader.Load("programs/autopilot.json");
    /// </summary>
    public class PublicRepoReader : IRemoteReader
    {
        readonly string _owner;
        readonly string _repo;
        readonly string _branch;

        /// <param name="owner">GitHub username (e.g. "alice").</param>
        /// <param name="repo">Repository name (e.g. "player-data").</param>
        /// <param name="branch">Branch to read from (default: "main").</param>
        public PublicRepoReader(string owner, string repo, string branch = "main")
        {
            _owner = owner;
            _repo = repo;
            _branch = branch;
        }

        /// <summary>
        /// Base URL for raw file access.
        /// Example: https://raw.githubusercontent.com/alice/player-data/main/
        /// </summary>
        public string BaseUrl => $"https://raw.githubusercontent.com/{_owner}/{_repo}/{_branch}/";

        public string Load(string path)
        {
            string url = BaseUrl + path;
            try
            {
                // UnityWebRequest should be used from MonoBehaviour coroutines
                // in production. This synchronous fallback is for non-Unity contexts.
#if UNITY_2018_1_OR_NEWER
                // Use UnityWebRequest via a helper — see README for coroutine pattern
                return UnityWebRequestSync(url);
#else
                using (var client = new System.Net.WebClient())
                {
                    return client.DownloadString(url);
                }
#endif
            }
            catch
            {
                return null; // 404 or network error
            }
        }

        public IReadOnlyList<string> List(string directoryPath)
        {
            // GitHub API required for directory listing (raw.githubusercontent doesn't support it)
            // GET https://api.github.com/repos/{owner}/{repo}/contents/{path}?ref={branch}
            string url = $"https://api.github.com/repos/{_owner}/{_repo}/contents/{directoryPath}?ref={_branch}";
            try
            {
                string json;
#if UNITY_2018_1_OR_NEWER
                json = UnityWebRequestSync(url);
#else
                using (var client = new System.Net.WebClient())
                {
                    client.Headers.Add("User-Agent", "CodeGamified");
                    json = client.DownloadString(url);
                }
#endif
                if (json == null) return Array.Empty<string>();
                return ParseGitHubContentsResponse(json, directoryPath);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public bool Exists(string path)
        {
            return Load(path) != null;
        }

#if UNITY_2018_1_OR_NEWER
        /// <summary>
        /// Synchronous web request fallback for Unity.
        /// Production code should use coroutines with UnityWebRequest instead.
        /// </summary>
        static string UnityWebRequestSync(string url)
        {
            var request = UnityEngine.Networking.UnityWebRequest.Get(url);
            var op = request.SendWebRequest();
            while (!op.isDone) { } // blocking — use coroutines in production
#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success)
                return null;
#else
            if (request.isNetworkError || request.isHttpError)
                return null;
#endif
            return request.downloadHandler.text;
        }
#endif

        /// <summary>
        /// Minimal parse of GitHub Contents API response to extract file paths.
        /// Avoids dependency on a JSON library — just extracts "path" fields.
        /// </summary>
        static List<string> ParseGitHubContentsResponse(string json, string directoryPath)
        {
            // GitHub returns: [{"name":"file.json","path":"programs/file.json","type":"file",...}, ...]
            var results = new List<string>();
            int idx = 0;
            while (true)
            {
                idx = json.IndexOf("\"path\":", idx);
                if (idx < 0) break;

                idx = json.IndexOf("\"", idx + 7) + 1;
                int end = json.IndexOf("\"", idx);
                if (end < 0) break;

                string path = json.Substring(idx, end - idx);
                if (path.EndsWith(".json"))
                    results.Add(path);

                idx = end + 1;
            }
            return results;
        }
    }
}
