# CodeGamified.WorldGraph

Generic weighted node-edge world map framework with pathfinding, traversal tracking,
pairwise relations, biome contracts, and procedural layout generators.

## Classes

| Class | Purpose |
|---|---|
| `WorldGraphNode` | Base node: ID, Name, Position, OwnerId, Category, EdgeIds |
| `WorldGraphEdge` | Base edge: ID, NodeA, NodeB, Distance, Risk, OtherNode() |
| `WorldGraph<TNode,TEdge>` | Generic graph: add/query/claim/Dijkstra pathfinding (with optional cost override) |
| `ActiveTraversal<TEdge>` | Entity traveling along edges: speed, SpeedModifier, progress, current edge |
| `TraversalManager<T,TEdge>` | Tick loop: advance traversals, detect arrivals + edge encounters via callbacks |
| `RelationMatrix<TState>` | Symmetric pairwise relations between owners |
| `BiomeDefinition` | Abstract biome: resource abundance + material palette contract |
| `ArcMapGenerator` | Semicircular arc layout with adjacent + cross-link connectivity |

## Usage

```csharp
// Define game-specific types
class MyNode : WorldGraphNode { public string Faction; }
class MyEdge : WorldGraphEdge
{
    public MyEdge(int a, int b, float d) : base(a, b, d) { }
}

// Build graph
var graph = new WorldGraph<MyNode, MyEdge>();
graph.AddNode(new MyNode { Name = "Alpha", Position = new(0, 0) });
graph.AddNode(new MyNode { Name = "Beta", Position = new(10, 0) });
graph.AddEdge(new MyEdge(0, 1, 10f));

// Pathfind
var path = graph.FindPath(0, 1);

// Track travel
var trip = new ActiveTraversal<MyEdge>(0, 1, path) { Speed = 2f };
trip.Advance(deltaTime);
if (trip.IsComplete) { /* arrived */ }

// Managed traversal loop with arrival + encounter detection
var mgr = new TraversalManager<ActiveTraversal<MyEdge>, MyEdge>();
mgr.OnArrival = t => Debug.Log($"Arrived at node {t.ToNodeId}");
mgr.ShouldPause = t => false;
mgr.GetSpeedMod = t => weatherSystem.SpeedMod;
mgr.ShouldEngage = (a, b) => relations.Is(a.OwnerId, b.OwnerId, MyRelation.Hostile);
mgr.OnEdgeEncounter = (a, b) => StartCombat(a, b);
mgr.Add(trip);
mgr.Tick(deltaTime); // advances, fires callbacks

// Dynamic-cost pathfinding (e.g. avoid stormy routes)
var safePath = graph.FindPath(0, 1, e => e.Distance * stormCost[e.Id]);

// Claim a node
graph.ClaimNode(nodeId, playerId, expectedCategory: 0, newCategory: 1);

// Pairwise relations
var relations = new RelationMatrix<MyRelation>(MyRelation.Neutral, MyRelation.Friendly);
relations.Set(playerA, playerB, MyRelation.Hostile);

// Arc layout
var graph = ArcMapGenerator.Generate<MyNode, MyEdge>(
    ArcMapConfig.Default,
    (i, pos) => new MyNode { Name = $"Node {i}", Position = pos },
    (a, b, dist) => new MyEdge(a, b, dist));
```

## Dependencies

None (uses UnityEngine for Vector2/Mathf).
