// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Emissive sun sphere positioned at a fixed direction from the scene origin.
    /// Creates a visually correct sun disc based on angular diameter and distance.
    /// Optionally creates a directional light (pipeline-agnostic base settings only;
    /// games wire HDRP/URP-specific configuration themselves).
    /// </summary>
    public class CelestialSun : MonoBehaviour
    {
        [Header("Position")]
        [Tooltip("Direction from scene origin to sun (normalized internally)")]
        public Vector3 sunDirection = Vector3.right;

        [Tooltip("Distance from origin in Unity units (visual only, not to scale)")]
        public float distance = 1920f;

        [Header("Appearance")]
        [Tooltip("Angular diameter as seen from origin (real sun ~0.53°)")]
        [Range(0.1f, 5f)]
        public float angularDiameter = 0.53f;

        [Tooltip("Color temperature in Kelvin (real sun ~5778 K)")]
        [Range(4000f, 10000f)]
        public float colorTemperature = 5778f;

        [Tooltip("Emissive intensity for HDR bloom")]
        [Range(1f, 50f)]
        public float emissiveIntensity = 10f;

        [Range(16, 48)]
        public int sphereSegments = 32;

        [Header("Texture")]
        [Tooltip("Sun surface texture (auto-loaded from Resources/8k_sun if null)")]
        public Texture2D sunTexture;

        [Header("Light")]
        [Tooltip("Create a basic directional light aimed at the origin")]
        public bool createDirectionalLight = false;

        [Tooltip("Light intensity (non-physical units)")]
        public float lightIntensity = 3f;

        // ═══════════════════════════════════════════════════════════════
        // RUNTIME
        // ═══════════════════════════════════════════════════════════════

        GameObject _sphereGO;
        MeshRenderer _renderer;
        Material _material;

        /// <summary>The directional light, if created. Games can configure HDRP settings on this.</summary>
        public Light DirectionalLight { get; private set; }

        /// <summary>World-space position of the sun sphere.</summary>
        public Vector3 WorldPosition => sunDirection.normalized * distance;

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        void Start()
        {
            if (sunTexture == null)
                sunTexture = Resources.Load<Texture2D>("8k_sun");

            transform.position = WorldPosition;

            CreateSphere();

            if (createDirectionalLight)
                CreateLight();
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATION
        // ═══════════════════════════════════════════════════════════════

        void CreateSphere()
        {
            float angularRadiusRad = angularDiameter * Mathf.Deg2Rad * 0.5f;
            float sunRadius = distance * Mathf.Tan(angularRadiusRad);

            _sphereGO = new GameObject("SunSphere");
            _sphereGO.transform.SetParent(transform);
            _sphereGO.transform.localPosition = Vector3.zero;
            _sphereGO.transform.localScale    = Vector3.one * sunRadius * 2f;

            var mf = _sphereGO.AddComponent<MeshFilter>();
            mf.mesh = CelestialMeshUtility.CreateUVSphere(sphereSegments, "Sun");
            _renderer = _sphereGO.AddComponent<MeshRenderer>();

            _sphereGO.AddComponent<SphereCollider>().radius = 0.5f;

            // Emissive unlit material (pipeline-agnostic fallback chain)
            var shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            _material = new Material(shader);

            Color sunColor = Mathf.CorrelatedColorTemperatureToRGB(colorTemperature);

            if (sunTexture != null)
            {
                _material.SetTexture("_UnlitColorMap", sunTexture);
                _material.SetTexture("_MainTex", sunTexture);
            }

            _material.SetColor("_UnlitColor", sunColor * emissiveIntensity);
            _material.SetColor("_Color", sunColor * emissiveIntensity);
            _material.SetColor("_EmissiveColor", sunColor * emissiveIntensity);
            _material.EnableKeyword("_EMISSION");

            _renderer.material = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
        }

        void CreateLight()
        {
            var lightGO = new GameObject("SunLight");
            lightGO.transform.SetParent(transform);
            lightGO.transform.localPosition = Vector3.zero;
            lightGO.transform.rotation = Quaternion.LookRotation(-sunDirection.normalized);

            DirectionalLight = lightGO.AddComponent<Light>();
            DirectionalLight.type = LightType.Directional;
            DirectionalLight.intensity = lightIntensity;
            DirectionalLight.useColorTemperature = true;
            DirectionalLight.colorTemperature = colorTemperature;
            DirectionalLight.color = Color.white;
            DirectionalLight.shadows = LightShadows.None;
        }

        // ═══════════════════════════════════════════════════════════════
        // RUNTIME SETTERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Update color temperature and refresh all related visuals.</summary>
        public void SetColorTemperature(float kelvin)
        {
            colorTemperature = Mathf.Clamp(kelvin, 4000f, 10000f);
            Color c = Mathf.CorrelatedColorTemperatureToRGB(colorTemperature);

            if (_material != null)
            {
                _material.SetColor("_UnlitColor", c * emissiveIntensity);
                _material.SetColor("_Color", c * emissiveIntensity);
                _material.SetColor("_EmissiveColor", c * emissiveIntensity);
            }
            if (DirectionalLight != null)
                DirectionalLight.colorTemperature = colorTemperature;
        }
    }
}
