// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Base edge connecting two nodes in a world graph.
    /// Games subclass to add domain-specific data (weather risk, encounter tables, etc).
    ///
    /// ID is assigned by the graph on insertion.
    /// </summary>
    public class WorldGraphEdge
    {
        /// <summary>Unique ID within the graph. Assigned by WorldGraph.AddEdge().</summary>
        public int Id { get; internal set; } = -1;

        /// <summary>Source node ID.</summary>
        public int NodeA { get; }

        /// <summary>Destination node ID.</summary>
        public int NodeB { get; }

        /// <summary>Travel distance (in game-defined units).</summary>
        public float Distance { get; set; }

        /// <summary>Risk factor 0–1 (game-defined: encounter chance, hazard level, etc).</summary>
        public float Risk { get; set; }

        public WorldGraphEdge(int nodeA, int nodeB, float distance, float risk = 0f)
        {
            NodeA = nodeA;
            NodeB = nodeB;
            Distance = distance;
            Risk = risk;
        }

        /// <summary>Get the node on the other end of this edge.</summary>
        public int OtherNode(int fromNode) => fromNode == NodeA ? NodeB : NodeA;
    }
}
