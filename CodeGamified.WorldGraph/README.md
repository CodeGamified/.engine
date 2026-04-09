# CodeGamified.WorldGraph

Generic weighted node-edge world map framework with pathfinding, traversal tracking,
pairwise relations, biome contracts, and procedural layout generators.

## Classes

| Class | Purpose |
|---|---|
| `WorldGraphNode` | Base node: ID, Name, Position, OwnerId, Category, EdgeIds |
| `WorldGraphEdge` | Base edge: ID, NodeA, NodeB, Distance, Risk, OtherNode() |
| `WorldGraph<TNode,TEdge>` | Generic graph: add/query/Dijkstra pathfinding |
| `ActiveTraversal<TEdge>` | Entity traveling along edges: speed, progress, current edge |
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
