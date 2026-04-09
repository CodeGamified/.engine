// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Base node in a world graph. Games subclass to add domain-specific data
    /// (biome, faction, loot tables, etc).
    ///
    /// ID is assigned by the graph on insertion — never set externally.
    /// Category is a game-defined int (e.g. 0=unclaimed, 1=player, 2=NPC).
    /// </summary>
    public class WorldGraphNode
    {
        /// <summary>Unique ID within the graph. Assigned by WorldGraph.AddNode().</summary>
        public int Id { get; internal set; } = -1;

        /// <summary>Display name.</summary>
        public string Name { get; set; }

        /// <summary>2D position on the world map.</summary>
        public Vector2 Position { get; set; }

        /// <summary>Owner ID (-1 = unclaimed/NPC).</summary>
        public int OwnerId { get; set; } = -1;

        /// <summary>Game-defined category. Meaning varies per game.</summary>
        public int Category { get; set; }

        /// <summary>IDs of edges incident to this node. Populated by WorldGraph.AddEdge().</summary>
        public List<int> EdgeIds { get; } = new();

        public WorldGraphNode() { }

        public WorldGraphNode(string name, Vector2 position)
        {
            Name = name;
            Position = position;
        }
    }
}
