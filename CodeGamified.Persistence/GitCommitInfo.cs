// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Metadata for a single git commit.
    /// </summary>
    public struct GitCommitInfo
    {
        public string Hash;
        public string Message;
        public string Author;
        public DateTime Timestamp;
    }
}
