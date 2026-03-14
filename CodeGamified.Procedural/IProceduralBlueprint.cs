// CodeGamified.Procedural — Shared procedural rendering framework
// MIT License

namespace CodeGamified.Procedural
{
    /// <summary>
    /// Bridge between game-specific data models and generic procedural assembly.
    ///
    /// Each game's data model implements this to emit a flat list of parts:
    ///
    ///   Ship:       ShipManifest.Blueprint → ribs + subcomponents → ProceduralPartDef[]
    ///   Satellite:  PrefabConfig DSL       → parsed parts          → ProceduralPartDef[]
    ///   Rack:       RackConfiguration      → slot cubes            → ProceduralPartDef[]
    ///   Launch:     RocketType enum        → staged proportions    → ProceduralPartDef[]
    ///   Pong:       PongManifest           → paddle + ball         → ProceduralPartDef[]
    ///
    /// The assembler consumes this interface — it never knows what game it's building for.
    /// </summary>
    public interface IProceduralBlueprint
    {
        /// <summary>
        /// Emit all parts that make up this entity's visual representation.
        /// Parts are assembled in order. ParentId references resolve against earlier parts.
        /// </summary>
        ProceduralPartDef[] GetParts();

        /// <summary>
        /// LOD hint for the assembler (controls collider defaults, label generation, etc).
        /// </summary>
        ProceduralLODHint LODHint { get; }

        /// <summary>
        /// Color palette ID. The assembler uses this to resolve ColorKey strings on each part.
        /// If null, the assembler's default palette is used.
        /// </summary>
        string PaletteId { get; }

        /// <summary>
        /// Root GameObject name.
        /// </summary>
        string DisplayName { get; }
    }
}
