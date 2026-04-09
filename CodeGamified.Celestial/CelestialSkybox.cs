// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;
using UnityEngine.Rendering;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Space skybox using an inverted sphere with dual-texture system:
    ///   - Base texture (milky way / starfield)
    ///   - Emissive texture (star points with power-curve contrast boost)
    ///
    /// Camera-following: skybox sphere repositions to camera each LateUpdate
    /// so it always appears at infinite distance. Render queue = Background.
    /// </summary>
    public class CelestialSkybox : MonoBehaviour
    {
        [Header("Textures")]
        [Tooltip("Base starfield (milky way). Auto-loads 8k_stars_milky_way from Resources.")]
        public Texture2D baseTexture;

        [Tooltip("Stars-only emissive overlay. Auto-loads 8k_stars from Resources.")]
        public Texture2D emissiveTexture;

        [Header("Brightness")]
        [Range(0.1f, 10f)]  public float baseBrightness = 1.5f;
        [Range(0f, 50f)]    public float starEmissiveBrightness = 8.0f;
        [Range(0.5f, 4f)]   public float emissivePower = 1.5f;

        [Header("Geometry")]
        public float skyboxRadius = 10000f;
        public Vector3 rotationOffset = Vector3.zero;

        [Range(12, 48)]
        public int sphereSegments = 24;

        // Runtime
        GameObject _sphereGO;
        Material _material;
        Camera _camera;

        void Start()
        {
            _camera = Camera.main;
            if (_camera == null) _camera = FindObjectOfType<Camera>();

            LoadTextures();
            CreateSphere();
        }

        void LateUpdate()
        {
            if (_sphereGO != null && _camera != null)
                _sphereGO.transform.position = _camera.transform.position;
        }

        void LoadTextures()
        {
            if (baseTexture == null)
                baseTexture = Resources.Load<Texture2D>("8k_stars_milky_way");
            if (emissiveTexture == null)
                emissiveTexture = Resources.Load<Texture2D>("8k_stars");
        }

        void CreateSphere()
        {
            _sphereGO = new GameObject("SkyboxMesh");
            _sphereGO.transform.position = _camera != null ? _camera.transform.position : Vector3.zero;
            _sphereGO.transform.rotation = Quaternion.Euler(rotationOffset);
            _sphereGO.transform.localScale = Vector3.one * skyboxRadius * 2f;

            var mf = _sphereGO.AddComponent<MeshFilter>();
            mf.mesh = CelestialMeshUtility.CreateInvertedSphere(sphereSegments, "Skybox");
            var mr = _sphereGO.AddComponent<MeshRenderer>();

            var shader = Shader.Find("CodeGamified/CelestialSkybox");
            if (shader == null) shader = Shader.Find("HDRP/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            _material = new Material(shader);

            if (baseTexture != null)
            {
                _material.SetTexture("_BaseMap", baseTexture);
                _material.SetTexture("_MainTex", baseTexture);
            }
            if (emissiveTexture != null)
                _material.SetTexture("_EmissiveMap", emissiveTexture);

            _material.SetFloat("_BaseBrightness", baseBrightness);
            _material.SetFloat("_EmissiveBrightness", starEmissiveBrightness);
            _material.SetFloat("_EmissivePower", emissivePower);
            _material.renderQueue = (int)RenderQueue.Background;

            mr.material = _material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
    }
}
