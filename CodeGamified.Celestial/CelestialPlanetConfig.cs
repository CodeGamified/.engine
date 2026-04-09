// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// One-shot configuration struct for <see cref="CelestialPlanet"/>.
    /// Mirrors ArcMapConfig pattern from WorldGraph: all defaults in one place,
    /// games override selectively, bootstrap is a one-liner.
    ///
    /// Usage:
    ///   var cfg = CelestialPlanetConfig.Earth;       // full Earth defaults
    ///   cfg.radius = 3.0f;                           // smaller planet
    ///   planet.ApplyConfig(cfg);
    /// </summary>
    [System.Serializable]
    public struct CelestialPlanetConfig
    {
        // Planet
        public float Radius;
        public int SphereSegments;
        public double RotationPeriodSeconds;
        public float InitialRotationOffset;
        public Vector3 SunDirection;

        // Material
        public float NormalStrength;
        public float SpecularIntensity;
        public float SpecularPower;
        public Color OceanSpecularColor;
        public float FresnelPower;
        public float FresnelIntensity;
        public float NightBrightness;
        public float DayBrightness;
        public float TerminatorSharpness;
        public float AmbientLight;

        // Clouds
        public bool ShowClouds;
        public float CloudOpacity;
        public float CloudAltitude;
        public float CloudRotationSpeed;
        public float CloudDayBrightness;
        public float CloudNightBrightness;
        public float CloudTerminatorSharpness;

        // Weather
        public bool EnableLiveClouds;
        public float HoursPerCloudTexture;
        public bool CycleSpecular;

        // Atmosphere
        public bool ShowAtmosphere;
        public bool UseLayeredAtmosphere;
        public Color SingleAtmosphereColor;
        public float AtmosphereThickness;
        public int SingleAtmosphereSegments;
        public float LayerVisualExaggeration;
        public int LayerSphereSegments;
        public float LayerGlobalAlpha;
        public float LayerFresnelPower;

        // Moonlight
        public bool EnableMoonlight;
        public float MoonlightIntensity;
        public Color MoonlightColor;

        /// <summary>Earth defaults — matches all field defaults on CelestialPlanet.</summary>
        public static CelestialPlanetConfig Earth => new()
        {
            Radius = 6.371f,
            SphereSegments = 96,
            RotationPeriodSeconds = 86400.0,
            InitialRotationOffset = 180f,
            SunDirection = Vector3.right,

            NormalStrength = 1.0f,
            SpecularIntensity = 1.0f,
            SpecularPower = 8f,
            OceanSpecularColor = Color.white,
            FresnelPower = 5f,
            FresnelIntensity = 0.3f,
            NightBrightness = 3f,
            DayBrightness = 1f,
            TerminatorSharpness = 8f,
            AmbientLight = 0.02f,

            ShowClouds = false,
            CloudOpacity = 0.75f,
            CloudAltitude = 0.005f,
            CloudRotationSpeed = 0.001f,
            CloudDayBrightness = 0.75f,
            CloudNightBrightness = 0.25f,
            CloudTerminatorSharpness = 6f,

            EnableLiveClouds = true,
            HoursPerCloudTexture = 0.25f,
            CycleSpecular = true,

            ShowAtmosphere = true,
            UseLayeredAtmosphere = true,
            SingleAtmosphereColor = new Color(0.4f, 0.7f, 1.0f, 0.2f),
            AtmosphereThickness = 0.05f,
            SingleAtmosphereSegments = 48,
            LayerVisualExaggeration = 3f,
            LayerSphereSegments = 24,
            LayerGlobalAlpha = 1f,
            LayerFresnelPower = 3f,

            EnableMoonlight = true,
            MoonlightIntensity = 0.1f,
            MoonlightColor = new Color(0.7f, 0.8f, 1.0f, 1.0f),
        };

        /// <summary>Mars-like preset — red tint, thinner atmosphere, longer day.</summary>
        public static CelestialPlanetConfig Mars
        {
            get
            {
                var cfg = Earth;
                cfg.Radius = 3.3895f; // ~3390 km
                cfg.RotationPeriodSeconds = 88775.0; // 24h 37m
                cfg.SingleAtmosphereColor = new Color(0.8f, 0.5f, 0.3f, 0.08f);
                cfg.AtmosphereThickness = 0.02f;
                cfg.ShowClouds = false;
                cfg.FresnelIntensity = 0.15f;
                cfg.SpecularIntensity = 0.1f; // no oceans
                return cfg;
            }
        }
    }
}
