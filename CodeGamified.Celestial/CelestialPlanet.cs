// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;
using System;
using CodeGamified.Quality;
using CodeGamified.Time;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Renders an Earth-like planet with:
    ///   - Day/night cycle driven by SimulationTime
    ///   - Normal mapping, specular oceans, fresnel rim
    ///   - Optional cloud layer with weather cycling
    ///   - Multi-layer atmosphere via <see cref="AtmosphereSystem"/>
    ///   - Moonlight on night side (requires <see cref="CelestialMoon"/> reference)
    ///   - Quality-responsive mesh segments and texture resolution
    ///
    /// Architecture:
    ///   RotationPivot (rotates)
    ///     └─ PlanetMesh (scaled, holds material)
    ///         └─ CloudLayer (optional, slight altitude offset)
    ///   AtmosphereSystem (stationary, outside pivot)
    /// </summary>
    public class CelestialPlanet : MonoBehaviour, IQualityResponsive
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        [Header("Planet")]
        [Tooltip("Radius in Unity units (Earth default: 6.371 = 6371 km)")]
        public float radius = 6.371f;

        [Range(32, 128)]
        public int sphereSegments = 96;

        [Header("Rotation")]
        [Tooltip("Rotation period in seconds (Earth: 86400)")]
        public double rotationPeriodSeconds = 86400.0;

        [Tooltip("Rotation offset in degrees so that at t=0 the correct face faces the sun")]
        public float initialRotationOffset = 180f;

        [Header("Sun Direction")]
        [Tooltip("Fixed world-space direction TO the sun")]
        public Vector3 sunDirection = Vector3.right;

        [Header("Textures (auto-loaded from Resources if null)")]
        public Texture2D dayTexture;
        public Texture2D nightTexture;
        public Texture2D cloudTexture;
        public Texture2D normalMap;
        public Texture2D specularMap;

        [Header("Material")]
        [Range(0, 2)]   public float normalStrength = 1.0f;
        [Range(0, 2)]   public float specularIntensity = 1.0f;
        [Range(1, 128)]  public float specularPower = 8f;
        public Color oceanSpecularColor = Color.white;
        [Range(1, 10)]  public float fresnelPower = 5f;
        [Range(0, 1)]   public float fresnelIntensity = 0.3f;
        [Range(0, 2)]   public float nightBrightness = 3f;
        [Range(0.5f, 2f)] public float dayBrightness = 1f;
        [Range(1, 20)]  public float terminatorSharpness = 8f;
        [Range(0, 0.3f)] public float ambientLight = 0.02f;

        [Header("Cloud Layer")]
        public bool showClouds = false;
        [Range(0, 1)]  public float cloudOpacity = 0.75f;
        public float cloudAltitude = 0.005f;
        public float cloudRotationSpeed = 0.001f;
        [Range(0, 2)]  public float cloudDayBrightness = 0.75f;
        [Range(0, 0.5f)] public float cloudNightBrightness = 0.25f;
        [Range(1, 20)] public float cloudTerminatorSharpness = 6f;

        [Header("Weather Cycle")]
        public bool enableLiveClouds = true;
        public float hoursPerCloudTexture = 0.25f;
        public string cloudAlphaResourcePath = "CloudsAlpha";
        public string specularResourcePath = "Specular";
        public bool cycleSpecular = true;

        [Header("Atmosphere")]
        public bool showAtmosphere = true;
        public bool useLayeredAtmosphere = true;
        public Color singleAtmosphereColor = new Color(0.4f, 0.7f, 1.0f, 0.2f);
        public float atmosphereThickness = 0.05f;
        [Range(16, 96)] public int singleAtmosphereSegments = 48;
        [Range(0.1f, 5f)] public float layerVisualExaggeration = 3f;
        [Range(8, 32)]    public int layerSphereSegments = 24;
        [Range(0f, 2f)]   public float layerGlobalAlpha = 1f;
        [Range(1f, 8f)]   public float layerFresnelPower = 3f;

        [Header("Moonlight")]
        public bool enableMoonlight = true;
        [Range(0f, 0.5f)] public float moonlightIntensity = 0.1f;
        public Color moonlightColor = new Color(0.7f, 0.8f, 1.0f, 1.0f);

        [Header("References")]
        [Tooltip("Moon orbiting this planet (for moonlight). Null = no moonlight.")]
        public CelestialMoon moon;

        /// <summary>
        /// Factory delegate for creating the atmosphere system component.
        /// Games can substitute a custom subclass:
        ///   planet.AtmosphereFactory = go => go.AddComponent&lt;MyCustomAtmosphere&gt;();
        /// </summary>
        [NonSerialized]
        public Func<GameObject, AtmosphereSystem> AtmosphereFactory;

        // ═══════════════════════════════════════════════════════════════
        // RUNTIME STATE
        // ═══════════════════════════════════════════════════════════════

        GameObject _pivot;
        GameObject _meshGO;
        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Material _material;

        GameObject _cloudGO;
        MeshRenderer _cloudRenderer;
        Material _cloudMaterial;

        GameObject _singleAtmoGO;
        Material _singleAtmoMaterial;

        AtmosphereSystem _atmosphereSystem;

        // Weather cycle
        Texture2D[] _cloudAlphaTextures;
        Texture2D[] _specularTextures;
        int _weatherIndex;
        double _lastCloudChangeHour = -1;
        // Streaming: only resource names loaded eagerly, textures loaded on-demand (#10)
        string[] _cloudAlphaNames;
        string[] _specularNames;

        // Cached per-frame
        Vector3 _sunDirNorm;
        // Delta-accumulation rotation (avoids double-precision modulo drift at high t_sim)
        float _accumulatedRotation;

        // Render queue constants
        const int RENDER_QUEUE_TRANSPARENT = 3000;
        const int RENDER_QUEUE_ATMOSPHERE_BASE = 3001;

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Get the rotation pivot Transform (rotates with time).</summary>
        public Transform Pivot => _pivot != null ? _pivot.transform : transform;

        /// <summary>Get the mesh GameObject (holds renderer + collider).</summary>
        public GameObject MeshObject => _meshGO;

        /// <summary>Current rotation degrees around Y axis.</summary>
        public float GetRotationDegrees() => _accumulatedRotation + initialRotationOffset;

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        void Start()
        {
            LoadTextures();
            CreatePlanetMesh();
            CreatePlanetMaterial();
            CreateAtmosphere();

            if (showClouds)
            {
                LoadWeatherTextures();
                CreateCloudLayer();
            }
        }

        void Update()
        {
            // Cache once per frame — used by all sub-updates
            _sunDirNorm = sunDirection.normalized;

            UpdateRotation();
            UpdateShaderParams();
            UpdateCloudDrift();
            UpdateMoonlight();
            UpdateAtmosphere();
            UpdateWeatherCycle();
        }

        // ═══════════════════════════════════════════════════════════════
        // IQualityResponsive
        // ═══════════════════════════════════════════════════════════════

        void OnEnable()  => QualityBridge.Register(this);
        void OnDisable() => QualityBridge.Unregister(this);

        public void OnQualityChanged(QualityTier tier)
        {
            // Mesh
            int segs = QualityHints.SphereSegments(tier, DetailRole.Primary);
            SetSphereQuality(segs);

            // Texture mipmap
            int texLimit = 3 - (int)tier;
            QualitySettings.globalTextureMipmapLimit = texLimit;
            float bias = tier switch
            {
                QualityTier.Low    => 3.0f,
                QualityTier.Medium => 2.0f,
                QualityTier.High   => 1.0f,
                _                  => 0.0f
            };
            ApplyMipmapBias(bias);
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATION
        // ═══════════════════════════════════════════════════════════════

        void LoadTextures()
        {
            if (dayTexture == null)     dayTexture     = Resources.Load<Texture2D>("8k_earth_daymap");
            if (nightTexture == null)   nightTexture   = Resources.Load<Texture2D>("8k_earth_nightmap");
            if (normalMap == null)      normalMap      = Resources.Load<Texture2D>("8k_earth_normal_map");
            if (specularMap == null)    specularMap    = Resources.Load<Texture2D>("8k_earth_specular_map");
            if (!enableLiveClouds && cloudTexture == null)
                cloudTexture = Resources.Load<Texture2D>("8k_earth_clouds");
        }

        void CreatePlanetMesh()
        {
            // Pivot for rotation (no scale)
            _pivot = new GameObject("RotationPivot");
            _pivot.transform.SetParent(transform);
            _pivot.transform.localPosition = Vector3.zero;
            _pivot.transform.localRotation = Quaternion.identity;
            _pivot.transform.localScale    = Vector3.one;

            // Mesh object (scaled to radius)
            _meshGO = new GameObject("PlanetMesh");
            _meshGO.transform.SetParent(_pivot.transform);
            _meshGO.transform.localPosition = Vector3.zero;
            _meshGO.transform.localRotation = Quaternion.identity;
            _meshGO.transform.localScale    = Vector3.one * radius * 2f;

            _meshFilter   = _meshGO.AddComponent<MeshFilter>();
            _meshFilter.mesh = CelestialMeshUtility.CreateUVSphere(sphereSegments, "Planet");
            _meshRenderer = _meshGO.AddComponent<MeshRenderer>();

            var col = _meshGO.AddComponent<SphereCollider>();
            col.radius = 0.5f;
        }

        void CreatePlanetMaterial()
        {
            var shader = Shader.Find("CodeGamified/CelestialDayNight");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            _material = new Material(shader);

            if (dayTexture != null)
            {
                _material.SetTexture("_DayTex", dayTexture);
                _material.SetTexture("_MainTex", dayTexture);
            }
            if (nightTexture != null)  _material.SetTexture("_NightTex", nightTexture);
            if (normalMap != null)
            {
                _material.SetTexture("_NormalMap", normalMap);
                _material.SetFloat("_NormalStrength", normalStrength);
            }
            if (specularMap != null)
            {
                _material.SetTexture("_SpecularMap", specularMap);
                _material.SetFloat("_SpecularIntensity", specularIntensity);
                _material.SetFloat("_SpecularPower", specularPower);
                _material.SetColor("_OceanSpecularColor", oceanSpecularColor);
            }

            _material.SetFloat("_FresnelPower", fresnelPower);
            _material.SetFloat("_FresnelIntensity", fresnelIntensity);
            _material.SetVector("_SunDir", (Vector4)(Vector3)sunDirection.normalized);
            _material.SetFloat("_TerminatorSharpness", terminatorSharpness);
            _material.SetFloat("_NightBrightness", nightBrightness);
            _material.SetFloat("_DayBrightness", dayBrightness);
            _material.SetFloat("_AmbientLight", ambientLight);

            _meshRenderer.material = _material;
            _meshRenderer.receiveShadows = false;
        }

        void CreateCloudLayer()
        {
            _cloudGO = new GameObject("CloudLayer");
            _cloudGO.transform.SetParent(_meshGO.transform);
            _cloudGO.transform.localPosition = Vector3.zero;
            _cloudGO.transform.localRotation = Quaternion.identity;
            _cloudGO.transform.localScale    = Vector3.one * (1f + cloudAltitude);

            var mf = _cloudGO.AddComponent<MeshFilter>();
            mf.mesh = CelestialMeshUtility.CreateUVSphere(sphereSegments, "Clouds");
            _cloudRenderer = _cloudGO.AddComponent<MeshRenderer>();

            var shader = Shader.Find("CodeGamified/CelestialCloudLayer");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            _cloudMaterial = new Material(shader);
            _cloudMaterial.SetTexture("_MainTex", cloudTexture);
            _cloudMaterial.SetFloat("_CloudOpacity", cloudOpacity);
            _cloudMaterial.SetFloat("_DayBrightness", cloudDayBrightness);
            _cloudMaterial.SetFloat("_NightBrightness", cloudNightBrightness);
            _cloudMaterial.SetFloat("_TerminatorSharpness", cloudTerminatorSharpness);
            _cloudMaterial.SetVector("_SunDir", (Vector4)(Vector3)sunDirection.normalized);
            _cloudMaterial.renderQueue = RENDER_QUEUE_TRANSPARENT;

            _cloudRenderer.material = _cloudMaterial;
            _cloudRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _cloudRenderer.receiveShadows = false;
        }

        void CreateAtmosphere()
        {
            CreateSingleLayerAtmosphere();

            if (useLayeredAtmosphere)
                CreateLayeredAtmosphere();
        }

        void CreateSingleLayerAtmosphere()
        {
            _singleAtmoGO = new GameObject("SingleAtmosphere");
            _singleAtmoGO.transform.SetParent(_meshGO.transform);
            _singleAtmoGO.transform.localPosition = Vector3.zero;
            _singleAtmoGO.transform.localRotation = Quaternion.identity;
            _singleAtmoGO.transform.localScale    = Vector3.one * (1f + atmosphereThickness);

            var mf = _singleAtmoGO.AddComponent<MeshFilter>();
            mf.mesh = CelestialMeshUtility.CreateUVSphere(singleAtmosphereSegments, "SingleAtmo");
            var mr = _singleAtmoGO.AddComponent<MeshRenderer>();

            var shader = Shader.Find("CodeGamified/CelestialAtmosphere");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");

            _singleAtmoMaterial = new Material(shader);
            _singleAtmoMaterial.SetColor("_Color", singleAtmosphereColor);
            _singleAtmoMaterial.SetVector("_SunDir", new Vector4(sunDirection.x, sunDirection.y, sunDirection.z, 0));
            _singleAtmoMaterial.renderQueue = RENDER_QUEUE_TRANSPARENT;

            mr.material = _singleAtmoMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            _singleAtmoGO.SetActive(showAtmosphere && !useLayeredAtmosphere);
        }

        void CreateLayeredAtmosphere()
        {
            // Only create data loader if one doesn't already exist (EnsureLoaded handles singleton)
            if (AtmosphereLayerDataLoader.Instance == null)
            {
                var dataGO = new GameObject("AtmosphereData");
                dataGO.transform.SetParent(transform);
                dataGO.transform.localPosition = Vector3.zero;
                dataGO.AddComponent<AtmosphereLayerDataLoader>();
            }

            var sysGO = new GameObject("AtmosphereSystem");
            sysGO.transform.SetParent(transform);
            sysGO.transform.localPosition = Vector3.zero;
            _atmosphereSystem = AtmosphereFactory != null
                ? AtmosphereFactory(sysGO)
                : sysGO.AddComponent<AtmosphereSystem>();
            _atmosphereSystem.visualExaggeration    = layerVisualExaggeration;
            _atmosphereSystem.sphereSegments         = layerSphereSegments;
            _atmosphereSystem.globalAlphaMultiplier  = layerGlobalAlpha;
            _atmosphereSystem.fresnelPower           = layerFresnelPower;
            _atmosphereSystem.Initialize(radius);
            _atmosphereSystem.SetVisible(showAtmosphere);
        }

        // ═══════════════════════════════════════════════════════════════
        // UPDATE
        // ═══════════════════════════════════════════════════════════════

        void UpdateRotation()
        {
            if (_pivot == null) return;

            // Delta accumulation: avoids modulo on growing simulationTime
            var sim = SimulationTime.Instance;
            if (sim != null && !sim.isPaused)
            {
                float dt = UnityEngine.Time.deltaTime * sim.timeScale;
                float degreesPerSecond = (float)(360.0 / rotationPeriodSeconds);
                _accumulatedRotation = (_accumulatedRotation + degreesPerSecond * dt) % 360f;
            }

            float deg = GetRotationDegrees();
            _pivot.transform.localRotation = Quaternion.Euler(0f, -deg, 0f);
        }

        void UpdateShaderParams()
        {
            if (_material == null) return;
            Vector4 sd = new Vector4(_sunDirNorm.x, _sunDirNorm.y, _sunDirNorm.z, 0);
            _material.SetVector("_SunDir", sd);
        }

        void UpdateCloudDrift()
        {
            if (_cloudGO == null) return;
            _cloudGO.transform.Rotate(Vector3.up, cloudRotationSpeed * UnityEngine.Time.deltaTime, Space.Self);
            if (_cloudMaterial != null)
                _cloudMaterial.SetVector("_SunDir", new Vector4(_sunDirNorm.x, _sunDirNorm.y, _sunDirNorm.z, 0));
        }

        void UpdateMoonlight()
        {
            if (_material == null) return;

            if (!enableMoonlight || moon == null)
            {
                _material.SetFloat("_MoonPhase", 0f);
                return;
            }

            Vector3 earthPos = _pivot != null ? _pivot.transform.position : Vector3.zero;
            Vector3 moonDir  = (moon.transform.position - earthPos).normalized;
            float phase = moon.GetIlluminatedFraction();

            _material.SetVector("_MoonDir", new Vector4(moonDir.x, moonDir.y, moonDir.z, 0));
            _material.SetFloat("_MoonPhase", phase);
            _material.SetFloat("_MoonlightIntensity", moonlightIntensity);
            _material.SetColor("_MoonlightColor", moonlightColor);

            // Pass to single atmosphere too
            if (_singleAtmoMaterial != null)
            {
                _singleAtmoMaterial.SetVector("_MoonDir", new Vector4(moonDir.x, moonDir.y, moonDir.z, 0));
                _singleAtmoMaterial.SetFloat("_MoonPhase", phase);
            }
        }

        void UpdateAtmosphere()
        {
            // Atmosphere layers use a global shader vector — single call for all materials
            if (_atmosphereSystem != null)
                _atmosphereSystem.UpdateSunDirection(_sunDirNorm);

            if (_singleAtmoMaterial != null)
                _singleAtmoMaterial.SetVector("_SunDir", new Vector4(_sunDirNorm.x, _sunDirNorm.y, _sunDirNorm.z, 0));
        }

        // ═══════════════════════════════════════════════════════════════
        // WEATHER CYCLING
        // ═══════════════════════════════════════════════════════════════

        void LoadWeatherTextures()
        {
            if (!enableLiveClouds) return;

            // Only load resource NAMES, not all N×8K textures into VRAM (#10)
            var clouds = Resources.LoadAll<Texture2D>(cloudAlphaResourcePath);
            if (clouds != null && clouds.Length > 0)
            {
                System.Array.Sort(clouds, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                _cloudAlphaNames = new string[clouds.Length];
                for (int i = 0; i < clouds.Length; i++)
                    _cloudAlphaNames[i] = clouds[i].name;
                // Keep only index 0 loaded
                cloudTexture = clouds[0];
                _cloudAlphaTextures = new Texture2D[clouds.Length];
                _cloudAlphaTextures[0] = clouds[0];
                // Unload the rest — they'll stream on demand
                for (int i = 1; i < clouds.Length; i++)
                    Resources.UnloadAsset(clouds[i]);
            }
            else
            {
                cloudTexture = Resources.Load<Texture2D>("8k_earth_clouds");
            }

            if (cycleSpecular)
            {
                var specs = Resources.LoadAll<Texture2D>(specularResourcePath);
                if (specs != null && specs.Length > 0)
                {
                    System.Array.Sort(specs, (a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                    _specularNames = new string[specs.Length];
                    for (int i = 0; i < specs.Length; i++)
                        _specularNames[i] = specs[i].name;
                    _specularTextures = new Texture2D[specs.Length];
                    _specularTextures[0] = specs[0];
                    for (int i = 1; i < specs.Length; i++)
                        Resources.UnloadAsset(specs[i]);
                }
            }
        }

        void UpdateWeatherCycle()
        {
            if (!enableLiveClouds || _cloudAlphaNames == null || _cloudAlphaNames.Length <= 1) return;
            if (SimulationTime.Instance == null) return;

            double currentHour = SimulationTime.Instance.simulationTime / 3600.0;
            if (_lastCloudChangeHour < 0) { _lastCloudChangeHour = currentHour; return; }

            if (currentHour - _lastCloudChangeHour >= hoursPerCloudTexture)
            {
                int prevIndex = _weatherIndex;
                _weatherIndex = (_weatherIndex + 1) % _cloudAlphaNames.Length;
                _lastCloudChangeHour = currentHour;

                // Stream on-demand: load new, unload old (#10)
                if (_cloudAlphaTextures[_weatherIndex] == null)
                    _cloudAlphaTextures[_weatherIndex] = Resources.Load<Texture2D>($"{cloudAlphaResourcePath}/{_cloudAlphaNames[_weatherIndex]}");
                cloudTexture = _cloudAlphaTextures[_weatherIndex];

                // Unload texture 2 steps behind (keep current + previous for safety)
                int evictIdx = (prevIndex - 1 + _cloudAlphaNames.Length) % _cloudAlphaNames.Length;
                if (evictIdx != _weatherIndex && _cloudAlphaTextures[evictIdx] != null)
                {
                    Resources.UnloadAsset(_cloudAlphaTextures[evictIdx]);
                    _cloudAlphaTextures[evictIdx] = null;
                }

                if (_cloudMaterial != null)
                    _cloudMaterial.SetTexture("_MainTex", cloudTexture);

                if (cycleSpecular && _specularNames != null && _specularNames.Length > 0)
                {
                    int si = _weatherIndex % _specularNames.Length;
                    if (_specularTextures[si] == null)
                        _specularTextures[si] = Resources.Load<Texture2D>($"{specularResourcePath}/{_specularNames[si]}");
                    specularMap = _specularTextures[si];
                    if (_material != null)
                        _material.SetTexture("_SpecularMap", specularMap);

                    int evictSpec = (si - 2 + _specularNames.Length) % _specularNames.Length;
                    if (evictSpec != si && _specularTextures[evictSpec] != null)
                    {
                        Resources.UnloadAsset(_specularTextures[evictSpec]);
                        _specularTextures[evictSpec] = null;
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // QUALITY HELPERS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Rebuild meshes at a new segment count.</summary>
        public void SetSphereQuality(int segments)
        {
            segments = Mathf.Clamp(segments, 32, 128);
            if (segments == sphereSegments) return;
            sphereSegments = segments;

            if (_meshFilter != null)
                CelestialMeshUtility.ReplaceMesh(_meshFilter, CelestialMeshUtility.CreateUVSphere(segments, "Planet"));

            if (_cloudGO != null)
            {
                var cf = _cloudGO.GetComponent<MeshFilter>();
                if (cf != null) CelestialMeshUtility.ReplaceMesh(cf, CelestialMeshUtility.CreateUVSphere(segments, "Clouds"));
            }

            if (_singleAtmoGO != null)
            {
                var af = _singleAtmoGO.GetComponent<MeshFilter>();
                int atmSeg = Mathf.Max(32, segments / 2);
                if (af != null) CelestialMeshUtility.ReplaceMesh(af, CelestialMeshUtility.CreateUVSphere(atmSeg, "SingleAtmo"));
            }

            if (_atmosphereSystem != null)
            {
                _atmosphereSystem.sphereSegments = Mathf.Clamp(segments / 4, 8, 32);
                _atmosphereSystem.RebuildLayers();
            }
        }

        void ApplyMipmapBias(float bias)
        {
            if (dayTexture != null)   dayTexture.mipMapBias   = bias;
            if (nightTexture != null) nightTexture.mipMapBias = bias;
            if (cloudTexture != null) cloudTexture.mipMapBias = bias;
            if (normalMap != null)    normalMap.mipMapBias    = bias;
            if (specularMap != null)  specularMap.mipMapBias  = bias;

            if (_cloudAlphaTextures != null)
                foreach (var t in _cloudAlphaTextures)
                    if (t != null) t.mipMapBias = bias;

            if (_specularTextures != null)
                foreach (var t in _specularTextures)
                    if (t != null) t.mipMapBias = bias;
        }

        // ═══════════════════════════════════════════════════════════════
        // RUNTIME SETTERS
        // ═══════════════════════════════════════════════════════════════

        public void SetCloudOpacity(float v) { cloudOpacity = Mathf.Clamp01(v); if (_cloudMaterial != null) _cloudMaterial.SetFloat("_CloudOpacity", cloudOpacity); }
        public void SetNightBrightness(float v) { nightBrightness = v; if (_material != null) _material.SetFloat("_NightBrightness", nightBrightness); }
        public void SetDayBrightness(float v) { dayBrightness = Mathf.Clamp(v, 0.5f, 2f); if (_material != null) _material.SetFloat("_DayBrightness", dayBrightness); }
        public void SetTerminatorSharpness(float v) { terminatorSharpness = Mathf.Clamp(v, 1f, 20f); if (_material != null) _material.SetFloat("_TerminatorSharpness", terminatorSharpness); }
        public void SetAmbientLight(float v) { ambientLight = Mathf.Clamp(v, 0f, 0.3f); if (_material != null) _material.SetFloat("_AmbientLight", ambientLight); }
        public void SetNormalStrength(float v) { normalStrength = Mathf.Clamp(v, 0f, 2f); if (_material != null) _material.SetFloat("_NormalStrength", normalStrength); }
        public void SetSpecularIntensity(float v) { specularIntensity = Mathf.Clamp(v, 0f, 2f); if (_material != null) _material.SetFloat("_SpecularIntensity", specularIntensity); }
        public void SetSpecularPower(float v) { specularPower = Mathf.Clamp(v, 1f, 128f); if (_material != null) _material.SetFloat("_SpecularPower", specularPower); }
        public void SetFresnelPower(float p) { fresnelPower = Mathf.Clamp(p, 1f, 10f); if (_material != null) _material.SetFloat("_FresnelPower", fresnelPower); }
        public void SetFresnelIntensity(float i) { fresnelIntensity = Mathf.Clamp01(i); if (_material != null) _material.SetFloat("_FresnelIntensity", fresnelIntensity); }

        /// <summary>Effective texture resolution for a quality tier.</summary>
        public static int TextureResolutionForQuality(QualityTier tier) => QualityHints.TextureResolution(tier);

        /// <summary>Sphere segments for a quality tier.</summary>
        public static int SegmentsForQuality(QualityTier tier) => QualityHints.SphereSegments(tier, DetailRole.Primary);

        // ═══════════════════════════════════════════════════════════════
        // CONFIG STRUCT (cross-pollination from WorldGraph.ArcMapConfig)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply a config struct to all fields. Call before Start() or at runtime.
        /// </summary>
        public void ApplyConfig(CelestialPlanetConfig cfg)
        {
            radius = cfg.Radius;
            sphereSegments = cfg.SphereSegments;
            rotationPeriodSeconds = cfg.RotationPeriodSeconds;
            initialRotationOffset = cfg.InitialRotationOffset;
            sunDirection = cfg.SunDirection;

            normalStrength = cfg.NormalStrength;
            specularIntensity = cfg.SpecularIntensity;
            specularPower = cfg.SpecularPower;
            oceanSpecularColor = cfg.OceanSpecularColor;
            fresnelPower = cfg.FresnelPower;
            fresnelIntensity = cfg.FresnelIntensity;
            nightBrightness = cfg.NightBrightness;
            dayBrightness = cfg.DayBrightness;
            terminatorSharpness = cfg.TerminatorSharpness;
            ambientLight = cfg.AmbientLight;

            showClouds = cfg.ShowClouds;
            cloudOpacity = cfg.CloudOpacity;
            cloudAltitude = cfg.CloudAltitude;
            cloudRotationSpeed = cfg.CloudRotationSpeed;
            cloudDayBrightness = cfg.CloudDayBrightness;
            cloudNightBrightness = cfg.CloudNightBrightness;
            cloudTerminatorSharpness = cfg.CloudTerminatorSharpness;

            enableLiveClouds = cfg.EnableLiveClouds;
            hoursPerCloudTexture = cfg.HoursPerCloudTexture;
            cycleSpecular = cfg.CycleSpecular;

            showAtmosphere = cfg.ShowAtmosphere;
            useLayeredAtmosphere = cfg.UseLayeredAtmosphere;
            singleAtmosphereColor = cfg.SingleAtmosphereColor;
            atmosphereThickness = cfg.AtmosphereThickness;
            singleAtmosphereSegments = cfg.SingleAtmosphereSegments;
            layerVisualExaggeration = cfg.LayerVisualExaggeration;
            layerSphereSegments = cfg.LayerSphereSegments;
            layerGlobalAlpha = cfg.LayerGlobalAlpha;
            layerFresnelPower = cfg.LayerFresnelPower;

            enableMoonlight = cfg.EnableMoonlight;
            moonlightIntensity = cfg.MoonlightIntensity;
            moonlightColor = cfg.MoonlightColor;
        }
    }
}
