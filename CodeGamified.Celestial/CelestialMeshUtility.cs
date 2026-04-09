// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Static mesh factory for celestial body geometry.
    /// Consolidates UV sphere, inverted sphere, and low-poly sphere generation
    /// into a single utility. All meshes use UInt32 indexing for high segment counts.
    ///
    /// Meshes are cached by (type, segments) key. Calling Create* with the same
    /// parameters returns a shared Mesh instance — zero GC after first call.
    /// Use <see cref="ClearCache"/> on scene unload if memory pressure is a concern.
    /// </summary>
    public static class CelestialMeshUtility
    {
        // ═══════════════════════════════════════════════════════════════
        // MESH CACHE — avoids GC on quality changes / layer rebuilds
        // ═══════════════════════════════════════════════════════════════

        enum MeshType { UV, Inverted, LowPoly }

        static readonly Dictionary<(MeshType, int), Mesh> _cache = new(16);

        /// <summary>Clear all cached meshes. Call on scene unload if needed.</summary>
        public static void ClearCache()
        {
            foreach (var m in _cache.Values)
                if (m != null) Object.Destroy(m);
            _cache.Clear();
        }

        static Mesh GetOrCreate(MeshType type, int segments, System.Func<Mesh> factory)
        {
            var key = (type, segments);
            if (_cache.TryGetValue(key, out var cached) && cached != null)
                return cached;
            var mesh = factory();
            _cache[key] = mesh;
            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // UV SPHERE — primary celestial bodies (Earth, Moon, Sun)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// High-quality UV sphere with tangents for normal mapping.
        /// Proper seam handling: first and last longitude vertices share position but differ in UV.
        /// Degenerate pole triangles skipped for clean topology.
        /// Pole tangents use RecalculateTangents for correct TBN.
        /// Cached by segment count — shared across all callers.
        /// </summary>
        public static Mesh CreateUVSphere(int segments, string name = "CelestialSphere")
        {
            return GetOrCreate(MeshType.UV, segments, () => CreateUVSphereInternal(segments, name));
        }

        static Mesh CreateUVSphereInternal(int segments, string name)
        {
            var mesh = new Mesh();
            mesh.name = $"{name}_{segments}";
            mesh.indexFormat = IndexFormat.UInt32;

            int vertCount = (segments + 1) * (segments + 1);
            var vertices = new Vector3[vertCount];
            var normals  = new Vector3[vertCount];
            var uvs      = new Vector2[vertCount];

            int idx = 0;
            for (int lat = 0; lat <= segments; lat++)
            {
                float theta    = lat * Mathf.PI / segments;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= segments; lon++)
                {
                    float phi    = lon * 2f * Mathf.PI / segments;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    float x = cosPhi * sinTheta;
                    float y = cosTheta;
                    float z = sinPhi * sinTheta;

                    vertices[idx] = new Vector3(x, y, z) * 0.5f;
                    normals[idx]  = new Vector3(x, y, z).normalized;
                    uvs[idx] = new Vector2((float)lon / segments, 1f - (float)lat / segments);
                    idx++;
                }
            }

            // Triangles — skip degenerate pole caps
            var triangles = new int[segments * segments * 6];
            int tri = 0;

            for (int lat = 0; lat < segments; lat++)
            {
                for (int lon = 0; lon < segments; lon++)
                {
                    int cur  = lat * (segments + 1) + lon;
                    int next = cur + segments + 1;

                    if (lat != 0)
                    {
                        triangles[tri++] = cur;
                        triangles[tri++] = cur + 1;
                        triangles[tri++] = next;
                    }

                    if (lat != segments - 1)
                    {
                        triangles[tri++] = cur + 1;
                        triangles[tri++] = next + 1;
                        triangles[tri++] = next;
                    }
                }
            }

            System.Array.Resize(ref triangles, tri);

            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = triangles;
            // RecalculateTangents handles pole degeneracy correctly
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // INVERTED SPHERE — skybox (normals inward, visible from inside)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Inverted sphere for skybox rendering. Normals point inward,
        /// winding order reversed so faces are visible from inside.
        /// Cached by segment count.
        /// </summary>
        public static Mesh CreateInvertedSphere(int segments, string name = "InvertedSphere")
        {
            return GetOrCreate(MeshType.Inverted, segments, () => CreateInvertedSphereInternal(segments, name));
        }

        static Mesh CreateInvertedSphereInternal(int segments, string name)
        {
            var mesh = new Mesh();
            mesh.name = $"{name}_{segments}";
            mesh.indexFormat = IndexFormat.UInt32;

            int vertCount = (segments + 1) * (segments + 1);
            var vertices = new Vector3[vertCount];
            var normals  = new Vector3[vertCount];
            var uvs      = new Vector2[vertCount];

            int idx = 0;
            for (int lat = 0; lat <= segments; lat++)
            {
                float theta    = lat * Mathf.PI / segments;
                float sinTheta = Mathf.Sin(theta);
                float cosTheta = Mathf.Cos(theta);

                for (int lon = 0; lon <= segments; lon++)
                {
                    float phi    = lon * 2f * Mathf.PI / segments;
                    float sinPhi = Mathf.Sin(phi);
                    float cosPhi = Mathf.Cos(phi);

                    float x = cosPhi * sinTheta;
                    float y = cosTheta;
                    float z = sinPhi * sinTheta;

                    vertices[idx] = new Vector3(x, y, z) * 0.5f;
                    normals[idx]  = -new Vector3(x, y, z).normalized; // Inward

                    float u = (float)lon / segments;
                    float v = 1f - (float)lat / segments;
                    uvs[idx] = new Vector2(u, v);
                    idx++;
                }
            }

            // Reversed winding for inward-facing triangles
            var triangles = new int[segments * segments * 6];
            int tri = 0;

            for (int lat = 0; lat < segments; lat++)
            {
                for (int lon = 0; lon < segments; lon++)
                {
                    int cur  = lat * (segments + 1) + lon;
                    int next = cur + segments + 1;

                    triangles[tri++] = cur;
                    triangles[tri++] = next;
                    triangles[tri++] = cur + 1;

                    triangles[tri++] = cur + 1;
                    triangles[tri++] = next;
                    triangles[tri++] = next + 1;
                }
            }

            mesh.vertices  = vertices;
            mesh.normals   = normals;
            mesh.uv        = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // LOW-POLY SPHERE — atmosphere layers, effects
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Low-poly sphere for atmosphere shells and subtle effects
        /// where triangle density isn't critical.
        /// Cached by segment count.
        /// </summary>
        public static Mesh CreateLowPolySphere(int segments, string name = "AtmosphereSphere")
        {
            return GetOrCreate(MeshType.LowPoly, segments, () => CreateLowPolySphereInternal(segments, name));
        }

        static Mesh CreateLowPolySphereInternal(int segments, string name)
        {
            var mesh = new Mesh();
            mesh.name = $"{name}_{segments}";

            int rings   = segments;
            int sectors = segments * 2;

            var vertices  = new System.Collections.Generic.List<Vector3>();
            var normals   = new System.Collections.Generic.List<Vector3>();
            var uvs       = new System.Collections.Generic.List<Vector2>();
            var triangles = new System.Collections.Generic.List<int>();

            for (int ring = 0; ring <= rings; ring++)
            {
                float phi        = Mathf.PI * ring / rings;
                float y          = Mathf.Cos(phi);
                float ringRadius = Mathf.Sin(phi);

                for (int sector = 0; sector <= sectors; sector++)
                {
                    float theta = 2f * Mathf.PI * sector / sectors;
                    float x = ringRadius * Mathf.Cos(theta);
                    float z = ringRadius * Mathf.Sin(theta);

                    Vector3 pos = new Vector3(x, y, z);
                    vertices.Add(pos);
                    normals.Add(pos.normalized);
                    uvs.Add(new Vector2((float)sector / sectors, 1f - (float)ring / rings));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int sector = 0; sector < sectors; sector++)
                {
                    int cur  = ring * (sectors + 1) + sector;
                    int next = cur + sectors + 1;

                    triangles.Add(cur);
                    triangles.Add(next);
                    triangles.Add(cur + 1);

                    triangles.Add(cur + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);

            return mesh;
        }

        // ═══════════════════════════════════════════════════════════════
        // MESH REBUILD HELPER
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Replace the mesh on a MeshFilter.
        /// Cached meshes are NOT destroyed — they're shared.
        /// </summary>
        public static void ReplaceMesh(MeshFilter filter, Mesh newMesh)
        {
            if (filter == null) return;
            var old = filter.sharedMesh;
            filter.sharedMesh = newMesh;
            // Only destroy non-cached meshes
            if (old != null && !_cache.ContainsValue(old))
                Object.Destroy(old);
        }
    }
}
