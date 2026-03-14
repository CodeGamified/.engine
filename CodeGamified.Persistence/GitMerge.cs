// CodeGamified.Persistence — Git-backed persistence framework
// MIT License
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.Persistence
{
    /// <summary>
    /// Field-level JSON merge for git-backed persistence.
    /// Avoids raw git conflict markers — merges at the JSON key level
    /// with a configurable strategy (last-write-wins or per-field).
    ///
    /// Uses Unity's JsonUtility-compatible dictionaries (flat key-value).
    /// For nested objects, keys are dot-separated: "nav.x", "nav.y".
    /// </summary>
    public static class GitMerge
    {
        /// <summary>
        /// Merge strategy for individual fields.
        /// </summary>
        public enum Strategy
        {
            /// <summary>Incoming value wins if both changed.</summary>
            IncomingWins,

            /// <summary>Local value wins if both changed.</summary>
            LocalWins,

            /// <summary>Higher numeric value wins (e.g. high scores).</summary>
            HigherWins,

            /// <summary>Lower numeric value wins (e.g. best time).</summary>
            LowerWins
        }

        /// <summary>
        /// Three-way merge of flat JSON dictionaries.
        /// Given a common ancestor (base), local changes, and incoming (remote) changes,
        /// produce a merged result.
        /// </summary>
        /// <param name="baseFields">Common ancestor — values before either side changed.</param>
        /// <param name="localFields">Local working copy.</param>
        /// <param name="incomingFields">Remote / incoming changes.</param>
        /// <param name="defaultStrategy">Default strategy when both sides change the same field.</param>
        /// <param name="fieldStrategies">Per-field override strategies (key → strategy).</param>
        /// <returns>Merged dictionary of field values.</returns>
        public static Dictionary<string, string> ThreeWayMerge(
            IReadOnlyDictionary<string, string> baseFields,
            IReadOnlyDictionary<string, string> localFields,
            IReadOnlyDictionary<string, string> incomingFields,
            Strategy defaultStrategy = Strategy.IncomingWins,
            IReadOnlyDictionary<string, Strategy> fieldStrategies = null)
        {
            var result = new Dictionary<string, string>();

            // Collect all keys from all three versions
            var allKeys = new HashSet<string>();
            foreach (var key in baseFields.Keys) allKeys.Add(key);
            foreach (var key in localFields.Keys) allKeys.Add(key);
            foreach (var key in incomingFields.Keys) allKeys.Add(key);

            foreach (string key in allKeys)
            {
                baseFields.TryGetValue(key, out string baseVal);
                localFields.TryGetValue(key, out string localVal);
                incomingFields.TryGetValue(key, out string incomingVal);

                bool localChanged = localVal != baseVal;
                bool incomingChanged = incomingVal != baseVal;

                if (!localChanged && !incomingChanged)
                {
                    // Neither changed — keep base (or skip if deleted)
                    if (baseVal != null) result[key] = baseVal;
                }
                else if (localChanged && !incomingChanged)
                {
                    // Only local changed
                    if (localVal != null) result[key] = localVal;
                    // else: local deleted it
                }
                else if (!localChanged && incomingChanged)
                {
                    // Only incoming changed
                    if (incomingVal != null) result[key] = incomingVal;
                    // else: incoming deleted it
                }
                else
                {
                    // Both changed — apply strategy
                    var strategy = defaultStrategy;
                    if (fieldStrategies != null && fieldStrategies.TryGetValue(key, out var fs))
                        strategy = fs;

                    string winner = ResolveConflict(localVal, incomingVal, strategy);
                    if (winner != null) result[key] = winner;
                }
            }

            return result;
        }

        /// <summary>
        /// Simple two-way merge (no common ancestor). Incoming overwrites local
        /// for conflicting keys unless a field strategy says otherwise.
        /// </summary>
        public static Dictionary<string, string> TwoWayMerge(
            IReadOnlyDictionary<string, string> localFields,
            IReadOnlyDictionary<string, string> incomingFields,
            Strategy defaultStrategy = Strategy.IncomingWins,
            IReadOnlyDictionary<string, Strategy> fieldStrategies = null)
        {
            var result = new Dictionary<string, string>(localFields.Count + incomingFields.Count);

            // Start with all local fields
            foreach (var kvp in localFields)
                result[kvp.Key] = kvp.Value;

            // Apply incoming — resolve conflicts
            foreach (var kvp in incomingFields)
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result[kvp.Key] = kvp.Value;
                    continue;
                }

                string localVal = result[kvp.Key];
                if (localVal == kvp.Value) continue; // No conflict

                var strategy = defaultStrategy;
                if (fieldStrategies != null && fieldStrategies.TryGetValue(kvp.Key, out var fs))
                    strategy = fs;

                string winner = ResolveConflict(localVal, kvp.Value, strategy);
                if (winner != null)
                    result[kvp.Key] = winner;
                else
                    result.Remove(kvp.Key);
            }

            return result;
        }

        static string ResolveConflict(string localVal, string incomingVal, Strategy strategy)
        {
            switch (strategy)
            {
                case Strategy.IncomingWins:
                    return incomingVal;

                case Strategy.LocalWins:
                    return localVal;

                case Strategy.HigherWins:
                    if (float.TryParse(localVal, out float lh) &&
                        float.TryParse(incomingVal, out float ih))
                        return lh >= ih ? localVal : incomingVal;
                    return incomingVal; // fallback

                case Strategy.LowerWins:
                    if (float.TryParse(localVal, out float ll) &&
                        float.TryParse(incomingVal, out float il))
                        return ll <= il ? localVal : incomingVal;
                    return incomingVal; // fallback

                default:
                    return incomingVal;
            }
        }
    }
}
