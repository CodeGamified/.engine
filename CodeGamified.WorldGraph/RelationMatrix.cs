// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Pairwise relation tracker between owners (players, factions, etc).
    /// Symmetric: relation(A,B) == relation(B,A).
    ///
    /// Generic over the relation enum:
    ///   RelationMatrix&lt;DiplomacyState&gt;   → Neutral/Allied/Hostile
    ///   RelationMatrix&lt;FactionStanding&gt;  → Friendly/Indifferent/KillOnSight
    /// </summary>
    public class RelationMatrix<TState> where TState : struct, Enum
    {
        private readonly Dictionary<long, TState> _relations = new();
        private readonly TState _defaultState;
        private readonly TState _selfState;

        /// <param name="defaultState">Relation between unrelated owners.</param>
        /// <param name="selfState">Relation of an owner with itself.</param>
        public RelationMatrix(TState defaultState, TState selfState)
        {
            _defaultState = defaultState;
            _selfState = selfState;
        }

        /// <summary>Get relation between two owners.</summary>
        public TState Get(int a, int b)
        {
            if (a == b) return _selfState;
            return _relations.TryGetValue(PackPair(a, b), out var s) ? s : _defaultState;
        }

        /// <summary>Set relation between two owners.</summary>
        public void Set(int a, int b, TState state)
        {
            if (a == b) return;
            _relations[PackPair(a, b)] = state;
        }

        /// <summary>Check if two owners have a specific relation.</summary>
        public bool Is(int a, int b, TState state) => EqualityComparer<TState>.Default.Equals(Get(a, b), state);

        private static long PackPair(int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            return ((long)lo << 32) | (uint)hi;
        }
    }
}
