// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;
using CodeGamified.Quality;
using CodeGamified.Time;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Moon orbiting a <see cref="CelestialPlanet"/> with:
    ///   - Realistic orbital mechanics (configurable period, inclination)
    ///   - Phase illumination via custom shader
    ///   - Earthshine (planet-reflected light on dark side)
    ///   - Crater normal mapping
    ///   - Tidal locking (same face always toward planet)
    ///   - Quality-responsive mesh resolution
    /// </summary>
    public class CelestialMoon : MonoBehaviour, IQualityResponsive
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        [Header("Moon")]
        [Tooltip("Moon radius in Unity units (Earth's Moon: ~0.273 Earth radii)")]
        public float moonRadius = 0.273f;

        [Range(32, 96)]
        public int sphereSegments = 64;

        [Header("Orbit")]
        [Tooltip("Distance from planet center in Unity units")]
        public float orbitalDistance = 10f;

        [Tooltip("Sidereal orbital period in days (Earth's Moon: ~27.3)")]
        public float orbitalPeriodDays = 27.3217f;

        [Tooltip("Orbital inclination in degrees (Earth's Moon: ~5.145°)")]
        public float orbitalInclination = 5.145f;

        [Tooltip("Initial orbital phase 0-360° (0=new moon position near sun)")]
        public float initialPhase = 180f;

        [Header("Textures")]
        public Texture2D moonTexture;
        public Texture2D normalMap;

        [Header("Shader")]
        [Range(0.5f, 2f)]   public float dayBrightness = 0.8f;
        [Range(1, 20)]      public float terminatorSharpness = 6f;
        [Range(0, 0.1f)]    public float ambientLight = 0.01f;
        [Range(0, 2f)]      public float normalStrength = 1.0f;

        [Header("Earthshine")]
        [Range(0, 1)]       public float earthshineIntensity = 0.1f;
        public Color earthshineColor = new Color(0.4f, 0.5f, 0.7f, 1f);

        [Header("Surface")]
        [Range(0, 1)]       public float smoothness = 0.05f;
        public Color surfaceTint = new Color(0.85f, 0.85f, 0.85f, 1f);

        [Header("Reference")]
        [Tooltip("Planet this moon orbits. Required.")]
        public CelestialPlanet planet;

        [Header("Sun Direction")]
        [Tooltip("Shared sun direction (should match planet's setting)")]
        public Vector3 sunDirection = Vector3.right;

        // ═══════════════════════════════════════════════════════════════
        // RUNTIME
        // ═══════════════════════════════════════════════════════════════

        GameObject _meshGO;
        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        Material _material;
        float _currentAngle;

        // Dirty tracking for shader params (#6)
        float _prevDayBright, _prevTermSharp, _prevAmbient, _prevNormStr;
        float _prevEarthshine, _prevSmooth;
        Color _prevEarthshineCol, _prevSurfaceTint;

        /// <summary>
        /// Simulation seconds per game-day. Must match the game's SimulationTime day length.
        /// Default 86400 = real-world; SeaRauber overrides to 120.
        /// </summary>
        public float secondsPerDay = 86400f;

        // Phase name thresholds (illuminated fraction boundaries)
        const float PHASE_NEW  = 0.02f;  // < this = New Moon
        const float PHASE_FULL = 0.98f;  // > this = Full Moon
        const float PHASE_QUARTER_LO = 0.45f;  // < this = Crescent
        const float PHASE_QUARTER_HI = 0.55f;  // < this = Quarter, else Gibbous

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Illuminated fraction as seen from the planet (0 = new, 1 = full).
        /// </summary>
        public float GetIlluminatedFraction()
        {
            Vector3 planetPos = planet != null ? planet.transform.position : Vector3.zero;
            Vector3 moonToSun   = sunDirection.normalized;
            Vector3 moonToEarth = (planetPos - transform.position).normalized;
            float alignment = Vector3.Dot(moonToSun, moonToEarth);
            return (alignment + 1f) * 0.5f;
        }

        /// <summary>Current lunar phase 0-1 (same as illuminated fraction).</summary>
        public float GetLunarPhase() => GetIlluminatedFraction();

        /// <summary>Human-readable phase name.</summary>
        public string GetLunarPhaseName()
        {
            float illum = GetIlluminatedFraction();
            bool waxing = _currentAngle <= 180f;

            if (illum < PHASE_NEW) return "New Moon";
            if (illum > PHASE_FULL) return "Full Moon";
            if (illum < PHASE_QUARTER_LO) return waxing ? "Waxing Crescent" : "Waning Crescent";
            if (illum < PHASE_QUARTER_HI) return waxing ? "First Quarter"   : "Last Quarter";
            return waxing ? "Waxing Gibbous" : "Waning Gibbous";
        }

        /// <summary>Set orbital phase directly (0=new, 0.5=full).</summary>
        public void SetLunarPhase(float phase)
        {
            _currentAngle = phase * 360f;
            initialPhase = _currentAngle;
            UpdateOrbitalPosition();
        }

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        void Start()
        {
            LoadTextures();
            CreateMesh();
            CreateMaterial();

            _currentAngle = initialPhase;
            UpdateOrbitalPosition();
        }

        void Update()
        {
            UpdateOrbit();
            UpdateShaderParams();
        }

        // ═══════════════════════════════════════════════════════════════
        // IQualityResponsive
        // ═══════════════════════════════════════════════════════════════

        void OnEnable()  => QualityBridge.Register(this);
        void OnDisable() => QualityBridge.Unregister(this);

        public void OnQualityChanged(QualityTier tier)
        {
            int segs = QualityHints.SphereSegments(tier, DetailRole.Secondary);
            SetSphereQuality(segs);
        }

        // ═══════════════════════════════════════════════════════════════
        // CREATION
        // ═══════════════════════════════════════════════════════════════

        void LoadTextures()
        {
            if (moonTexture == null) moonTexture = Resources.Load<Texture2D>("8k_moon");
            if (normalMap == null)   normalMap   = Resources.Load<Texture2D>("8k_moon_normal");
        }

        void CreateMesh()
        {
            _meshGO = new GameObject("MoonMesh");
            _meshGO.transform.SetParent(transform);
            _meshGO.transform.localPosition = Vector3.zero;
            _meshGO.transform.localScale    = Vector3.one * moonRadius * 2f;

            _meshFilter   = _meshGO.AddComponent<MeshFilter>();
            _meshFilter.mesh = CelestialMeshUtility.CreateUVSphere(sphereSegments, "Moon");
            _meshRenderer = _meshGO.AddComponent<MeshRenderer>();

            _meshGO.AddComponent<SphereCollider>().radius = 0.5f;
        }

        void CreateMaterial()
        {
            var shader = Shader.Find("CodeGamified/CelestialMoon");
            if (shader == null) shader = Shader.Find("Unlit/Texture");

            _material = new Material(shader);

            if (moonTexture != null) _material.SetTexture("_MainTex", moonTexture);
            if (normalMap != null)
            {
                _material.SetTexture("_NormalMap", normalMap);
                _material.SetFloat("_NormalStrength", normalStrength);
            }

            _material.SetFloat("_DayBrightness", dayBrightness);
            _material.SetFloat("_TerminatorSharpness", terminatorSharpness);
            _material.SetFloat("_AmbientLight", ambientLight);
            _material.SetFloat("_EarthshineIntensity", earthshineIntensity);
            _material.SetColor("_EarthshineColor", earthshineColor);
            _material.SetFloat("_Smoothness", smoothness);
            _material.SetColor("_SurfaceColor", surfaceTint);

            if (planet != null)
                _material.SetFloat("_EarthRadius", planet.radius);

            _meshRenderer.material = _material;
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
        }

        // ═══════════════════════════════════════════════════════════════
        // ORBIT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Advance orbit by simulation-scaled delta time.
        /// Called automatically from Update via SimulationTime, but can also be
        /// called manually for decoupled time control (à la WorldGraph.ActiveTraversal.Advance).
        /// </summary>
        public void AdvanceOrbit(float dt)
        {
            float periodSec = orbitalPeriodDays * secondsPerDay;
            float angularVel = 360f / periodSec;
            _currentAngle = (_currentAngle + angularVel * dt) % 360f;
            UpdateOrbitalPosition();
        }

        void UpdateOrbit()
        {
            if (SimulationTime.Instance == null) return;
            float dt = UnityEngine.Time.deltaTime * SimulationTime.Instance.timeScale;
            AdvanceOrbit(dt);
        }

        void UpdateOrbitalPosition()
        {
            float rad = _currentAngle * Mathf.Deg2Rad;
            float incRad = orbitalInclination * Mathf.Deg2Rad;

            float x = orbitalDistance * Mathf.Cos(rad);
            float z = orbitalDistance * Mathf.Sin(rad) * Mathf.Cos(incRad);
            float y = orbitalDistance * Mathf.Sin(rad) * Mathf.Sin(incRad);

            Vector3 center = planet != null ? planet.transform.position : Vector3.zero;
            transform.position = center + new Vector3(x, y, z);

            // Tidal lock — always face the planet
            transform.LookAt(center);
        }

        void UpdateShaderParams()
        {
            if (_material == null) return;

            // Sun + Earth position always update (change every frame due to orbit)
            Vector3 toSun = sunDirection.normalized;
            _material.SetVector("_SunDir", new Vector4(toSun.x, toSun.y, toSun.z, 0));

            Vector3 earthPos = planet != null ? planet.transform.position : Vector3.zero;
            _material.SetVector("_EarthPos", new Vector4(earthPos.x, earthPos.y, earthPos.z, 0));

            // Inspector-tunable params — only push when changed
            if (!Mathf.Approximately(_prevDayBright, dayBrightness))     { _material.SetFloat("_DayBrightness", dayBrightness);           _prevDayBright = dayBrightness; }
            if (!Mathf.Approximately(_prevTermSharp, terminatorSharpness)) { _material.SetFloat("_TerminatorSharpness", terminatorSharpness); _prevTermSharp = terminatorSharpness; }
            if (!Mathf.Approximately(_prevAmbient, ambientLight))        { _material.SetFloat("_AmbientLight", ambientLight);             _prevAmbient = ambientLight; }
            if (!Mathf.Approximately(_prevNormStr, normalStrength))      { _material.SetFloat("_NormalStrength", normalStrength);          _prevNormStr = normalStrength; }
            if (!Mathf.Approximately(_prevEarthshine, earthshineIntensity)) { _material.SetFloat("_EarthshineIntensity", earthshineIntensity); _prevEarthshine = earthshineIntensity; }
            if (_prevEarthshineCol != earthshineColor)                   { _material.SetColor("_EarthshineColor", earthshineColor);        _prevEarthshineCol = earthshineColor; }
            if (!Mathf.Approximately(_prevSmooth, smoothness))           { _material.SetFloat("_Smoothness", smoothness);                 _prevSmooth = smoothness; }
            if (_prevSurfaceTint != surfaceTint)                         { _material.SetColor("_SurfaceColor", surfaceTint);              _prevSurfaceTint = surfaceTint; }
        }

        // ═══════════════════════════════════════════════════════════════
        // QUALITY
        // ═══════════════════════════════════════════════════════════════

        public void SetSphereQuality(int segments)
        {
            segments = Mathf.Clamp(segments, 24, 96);
            if (segments == sphereSegments) return;
            sphereSegments = segments;

            if (_meshFilter != null)
                CelestialMeshUtility.ReplaceMesh(_meshFilter, CelestialMeshUtility.CreateUVSphere(segments, "Moon"));
        }

        /// <summary>Segments per quality tier.</summary>
        public static int SegmentsForQuality(QualityTier tier) => QualityHints.SphereSegments(tier, DetailRole.Secondary);

        // ═══════════════════════════════════════════════════════════════
        // GIZMOS
        // ═══════════════════════════════════════════════════════════════

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.gray;
            Vector3 center = planet != null ? planet.transform.position : Vector3.zero;

            const int GIZMO_SEGMENTS = 64;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= GIZMO_SEGMENTS; i++)
            {
                float a = (i / (float)GIZMO_SEGMENTS) * 360f * Mathf.Deg2Rad;
                float incRad = orbitalInclination * Mathf.Deg2Rad;
                float ox = orbitalDistance * Mathf.Cos(a);
                float oz = orbitalDistance * Mathf.Sin(a) * Mathf.Cos(incRad);
                float oy = orbitalDistance * Mathf.Sin(a) * Mathf.Sin(incRad);
                Vector3 pos = center + new Vector3(ox, oy, oz);
                if (i > 0) Gizmos.DrawLine(prev, pos);
                prev = pos;
            }
        }
    }
}
