// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Manages a collection of active traversals — advances them each tick,
    /// detects arrivals, and detects edge encounters between pairs.
    ///
    /// Games wire domain-specific behavior through callbacks:
    ///   OnArrival        → dock ship, transfer crew, trigger port interaction
    ///   OnEdgeEncounter  → create naval engagement, start combat
    ///   ShouldPause      → skip advance while in combat
    ///   GetSpeedMod      → apply weather, curses, buffs
    ///   ShouldEngage     → filter by diplomacy, intent, ship state
    /// </summary>
    public class TraversalManager<TTraversal, TEdge>
        where TTraversal : ActiveTraversal<TEdge>
        where TEdge : WorldGraphEdge
    {
        private readonly List<TTraversal> _active = new();

        public IReadOnlyList<TTraversal> Active => _active;
        public int Count => _active.Count;

        // ═══════════════════════════════════════════════════════
        // CALLBACKS — game wires these to domain-specific handlers
        // ═══════════════════════════════════════════════════════

        /// <summary>Fired when a traversal reaches its destination (after removal from active list).</summary>
        public Action<TTraversal> OnArrival;

        /// <summary>Fired when two traversals occupy the same edge and ShouldEngage returns true.</summary>
        public Action<TTraversal, TTraversal> OnEdgeEncounter;

        // ═══════════════════════════════════════════════════════
        // PREDICATES — game provides filtering logic
        // ═══════════════════════════════════════════════════════

        /// <summary>Return true to skip advancing this traversal (e.g. in combat).</summary>
        public Func<TTraversal, bool> ShouldPause;

        /// <summary>Return external speed modifier for this traversal (weather, buffs). Default 1.</summary>
        public Func<TTraversal, float> GetSpeedMod;

        /// <summary>Return true if two traversals on the same edge should trigger OnEdgeEncounter.</summary>
        public Func<TTraversal, TTraversal, bool> ShouldEngage;

        // ═══════════════════════════════════════════════════════
        // MUTATORS
        // ═══════════════════════════════════════════════════════

        public void Add(TTraversal t) => _active.Add(t);

        public bool Remove(TTraversal t) => _active.Remove(t);

        public int RemoveAll(Predicate<TTraversal> predicate) => _active.RemoveAll(predicate);

        // ═══════════════════════════════════════════════════════
        // TICK — advance, detect arrivals, detect encounters
        // ═══════════════════════════════════════════════════════

        public void Tick(float dt)
        {
            // Advance + detect arrivals (backward iteration, swap-and-pop removal)
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                if (ShouldPause != null && ShouldPause(t)) continue;
                float mod = GetSpeedMod != null ? GetSpeedMod(t) : 1f;
                t.Advance(dt, mod);

                if (t.IsComplete)
                {
                    int last = _active.Count - 1;
                    if (i < last) _active[i] = _active[last];
                    _active.RemoveAt(last);
                    OnArrival?.Invoke(t);
                }
            }

            // Edge encounter detection
            if (OnEdgeEncounter != null && ShouldEngage != null)
                DetectEdgeEncounters();
        }

        // ═══════════════════════════════════════════════════════
        // ENCOUNTER DETECTION — spatial hash on edge ID
        // ═══════════════════════════════════════════════════════

        private readonly Dictionary<int, List<int>> _edgeHash = new();

        private void DetectEdgeEncounters()
        {
            _edgeHash.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                var t = _active[i];
                if (t.IsComplete) continue;
                int eid = t.CurrentEdgeId;
                if (eid < 0) continue;
                if (!_edgeHash.TryGetValue(eid, out var list))
                {
                    list = new List<int>();
                    _edgeHash[eid] = list;
                }
                list.Add(i);
            }

            foreach (var kv in _edgeHash)
            {
                var indices = kv.Value;
                if (indices.Count < 2) continue;
                for (int ai = 0; ai < indices.Count; ai++)
                {
                    var a = _active[indices[ai]];
                    for (int bi = ai + 1; bi < indices.Count; bi++)
                    {
                        var b = _active[indices[bi]];
                        if (ShouldEngage(a, b))
                            OnEdgeEncounter(a, b);
                    }
                }
            }
        }
    }
}
