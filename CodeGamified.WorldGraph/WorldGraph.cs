// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Generic weighted node-edge graph with Dijkstra pathfinding.
    ///
    /// Games parameterize with their own node/edge subclasses:
    ///   WorldGraph&lt;ShorelineNode, OceanRoute&gt;   (SeaRauber)
    ///   WorldGraph&lt;StationNode, TradeRoute&gt;      (space game)
    ///   WorldGraph&lt;TownNode, RoadSegment&gt;         (overland game)
    ///
    /// Graph assigns IDs on insertion — callers never set IDs.
    /// </summary>
    public class WorldGraph<TNode, TEdge>
        where TNode : WorldGraphNode
        where TEdge : WorldGraphEdge
    {
        private readonly List<TNode> _nodes = new();
        private readonly List<TEdge> _edges = new();
        private readonly Dictionary<int, TNode> _nodeById = new();

        public IReadOnlyList<TNode> Nodes => _nodes;
        public IReadOnlyList<TEdge> Edges => _edges;

        // ═══════════════════════════════════════════════════════
        // BUILD
        // ═══════════════════════════════════════════════════════

        /// <summary>Add a node. ID is auto-assigned.</summary>
        public TNode AddNode(TNode node)
        {
            node.Id = _nodes.Count;
            _nodes.Add(node);
            _nodeById[node.Id] = node;
            return node;
        }

        /// <summary>Add an edge. ID is auto-assigned. Updates both nodes' EdgeIds.</summary>
        public TEdge AddEdge(TEdge edge)
        {
            edge.Id = _edges.Count;
            _edges.Add(edge);
            if (_nodeById.TryGetValue(edge.NodeA, out var a)) a.EdgeIds.Add(edge.Id);
            if (_nodeById.TryGetValue(edge.NodeB, out var b)) b.EdgeIds.Add(edge.Id);
            return edge;
        }

        // ═══════════════════════════════════════════════════════
        // QUERIES
        // ═══════════════════════════════════════════════════════

        public TNode GetNode(int id) => _nodeById.TryGetValue(id, out var n) ? n : null;

        public TEdge GetEdge(int id) => id >= 0 && id < _edges.Count ? _edges[id] : null;

        public TNode FindNodeByName(string name)
        {
            foreach (var n in _nodes)
                if (n.Name == name) return n;
            return null;
        }

        /// <summary>Find nodes matching a predicate.</summary>
        public List<TNode> FindNodes(Predicate<TNode> predicate)
        {
            var result = new List<TNode>();
            foreach (var n in _nodes)
                if (predicate(n)) result.Add(n);
            return result;
        }

        /// <summary>Get all edges incident to a node.</summary>
        public List<TEdge> GetEdgesFrom(int nodeId)
        {
            var result = new List<TEdge>();
            var node = GetNode(nodeId);
            if (node == null) return result;
            foreach (int eid in node.EdgeIds)
                if (eid >= 0 && eid < _edges.Count) result.Add(_edges[eid]);
            return result;
        }

        // ═══════════════════════════════════════════════════════
        // PATHFINDING — Dijkstra on weighted edges
        // ═══════════════════════════════════════════════════════

        /// <summary>Find shortest path between two nodes. Returns ordered edge list.</summary>
        public List<TEdge> FindPath(int fromNodeId, int toNodeId)
        {
            if (fromNodeId == toNodeId) return new List<TEdge>();

            var dist = new Dictionary<int, float>();
            var prev = new Dictionary<int, (int nodeId, TEdge edge)>();
            var visited = new HashSet<int>();
            int seq = 0;
            var queue = new SortedSet<(float cost, int seq, int nodeId)>();

            dist[fromNodeId] = 0f;
            queue.Add((0f, seq++, fromNodeId));

            while (queue.Count > 0)
            {
                var (cost, _, current) = queue.Min;
                queue.Remove(queue.Min);

                if (current == toNodeId) break;
                if (!visited.Add(current)) continue;

                foreach (var edge in GetEdgesFrom(current))
                {
                    int neighbor = edge.OtherNode(current);
                    float newDist = cost + edge.Distance;
                    if (!dist.ContainsKey(neighbor) || newDist < dist[neighbor])
                    {
                        dist[neighbor] = newDist;
                        prev[neighbor] = (current, edge);
                        queue.Add((newDist, seq++, neighbor));
                    }
                }
            }

            var path = new List<TEdge>();
            if (!prev.ContainsKey(toNodeId)) return path;

            int at = toNodeId;
            while (prev.ContainsKey(at))
            {
                var (fromNode, edge) = prev[at];
                path.Add(edge);
                at = fromNode;
            }
            path.Reverse();
            return path;
        }

        /// <summary>Total distance along a path of edges.</summary>
        public static float PathDistance(List<TEdge> path)
        {
            float d = 0f;
            foreach (var e in path) d += e.Distance;
            return d;
        }
    }
}
