// CodeGamified.Engine — Shared code execution framework
// MIT License
using System.Collections.Generic;

namespace CodeGamified.Engine.Runtime
{
    /// <summary>
    /// Interface for loading programs from a database (CSV, ScriptableObject, etc).
    /// </summary>
    public interface IProgramDatabase<TKey, TEntry>
    {
        TEntry GetProgram(TKey key);
        IEnumerable<TEntry> GetAllPrograms();
        bool HasProgram(TKey key);
    }
}
