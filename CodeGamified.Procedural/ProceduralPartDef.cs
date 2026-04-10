// CodeGamified.Procedural — Shared procedural rendering framework
// MIT License
using UnityEngine;

namespace CodeGamified.Procedural
{
    /// <summary>
    /// Collider strategy for a procedural part.
    /// </summary>
    public enum ColliderMode
    {
        None,       // No collider (visual only, e.g. exhaust, rigging)
        Box,        // BoxCollider sized to primitive bounds
        Mesh,       // MeshCollider (for raycast selection on complex shapes)
        ConvexMesh  // MeshCollider with convex=true (for physics)
    }

    /// <summary>
    /// LOD hint — tells the assembler which detail tier this blueprint targets.
    /// </summary>
    public enum ProceduralLODHint
    {
        Lightweight, // Minimum geometry (1-2 primitives, no labels, GPU-instancing friendly)
        Standard,    // Full primitives with colliders
        Detailed     // Custom meshes, labels, particle effects
    }

    /// <summary>
    /// Universal part descriptor.
    ///
    /// Replaces per-module structs:
    ///   Ship:      SubcomponentDef (name, shape, pos, scale, color)
    ///   Satellite: DSL string "type:size:position:color"
    ///   Rack:      SlotVisual (index, slot, renderer, emission)
    ///   Launch:    Hardcoded CreatePrimitive calls per stage
    ///   Crew:      Inline CreatePrimitive(Sphere) in SetupVisual
    ///
    /// One struct to describe any primitive part in any game.
    /// </summary>
    public struct ProceduralPartDef
    {
        /// <summary>Unique ID within the blueprint. Used for hierarchy and animation targeting.</summary>
        public string Id;

        /// <summary>Unity primitive type. Ignored when CustomMesh is set.</summary>
        public PrimitiveType Shape;

        /// <summary>Optional pre-built mesh. When set, Shape is ignored.</summary>
        public Mesh CustomMesh;

        /// <summary>Local position relative to parent.</summary>
        public Vector3 LocalPos;

        /// <summary>Local scale.</summary>
        public Vector3 LocalScale;

        /// <summary>Local rotation.</summary>
        public Quaternion LocalRot;

        /// <summary>Color palette key. Resolved by ColorPalette at assembly time.</summary>
        public string ColorKey;

        /// <summary>Semantic tag for grouping (e.g. "structure", "engine", "visual", "crew").</summary>
        public string Tag;

        /// <summary>Collider strategy.</summary>
        public ColliderMode Collider;

        /// <summary>Parent part ID. Empty/null = child of root.</summary>
        public string ParentId;

        /// <summary>Layer name override (e.g. "Ignore Raycast"). Null = default.</summary>
        public string Layer;

        /// <summary>When true, emit a single double-sided quad instead of a full box.</summary>
        public bool IsQuad;

        /// <summary>Optional texture path for atlas/material lookup. Null = use ColorKey only.</summary>
        public string TexturePath;

        // ═══════════════════════════════════════════════════════════════
        // CONVENIENCE CONSTRUCTORS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Minimal: shape + position + scale + color.</summary>
        public ProceduralPartDef(string id, PrimitiveType shape, Vector3 pos, Vector3 scale, string colorKey)
        {
            Id = id;
            Shape = shape;
            CustomMesh = null;
            LocalPos = pos;
            LocalScale = scale;
            LocalRot = Quaternion.identity;
            ColorKey = colorKey;
            Tag = null;
            Collider = ColliderMode.None;
            ParentId = null;
            Layer = null;
            IsQuad = false;
            TexturePath = null;
        }

        /// <summary>Full constructor.</summary>
        public ProceduralPartDef(
            string id, PrimitiveType shape, Vector3 pos, Vector3 scale, Quaternion rot,
            string colorKey, string tag, ColliderMode collider, string parentId)
        {
            Id = id;
            Shape = shape;
            CustomMesh = null;
            LocalPos = pos;
            LocalScale = scale;
            LocalRot = rot;
            ColorKey = colorKey;
            Tag = tag;
            Collider = collider;
            ParentId = parentId;
            Layer = null;
            IsQuad = false;
            TexturePath = null;
        }
    }
}
