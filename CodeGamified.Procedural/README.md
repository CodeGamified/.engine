# CodeGamified.Procedural

Blueprint-driven procedural mesh assembly framework.  
Game data → part list → assembled GameObject hierarchy + runtime animation.

## Architecture

```
IProceduralBlueprint           Game implements: emit ProceduralPartDef[]
        ↓
ProceduralAssembler.Build()    Static factory: parts → GameObjects + materials + colliders
        ↓
AssemblyResult                 Root GameObject + Renderer lookup by part ID
        ↓
ProceduralVisualState          Optional MonoBehaviour: pulse, throb, bind animations
```

Replaces: `HullRibBuilder` (Ship), `SatellitePrefabFactory` (Satellite),
`RackVisualization` (Rack), `LaunchVehiclePrefabFactory` (Launch), `CrewAgent.SetupVisual` (Crew).

## Files

| File | Purpose |
|------|---------|
| `IProceduralBlueprint.cs` | Interface — `GetParts()`, `LODHint`, `PaletteId`, `DisplayName` |
| `ProceduralPartDef.cs` | Struct — shape, position, scale, rotation, color key, collider mode, parent ID |
| `ProceduralAssembler.cs` | Static factory — `Build(blueprint, palette, shader?)` → `AssemblyResult` |
| `ProceduralVisualState.cs` | MonoBehaviour — imperative (Pulse/Throb) + declarative (Bind) animation |
| `ColorPalette.cs` | ScriptableObject — keyed color map, Inspector-editable, runtime-creatable |

## Dependencies

| Assembly | References |
|---|---|
| `CodeGamified.Procedural` | — (no dependencies) |

## Integration Pattern

### 1. Implement `IProceduralBlueprint`

```csharp
using CodeGamified.Procedural;

public class PaddleBlueprint : IProceduralBlueprint
{
    public string DisplayName => "Paddle";
    public string PaletteId => "arcade";
    public ProceduralLODHint LODHint => ProceduralLODHint.Lightweight;

    public ProceduralPartDef[] GetParts() => new[]
    {
        new ProceduralPartDef("body", PrimitiveType.Cube,
            Vector3.zero, new Vector3(3, 0.5f, 0.5f), "paddle_white"),
        new ProceduralPartDef("glow", PrimitiveType.Cube,
            Vector3.zero, new Vector3(3.1f, 0.6f, 0.6f), "paddle_glow")
        { Tag = "visual", Layer = "Ignore Raycast" }
    };
}
```

### 2. Create a `ColorPalette`

**Inspector:** Create → CodeGamified → Color Palette. Add key/color entries.

**Runtime:**
```csharp
var palette = ColorPalette.CreateRuntime(new Dictionary<string, Color>
{
    { "paddle_white", Color.white },
    { "paddle_glow",  new Color(0.2f, 0.5f, 1f, 0.3f) },
    { "ball_orange",  new Color(1f, 0.6f, 0f) }
});
```

### 3. Build

```csharp
var result = ProceduralAssembler.Build(new PaddleBlueprint(), palette);
// result.Root       — assembled GameObject hierarchy
// result.Renderers  — Dictionary<string, Renderer> keyed by part ID
```

### 4. Animate with `ProceduralVisualState`

```csharp
var vis = result.Root.AddComponent<ProceduralVisualState>();
vis.Initialize(result.Renderers);

// Imperative — fire and forget
vis.Pulse("glow", Color.cyan, duration: 0.3f);
vis.Throb("body", scaleMultiplier: 1.2f, duration: 0.15f);

// Declarative — continuously driven
vis.Bind("glow", VisualChannel.Emission, () => hitIntensity, min: 0f, max: 2f);
vis.Bind("body", VisualChannel.ScaleY, () => chargeLevel, min: 1f, max: 1.5f);
```

## `ProceduralPartDef` Fields

| Field | Type | Purpose |
|-------|------|---------|
| `Id` | string | Unique ID within blueprint — hierarchy + animation targeting |
| `Shape` | PrimitiveType | Unity primitive (ignored when `CustomMesh` is set) |
| `CustomMesh` | Mesh | Optional pre-built mesh |
| `LocalPos` | Vector3 | Position relative to parent |
| `LocalScale` | Vector3 | Scale |
| `LocalRot` | Quaternion | Rotation |
| `ColorKey` | string | Resolved by `ColorPalette` at assembly time |
| `Tag` | string | Semantic grouping ("structure", "engine", "visual") |
| `Collider` | ColliderMode | `None`, `Box`, `Mesh`, `ConvexMesh` |
| `ParentId` | string | Parent part ID (null = child of root) |
| `Layer` | string | Layer name override |

## Enums

### `ColliderMode`

| Value | Usage |
|-------|-------|
| `None` | Visual only (exhaust, rigging) |
| `Box` | BoxCollider sized to bounds |
| `Mesh` | MeshCollider for raycast selection |
| `ConvexMesh` | MeshCollider convex=true for physics |

### `ProceduralLODHint`

| Value | Usage |
|-------|-------|
| `Lightweight` | 1-2 primitives, no labels, GPU-instancing friendly |
| `Standard` | Full primitives with colliders |
| `Detailed` | Custom meshes, labels, particle effects |

### `VisualChannel`

| Value | Drives |
|-------|--------|
| `Emission` | Material emission intensity (glow) |
| `ScaleY` | Local Y scale (fill bars, growth) |
| `ColorAlpha` | Material alpha (fade) |
| `PositionY` | Local Y offset (bob, bounce) |
| `ColorTint` | Blend toward a target color |

## Shader Fallback

`ProceduralAssembler` auto-selects: URP Unlit → URP Lit → Standard.
Pass an explicit `Shader` to `Build()` to override.
