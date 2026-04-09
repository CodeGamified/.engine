// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;
using System.Collections.Generic;
using CodeGamified.Quality;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Multi-layer atmosphere rendering system.
    /// Creates N concentric low-poly spheres (one per layer), each with subtle alpha
    /// that stack for a realistic limb-brightening effect. Layer count scales with quality.
    ///
    /// Quality mapping:
    ///   Low    → 3 layers (Stratosphere, Mesosphere, Thermosphere)
    ///   Medium → 4 layers (+ Troposphere)
    ///   High+  → all 5 layers
    ///
    /// Parented outside the planet's rotation pivot so atmosphere stays stationary
    /// while the planet rotates — prevents low-poly vertex artifacts.
    /// </summary>
    public class AtmosphereSystem : MonoBehaviour, IQualityResponsive
    {
        // ═══════════════════════════════════════════════════════════════
        // CONSTANTS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Earth equatorial radius in km — used to normalize altitude fractions.</summary>
        const float EARTH_RADIUS_KM = 6371f;

        /// <summary>Base scale offset (fraction of body radius) to prevent z-fighting with surface.</summary>
        const float SURFACE_OFFSET = 0.005f;

        /// <summary>Render queue for atmosphere layers (Transparent + 1, incrementing per layer).</summary>
        const int RENDER_QUEUE_ATMOSPHERE_BASE = 3001;

        [Header("Visual Settings")]
        [Range(0.1f, 5f)]  public float visualExaggeration = 3f;
        [Range(8, 32)]     public int   sphereSegments = 16;
        [Range(0f, 2f)]    public float globalAlphaMultiplier = 1f;
        [Range(1f, 8f)]    public float fresnelPower = 3f;

        [Header("Layer Visibility")]
        public bool showTroposphere  = true;
        public bool showStratosphere = true;
        public bool showMesosphere   = true;
        public bool showThermosphere = true;
        public bool showExosphere    = true;

        // Shader property IDs (cached)
        static readonly int _SunDirID = Shader.PropertyToID("_CelestialSunDir");

        // Internal
        float _bodyRadius;
        readonly List<GameObject> _layerObjects  = new List<GameObject>();
        readonly List<Material>   _layerMaterials = new List<Material>();
        Shader _layerShader;

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Initialize the atmosphere. Call once after creation.
        /// </summary>
        /// <param name="bodyRadius">Radius of the parent body in Unity units.</param>
        public void Initialize(float bodyRadius)
        {
            _bodyRadius = bodyRadius;

            _layerShader = Shader.Find("CodeGamified/CelestialAtmosphereLayer");
            if (_layerShader == null)
                _layerShader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

            AtmosphereLayerDataLoader.EnsureLoaded();
            RebuildLayers();
        }

        /// <summary>Update sun direction for all layer materials via global shader vector — O(1).</summary>
        public void UpdateSunDirection(Vector3 sunDir)
        {
            Shader.SetGlobalVector(_SunDirID, new Vector4(sunDir.x, sunDir.y, sunDir.z, 0));
        }

        /// <summary>Rebuild layers (call after quality or visibility change).</summary>
        public void RebuildLayers()
        {
            ClearLayers();

            var layers = AtmosphereLayerDataLoader.Instance?.Layers;
            if (layers == null || layers.Count == 0) return;

            int baseQueue = RENDER_QUEUE_ATMOSPHERE_BASE;
            var tier = QualityBridge.CurrentTier;

            for (int i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                if (!IsLayerVisible(layer.LayerName)) continue;
                if (!IsLayerVisibleForQuality(layer.LayerName, tier)) continue;

                CreateLayerSphere(layer, baseQueue + i);
            }
        }

        public void SetVisible(bool visible)
        {
            foreach (var obj in _layerObjects)
                if (obj != null) obj.SetActive(visible);
        }

        public void SetGlobalAlpha(float alpha)
        {
            globalAlphaMultiplier = alpha;
            foreach (var mat in _layerMaterials)
                if (mat != null) mat.SetFloat("_AlphaMultiplier", alpha);
        }

        // ═══════════════════════════════════════════════════════════════
        // IQualityResponsive
        // ═══════════════════════════════════════════════════════════════

        void OnEnable()  => QualityBridge.Register(this);
        void OnDisable() => QualityBridge.Unregister(this);

        public void OnQualityChanged(QualityTier tier)
        {
            sphereSegments = QualityHints.SphereSegments(tier, DetailRole.Effect);
            if (_bodyRadius > 0) RebuildLayers();
        }

        // ═══════════════════════════════════════════════════════════════
        // LAYER CREATION
        // ═══════════════════════════════════════════════════════════════

        void CreateLayerSphere(AtmosphereLayerData layer, int renderQueue)
        {
            float outerScale = GetScale(layer.AltitudeMaxKm, layer.VisualScale);
            float innerScale = GetScale(layer.AltitudeMinKm, layer.VisualScale);

            var go = new GameObject($"Atmosphere_{layer.LayerName}");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one * outerScale;

            var mf = go.AddComponent<MeshFilter>();
            mf.mesh = CelestialMeshUtility.CreateLowPolySphere(sphereSegments, $"Atmo_{layer.LayerName}");

            var mr = go.AddComponent<MeshRenderer>();

            var mat = new Material(_layerShader);
            Color c = layer.BaseColor;
            c.a = layer.AlphaBase;
            mat.SetColor("_Color", c);
            mat.SetFloat("_AlphaMultiplier", globalAlphaMultiplier);
            mat.SetFloat("_FresnelPower", fresnelPower);
            mat.SetFloat("_InnerRadius", innerScale / outerScale);
            mat.SetFloat("_OuterRadius", 1f);

            mat.renderQueue = renderQueue;

            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _layerObjects.Add(go);
            _layerMaterials.Add(mat);
        }

        float GetScale(float altitudeKm, float layerVisualScale)
        {
            float realScale = altitudeKm / EARTH_RADIUS_KM;
            float exaggerated = realScale * layerVisualScale * visualExaggeration;
            return _bodyRadius * (1f + SURFACE_OFFSET + exaggerated);
        }

        // ═══════════════════════════════════════════════════════════════
        // VISIBILITY LOGIC — indexed lookup, no string matching per frame
        // ═══════════════════════════════════════════════════════════════

        // Layer indices: Troposphere=0, Stratosphere=1, Mesosphere=2, Thermosphere=3, Exosphere=4
        static int LayerIndex(string name) => name switch
        {
            "Troposphere"  => 0,
            "Stratosphere" => 1,
            "Mesosphere"   => 2,
            "Thermosphere" => 3,
            "Exosphere"    => 4,
            _ => -1
        };

        bool IsLayerVisible(string name)
        {
            int idx = LayerIndex(name);
            return idx switch
            {
                0 => showTroposphere,
                1 => showStratosphere,
                2 => showMesosphere,
                3 => showThermosphere,
                4 => showExosphere,
                _ => true
            };
        }

        // Packed bitmask: which layer indices are visible at each quality tier
        // Low=0b01110 (1,2,3), Medium=0b01111 (0,1,2,3), High+=0b11111 (all)
        const int QUALITY_MASK_LOW    = 0b01110;
        const int QUALITY_MASK_MEDIUM = 0b01111;
        const int QUALITY_MASK_HIGH   = 0b11111;

        static bool IsLayerVisibleForQuality(string name, QualityTier tier)
        {
            int idx = LayerIndex(name);
            if (idx < 0) return true;
            int mask = tier switch
            {
                QualityTier.Low    => QUALITY_MASK_LOW,
                QualityTier.Medium => QUALITY_MASK_MEDIUM,
                _                  => QUALITY_MASK_HIGH
            };
            return (mask & (1 << idx)) != 0;
        }

        public static int GetLayerCountForQuality(QualityTier tier) => tier switch
        {
            QualityTier.Low    => 3,
            QualityTier.Medium => 4,
            _                  => 5
        };

        // ═══════════════════════════════════════════════════════════════
        // CLEANUP
        // ═══════════════════════════════════════════════════════════════

        public void ClearLayers()
        {
            foreach (var obj in _layerObjects)
            {
                if (obj == null) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(obj); else
#endif
                Destroy(obj);
            }
            _layerObjects.Clear();
            _layerMaterials.Clear();
        }

        void OnDestroy() => ClearLayers();
    }
}
