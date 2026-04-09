// CodeGamified.WorldGraph — Shared world map graph framework
// MIT License
using System;
using UnityEngine;

namespace CodeGamified.WorldGraph
{
    /// <summary>
    /// Configuration for arc-based world map generation.
    /// </summary>
    public struct ArcMapConfig
    {
        /// <summary>Radius of the arc in world units.</summary>
        public float Radius;

        /// <summary>Arc start angle in degrees.</summary>
        public float StartAngleDeg;

        /// <summary>Arc end angle in degrees.</summary>
        public float EndAngleDeg;

        /// <summary>Total number of nodes to place on the arc.</summary>
        public int TotalNodes;

        /// <summary>Multiplier: raw euclidean distance → edge distance (adjacent).</summary>
        public float DistanceScale;

        /// <summary>Multiplier: raw euclidean distance → edge distance (cross-links).</summary>
        public float CrossLinkDistScale;

        public static ArcMapConfig Default => new()
        {
            Radius = 50f,
            StartAngleDeg = 30f,
            EndAngleDeg = 150f,
            TotalNodes = 28,
            DistanceScale = 0.1f,
            CrossLinkDistScale = 0.12f,
        };
    }

    /// <summary>
    /// Generates a world graph by placing nodes on a semicircular arc and
    /// connecting adjacent nodes + skip-1 cross-links.
    ///
    /// Game provides factory delegates to create typed nodes and edges
    /// with domain-specific data (biome, risk, name, category).
    /// </summary>
    public static class ArcMapGenerator
    {
        /// <summary>
        /// Generate a world graph on an arc layout.
        /// </summary>
        /// <param name="config">Arc geometry and distance scaling.</param>
        /// <param name="createNode">Factory: (index, position) → node. Set name, category, etc.</param>
        /// <param name="createEdge">Factory: (nodeA, nodeB, scaledDistance) → edge. Set risk, etc.</param>
        public static WorldGraph<TNode, TEdge> Generate<TNode, TEdge>(
            ArcMapConfig config,
            Func<int, Vector2, TNode> createNode,
            Func<int, int, float, TEdge> createEdge)
            where TNode : WorldGraphNode
            where TEdge : WorldGraphEdge
        {
            var graph = new WorldGraph<TNode, TEdge>();

            float startRad = config.StartAngleDeg * Mathf.Deg2Rad;
            float endRad = config.EndAngleDeg * Mathf.Deg2Rad;
            float step = config.TotalNodes > 1
                ? (endRad - startRad) / (config.TotalNodes - 1)
                : 0f;

            // Place nodes on arc
            for (int i = 0; i < config.TotalNodes; i++)
            {
                float angle = startRad + step * i;
                var pos = new Vector2(
                    Mathf.Cos(angle) * config.Radius,
                    Mathf.Sin(angle) * config.Radius);
                graph.AddNode(createNode(i, pos));
            }

            // Connect adjacent nodes
            for (int i = 0; i < config.TotalNodes - 1; i++)
            {
                float dist = Vector2.Distance(
                    graph.GetNode(i).Position,
                    graph.GetNode(i + 1).Position);
                graph.AddEdge(createEdge(i, i + 1, dist * config.DistanceScale));
            }

            // Cross-links: skip-1 neighbors for alternative routes
            for (int i = 0; i < config.TotalNodes - 2; i += 2)
            {
                float dist = Vector2.Distance(
                    graph.GetNode(i).Position,
                    graph.GetNode(i + 2).Position);
                graph.AddEdge(createEdge(i, i + 2, dist * config.CrossLinkDistScale));
            }

            return graph;
        }
    }
}
