// CodeGamified.Effects — Procedural sun lens flare
// MIT License
//
// Screen-space lens flare that tracks a sun transform.
// Pipeline-agnostic (URP/Built-in). No texture dependencies.
// Features: core glow, anamorphic streaks, starburst rays, ghost orbs,
// geometric occlusion by a spherical body (planet/moon).
using UnityEngine;
using CodeGamified.Quality;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Attaches to any GameObject with a sun transform reference.
    /// Creates a camera-facing quad with a procedural flare shader.
    /// Occlusion is geometric (angular separation vs body angular radius).
    /// Quality-responsive: Low disables flare, Medium disables ghosts, High/Ultra full effects.
    /// </summary>
    [ExecuteAlways]
    public class SunFlareEffect : MonoBehaviour, IQualityResponsive
    {
        [Header("Sun")]
        [Tooltip("Transform to track. Uses this.transform if null.")]
        public Transform sunTransform;

        [Header("Occluder (Planet)")]
        [Tooltip("Spherical body that can occlude the sun.")]
        public Transform occluderTransform;
        public float occluderRadius;

        [Header("Moon Occluder (Eclipse)")]
        public Transform moonTransform;
        public float moonRadius;

        [Header("Appearance")]
        public Color flareColor = new Color(1f, 0.95f, 0.8f, 1f);
        [Range(0.1f, 1.5f)] public float flareScreenSize = 0.6f;
        public float flareDistance = 50f;

        [Header("Core & Glow")]
        [Range(0f, 30f)] public float coreIntensity = 8f;
        [Range(0f, 3f)]  public float glowIntensity = 0.8f;
        [Range(1f, 30f)] public float glowFalloff = 12f;

        [Header("Anamorphic Streaks")]
        [Range(0f, 5f)]   public float anamorphicStrength = 1.5f;
        [Range(1f, 100f)] public float anamorphicWidth = 40f;

        [Header("Starburst Rays")]
        [Range(0f, 3f)] public float starburstIntensity = 0.5f;

        [Header("Ghost Flares")]
        [Range(0f, 3f)] public float ghostIntensity = 0.8f;

        [Header("Edge Fade")]
        [Range(0.3f, 0.95f)] public float edgeFadeStart = 0.85f;

        [Header("Intensity")]
        [Tooltip("External multiplier (e.g. moon phase). 1 = full, 0 = hidden.")]
        [Range(0f, 1f)] public float intensityScale = 1f;

        [Tooltip("When true, quality tier changes won't re-enable extras (streaks, ghosts, rays).")]
        public bool haloOnly = false;

        // ═══════════════════════════════════════════════════════
        // INTERNAL
        // ═══════════════════════════════════════════════════════

        private GameObject _quad;
        private MeshRenderer _renderer;
        private Material _mat;
        private Camera _cam;
        private float _visibility = 1f;
        private bool _disabledByQuality;

        private static readonly int FlareColorID = Shader.PropertyToID("_FlareColor");
        private static readonly int CoreIntensityID = Shader.PropertyToID("_CoreIntensity");
        private static readonly int GlowIntensityID = Shader.PropertyToID("_GlowIntensity");
        private static readonly int GlowFalloffID = Shader.PropertyToID("_GlowFalloff");
        private static readonly int AnamorphicStrengthID = Shader.PropertyToID("_AnamorphicStrength");
        private static readonly int AnamorphicWidthID = Shader.PropertyToID("_AnamorphicWidth");
        private static readonly int StarburstIntensityID = Shader.PropertyToID("_StarburstIntensity");
        private static readonly int GhostIntensityID = Shader.PropertyToID("_GhostIntensity");
        private static readonly int VisibilityID = Shader.PropertyToID("_Visibility");
        private static readonly int ViewAngleID = Shader.PropertyToID("_ViewAngle");
        private static readonly int OcclusionAmountID = Shader.PropertyToID("_OcclusionAmount");

        void OnEnable()
        {
            if (sunTransform == null) sunTransform = transform;
            _cam = Camera.main;

            QualityBridge.Register(this);

            var shader = Shader.Find("CodeGamified/SunFlare");
            if (shader == null)
            {
                Debug.LogWarning("[SunFlareEffect] CodeGamified/SunFlare shader not found");
                enabled = false;
                return;
            }

            _mat = new Material(shader);

            _quad = new GameObject("SunFlareQuad");
            _quad.transform.SetParent(null);
            var mf = _quad.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildQuad();
            _renderer = _quad.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _mat;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        void OnDisable() { QualityBridge.Unregister(this); Cleanup(); }
        void OnDestroy() { QualityBridge.Unregister(this); Cleanup(); }

        private void Cleanup()
        {
            if (_quad != null) DestroyImmediate(_quad);
            if (_mat != null) DestroyImmediate(_mat);
            _quad = null; _mat = null;
        }

        public void OnQualityChanged(QualityTier tier)
        {
            _disabledByQuality = tier == QualityTier.Low;

            if (_disabledByQuality && _quad != null)
                _quad.SetActive(false);

            if (haloOnly) return; // moon halo: never enable extras

            // Scale effects by tier
            switch (tier)
            {
                case QualityTier.Low:
                    // Flare completely off
                    break;
                case QualityTier.Medium:
                    // Core + glow only, no ghosts/streaks/rays
                    ghostIntensity = 0f;
                    anamorphicStrength = 0f;
                    starburstIntensity = 0f;
                    break;
                case QualityTier.High:
                    // Core + glow + streaks, no ghosts
                    ghostIntensity = 0f;
                    anamorphicStrength = 1.5f;
                    starburstIntensity = 0.5f;
                    break;
                default: // Ultra
                    // Full effects
                    ghostIntensity = 0.8f;
                    anamorphicStrength = 1.5f;
                    starburstIntensity = 0.5f;
                    break;
            }
        }

        void LateUpdate()
        {
            if (_disabledByQuality) return;
            if (_quad == null || _mat == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null || sunTransform == null) return;

            Vector3 sunPos = sunTransform.position;
            Vector3 toSun = sunPos - _cam.transform.position;

            // Behind camera → hide instantly
            if (Vector3.Dot(toSun.normalized, _cam.transform.forward) < 0f)
            {
                _quad.SetActive(false);
                return;
            }
            _quad.SetActive(true);

            Vector3 vp = _cam.WorldToViewportPoint(sunPos);

            // Edge fade
            float edgeDist = Mathf.Max(
                Mathf.Abs(vp.x - 0.5f) * 2f,
                Mathf.Abs(vp.y - 0.5f) * 2f);
            float edgeFade = 1f - Mathf.InverseLerp(edgeFadeStart, 1f, edgeDist);

            // Occlusion (geometric angular separation)
            float occlusion = ComputeOcclusion(sunPos, occluderTransform, occluderRadius);
            if (moonTransform != null)
                occlusion = Mathf.Max(occlusion, ComputeOcclusion(sunPos, moonTransform, moonRadius));

            _visibility = edgeFade * (1f - occlusion) * intensityScale;

            // Position quad in screen-space
            Vector3 sp = _cam.WorldToScreenPoint(sunPos);
            Vector3 worldPos = _cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, flareDistance));
            _quad.transform.position = worldPos;
            _quad.transform.rotation = _cam.transform.rotation;

            float screenH = 2f * flareDistance * Mathf.Tan(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            _quad.transform.localScale = Vector3.one * screenH * flareScreenSize;

            // View angle for ray rotation
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float viewAngle = Mathf.Atan2(sp.y - cy, sp.x - cx);

            // Set material
            _mat.SetColor(FlareColorID, flareColor);
            _mat.SetFloat(CoreIntensityID, coreIntensity);
            _mat.SetFloat(GlowIntensityID, glowIntensity);
            _mat.SetFloat(GlowFalloffID, glowFalloff);
            _mat.SetFloat(AnamorphicStrengthID, anamorphicStrength);
            _mat.SetFloat(AnamorphicWidthID, anamorphicWidth);
            _mat.SetFloat(StarburstIntensityID, starburstIntensity);
            _mat.SetFloat(GhostIntensityID, ghostIntensity);
            _mat.SetFloat(VisibilityID, _visibility);
            _mat.SetFloat(ViewAngleID, viewAngle);
            _mat.SetFloat(OcclusionAmountID, occlusion);
            _mat.SetFloat(Shader.PropertyToID("_GlintIntensity"), 0.3f);
            _mat.SetFloat(Shader.PropertyToID("_Time2"), UnityEngine.Time.time);
        }

        private float ComputeOcclusion(Vector3 sunPos, Transform body, float bodyRadius)
        {
            if (body == null || bodyRadius <= 0f) return 0f;

            Vector3 camPos = _cam.transform.position;
            float distToBody = Vector3.Distance(camPos, body.position);
            float distToSun = Vector3.Distance(camPos, sunPos);
            if (distToBody > distToSun) return 0f; // body behind sun

            // Angular radius of body as seen from camera
            float bodyAngular = Mathf.Atan2(bodyRadius, distToBody) * Mathf.Rad2Deg;
            // Angular separation between sun and body center
            Vector3 toBody = (body.position - camPos).normalized;
            Vector3 toSunDir = (sunPos - camPos).normalized;
            float sep = Mathf.Acos(Mathf.Clamp(Vector3.Dot(toBody, toSunDir), -1f, 1f)) * Mathf.Rad2Deg;

            // 0 when separated, 1 when fully overlapping
            return Mathf.Clamp01(1f - sep / Mathf.Max(bodyAngular, 0.01f));
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "FlareQuad" };
            m.vertices = new[] {
                new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
            m.uv = new[] {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateNormals();
            return m;
        }
    }
}
