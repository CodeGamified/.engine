// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Tracks an entity traveling along a path of edges through a world graph.
    ///
    /// Inherit to add domain-specific fields:
    ///   ActiveVoyage : ActiveTraversal&lt;OceanRoute&gt;   → adds ShipId, Intent, EngagementId
    ///   CaravanTrip  : ActiveTraversal&lt;RoadSegment&gt;  → adds CaravanId, Cargo
    /// </summary>
    public class ActiveTraversal<TEdge> where TEdge : WorldGraphEdge
    {
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public List<TEdge> Path { get; }
        public float TotalDistance { get; }
        public float DistanceTraveled { get; set; }
        public float Speed { get; set; } = 1f;

        /// <summary>Persistent per-traversal speed multiplier (hull bonus, curse, etc). Stacks with speedMod param.</summary>
        public float SpeedModifier { get; set; } = 1f;

        // Cached edge lookup — dirty on Advance()
        int _cachedEdgeId = -1;
        bool _edgeDirty = true;

        /// <summary>Progress 0..1.</summary>
        public float Progress => TotalDistance > 0f ? Mathf.Clamp01(DistanceTraveled / TotalDistance) : 1f;

        /// <summary>Has the entity reached the destination?</summary>
        public bool IsComplete => DistanceTraveled >= TotalDistance;

        /// <summary>ID of the edge currently being traversed. Cached; recomputed on Advance().</summary>
        public int CurrentEdgeId
        {
            get
            {
                if (!_edgeDirty) return _cachedEdgeId;
                _edgeDirty = false;

                float dist = 0f;
                foreach (var e in Path)
                {
                    dist += e.Distance;
                    if (dist >= DistanceTraveled) { _cachedEdgeId = e.Id; return _cachedEdgeId; }
                }
                _cachedEdgeId = Path.Count > 0 ? Path[Path.Count - 1].Id : -1;
                return _cachedEdgeId;
            }
        }

        public ActiveTraversal(int fromNode, int toNode, List<TEdge> path)
        {
            FromNodeId = fromNode;
            ToNodeId = toNode;
            Path = path;
            float d = 0f;
            foreach (var e in path) d += e.Distance;
            TotalDistance = d;
        }

        /// <summary>Advance travel by dt seconds, with optional external speed modifier.</summary>
        public void Advance(float dt, float speedMod = 1f)
        {
            DistanceTraveled += Speed * SpeedModifier * speedMod * dt;
            _edgeDirty = true;
        }
    }
}
