// CodeGamified.Procedural — Shared procedural rendering framework
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace CodeGamified.Procedural
{
    /// <summary>
    /// Single factory that assembles any IProceduralBlueprint into a GameObject hierarchy.
    ///
    /// Replaces:
    ///   HullRibBuilder.BuildHullMesh         (Ship)
    ///   SatellitePrefabFactory.Create*        (Satellite)
    ///   RackVisualization.CreateSlotVisual    (Rack)
    ///   LaunchVehiclePrefabFactory.Create*    (Launch)
    ///   CrewAgent.SetupVisual                 (Crew)
    ///
    /// All primitive creation, material assignment, collider policy, and hierarchy
    /// wiring flows through here.
    /// </summary>
    public static class ProceduralAssembler
    {
        // Shader name fallback chain: HDRP → URP → Built-in
        private static readonly string[] ShaderFallbacks = new[]
        {
            "HDRP/Lit",
            "Universal Render Pipeline/Lit",
            "Standard"
        };

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Build a complete GameObject from a blueprint.
        /// </summary>
        public static AssemblyResult Build(IProceduralBlueprint blueprint, ColorPalette palette, Shader shader = null)
        {
            if (blueprint == null)
            {
                Debug.LogError("[ProceduralAssembler] Blueprint is null");
                return AssemblyResult.Empty;
            }

            var parts = blueprint.GetParts();
            if (parts == null || parts.Length == 0)
            {
                Debug.LogWarning("[ProceduralAssembler] Blueprint returned no parts");
                return AssemblyResult.Empty;
            }

            if (shader == null)
                shader = FindFallbackShader();

            var root = new GameObject(blueprint.DisplayName ?? "Procedural");
            var lookup = new Dictionary<string, Transform>(parts.Length);
            var renderers = new Dictionary<string, Renderer>(parts.Length);

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                // Resolve parent
                Transform parent = root.transform;
                if (!string.IsNullOrEmpty(part.ParentId) && lookup.TryGetValue(part.ParentId, out var p))
                    parent = p;

                // Create the GameObject
                GameObject go = CreateGeometry(part);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = part.LocalPos;
                go.transform.localScale = part.LocalScale;
                go.transform.localRotation = part.LocalRot;

                // Layer
                if (!string.IsNullOrEmpty(part.Layer))
                {
                    int layer = LayerMask.NameToLayer(part.Layer);
                    if (layer >= 0) go.layer = layer;
                }

                // Material
                ApplyMaterial(go, palette, shader, part.ColorKey);

                // Collider policy
                ApplyCollider(go, part);

                // Register
                if (!string.IsNullOrEmpty(part.Id))
                {
                    lookup[part.Id] = go.transform;
                    var r = go.GetComponent<Renderer>();
                    if (r != null) renderers[part.Id] = r;
                }
            }

            return new AssemblyResult
            {
                Root = root,
                Parts = lookup,
                Renderers = renderers
            };
        }

        /// <summary>
        /// Build and attach a ProceduralVisualState component for animation binding.
        /// Convenience: Build() + auto-attach.
        /// </summary>
        public static AssemblyResult BuildWithVisualState(
            IProceduralBlueprint blueprint, ColorPalette palette, Shader shader = null)
        {
            var result = Build(blueprint, palette, shader);
            if (result.Root == null) return result;

            var vs = result.Root.AddComponent<ProceduralVisualState>();
            vs.Initialize(result.Renderers);
            result.VisualState = vs;
            return result;
        }

        // ═══════════════════════════════════════════════════════════════
        // GEOMETRY
        // ═══════════════════════════════════════════════════════════════

        static GameObject CreateGeometry(ProceduralPartDef part)
        {
            if (part.CustomMesh != null)
            {
                var go = new GameObject(part.Id ?? "CustomMesh");
                var mf = go.AddComponent<MeshFilter>();
                mf.mesh = part.CustomMesh;
                go.AddComponent<MeshRenderer>();
                return go;
            }

            var prim = GameObject.CreatePrimitive(part.Shape);
            prim.name = part.Id ?? part.Shape.ToString();

            // CreatePrimitive adds a default collider — remove it.
            // We apply our own collider policy afterward.
            var defaultCollider = prim.GetComponent<Collider>();
            if (defaultCollider != null)
                Object.Destroy(defaultCollider);

            return prim;
        }

        // ═══════════════════════════════════════════════════════════════
        // MATERIAL
        // ═══════════════════════════════════════════════════════════════

        static void ApplyMaterial(GameObject go, ColorPalette palette, Shader shader, string colorKey)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Color color = palette != null ? palette.Resolve(colorKey) : Color.gray;

            var mat = new Material(shader);

            // Try HDRP property first, then Standard fallback
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", color);
            else
                mat.color = color;

            // Enable emission so Pulse / SetEmission / Bind(Emission) actually work
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            renderer.material = mat;
        }

        // ═══════════════════════════════════════════════════════════════
        // COLLIDER
        // ═══════════════════════════════════════════════════════════════

        static void ApplyCollider(GameObject go, ProceduralPartDef part)
        {
            switch (part.Collider)
            {
                case ColliderMode.None:
                    break;

                case ColliderMode.Box:
                    go.AddComponent<BoxCollider>();
                    break;

                case ColliderMode.Mesh:
                    var mc = go.AddComponent<MeshCollider>();
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null) mc.sharedMesh = mf.sharedMesh;
                    break;

                case ColliderMode.ConvexMesh:
                    var mcc = go.AddComponent<MeshCollider>();
                    mcc.convex = true;
                    var mfc = go.GetComponent<MeshFilter>();
                    if (mfc != null) mcc.sharedMesh = mfc.sharedMesh;
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // SHADER FALLBACK
        // ═══════════════════════════════════════════════════════════════

        static Shader FindFallbackShader()
        {
            for (int i = 0; i < ShaderFallbacks.Length; i++)
            {
                var s = Shader.Find(ShaderFallbacks[i]);
                if (s != null) return s;
            }
            Debug.LogWarning("[ProceduralAssembler] No render pipeline shader found, using fallback");
            return Shader.Find("Unlit/Color");
        }
    }

    /// <summary>
    /// Result of assembling a blueprint. Gives the caller handles to the root,
    /// individual parts, and renderers for post-assembly wiring.
    /// </summary>
    public struct AssemblyResult
    {
        public GameObject Root;
        public Dictionary<string, Transform> Parts;
        public Dictionary<string, Renderer> Renderers;
        public ProceduralVisualState VisualState;

        public bool IsValid => Root != null;

        public static readonly AssemblyResult Empty = new AssemblyResult
        {
            Root = null,
            Parts = null,
            Renderers = null,
            VisualState = null
        };

        /// <summary>Get a part transform by ID. Returns null if not found.</summary>
        public Transform GetPart(string id)
        {
            if (Parts == null || string.IsNullOrEmpty(id)) return null;
            Parts.TryGetValue(id, out var t);
            return t;
        }

        /// <summary>Get a renderer by part ID. Returns null if not found.</summary>
        public Renderer GetRenderer(string id)
        {
            if (Renderers == null || string.IsNullOrEmpty(id)) return null;
            Renderers.TryGetValue(id, out var r);
            return r;
        }
    }
}
