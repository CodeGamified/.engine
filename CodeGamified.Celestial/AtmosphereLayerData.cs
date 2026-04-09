// CodeGamified.Celestial — Shared celestial rendering framework
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace CodeGamified.Celestial
{
    /// <summary>
    /// Data structure for a single atmosphere layer (Troposphere, Stratosphere, etc.).
    /// </summary>
    [System.Serializable]
    public class AtmosphereLayerData
    {
        /// <summary>Earth equatorial radius in km — normalizes altitude fractions.</summary>
        const float EARTH_RADIUS_KM = 6371f;

        public string LayerName;
        public float AltitudeMinKm;
        public float AltitudeMaxKm;
        public Color BaseColor;
        public float AlphaBase;
        public float DensityRelative;
        public float TemperatureMinC;
        public float TemperatureMaxC;
        public float VisualScale;
        public string Description;

        public float GetInnerRadius(float bodyRadius, float visualExaggeration = 1f)
        {
            float realScale = AltitudeMinKm / EARTH_RADIUS_KM;
            return bodyRadius * (1f + realScale * VisualScale * visualExaggeration);
        }

        public float GetOuterRadius(float bodyRadius, float visualExaggeration = 1f)
        {
            float realScale = AltitudeMaxKm / EARTH_RADIUS_KM;
            return bodyRadius * (1f + realScale * VisualScale * visualExaggeration);
        }
    }

    /// <summary>
    /// Loads atmosphere layer data from CSV resource or provides Earth defaults.
    /// Singleton for convenient access; create via AtmosphereLayerDataLoader.EnsureLoaded().
    /// </summary>
    public class AtmosphereLayerDataLoader : MonoBehaviour
    {
        public static AtmosphereLayerDataLoader Instance { get; private set; }

        [Header("Data Source")]
        [Tooltip("Resource path to CSV (without extension). Leave empty for built-in Earth defaults.")]
        public string csvResourcePath = "";

        public List<AtmosphereLayerData> Layers { get; private set; } = new List<AtmosphereLayerData>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Load();
        }

        /// <summary>
        /// Ensure instance exists. Creates one if needed. Returns the loaded layers.
        /// </summary>
        public static List<AtmosphereLayerData> EnsureLoaded()
        {
            if (Instance != null) return Instance.Layers;
            var go = new GameObject("AtmosphereLayerData");
            var loader = go.AddComponent<AtmosphereLayerDataLoader>();
            return loader.Layers;
        }

        /// <summary>
        /// Pure-static loader — no MonoBehaviour needed (cross-pollination from WorldGraph's pure-data pattern).
        /// Returns Earth defaults or parsed CSV. Use when you just need data without a scene singleton.
        /// </summary>
        public static List<AtmosphereLayerData> LoadStatic(string csvResourcePath = "")
        {
            var layers = new List<AtmosphereLayerData>();

            if (!string.IsNullOrEmpty(csvResourcePath))
            {
                var csv = Resources.Load<TextAsset>(csvResourcePath);
                if (csv != null)
                {
                    ParseCSVInto(csv.text, layers);
                    return layers;
                }
            }

            CreateEarthDefaultsInto(layers);
            return layers;
        }

        public void Load()
        {
            Layers.Clear();

            if (!string.IsNullOrEmpty(csvResourcePath))
            {
                var csv = Resources.Load<TextAsset>(csvResourcePath);
                if (csv != null)
                {
                    ParseCSVInto(csv.text, Layers);
                    return;
                }
                Debug.LogWarning($"[CEL] CSV not found at Resources/{csvResourcePath}, using defaults");
            }

            CreateEarthDefaultsInto(Layers);
        }

        // ═══════════════════════════════════════════════════════════════
        // CSV PARSING (static — usable from both instance and LoadStatic)
        // ═══════════════════════════════════════════════════════════════

        static void ParseCSVInto(string text, List<AtmosphereLayerData> target)
        {
            string[] lines = text.Split('\n');
            bool headerSkipped = false;

            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                if (!headerSkipped && line.StartsWith("layer_name"))
                {
                    headerSkipped = true;
                    continue;
                }

                var parts = ParseCSVLine(line);
                if (parts.Count < 12) continue;

                try
                {
                    target.Add(new AtmosphereLayerData
                    {
                        LayerName       = parts[0],
                        AltitudeMinKm   = float.Parse(parts[1]),
                        AltitudeMaxKm   = float.Parse(parts[2]),
                        BaseColor       = new Color(float.Parse(parts[3]), float.Parse(parts[4]), float.Parse(parts[5]), 1f),
                        AlphaBase       = float.Parse(parts[6]),
                        DensityRelative = float.Parse(parts[7]),
                        TemperatureMinC = float.Parse(parts[8]),
                        TemperatureMaxC = float.Parse(parts[9]),
                        VisualScale     = float.Parse(parts[10]),
                        Description     = parts[11].Trim('"')
                    });
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CEL] Failed to parse CSV line: {line}\n{e.Message}");
                }
            }
        }

        static List<string> ParseCSVLine(string line)
        {
            var parts = new List<string>();
            bool inQuotes = false;
            string current = "";

            foreach (char c in line)
            {
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    current += c;
                }
                else if (c == ',' && !inQuotes)
                {
                    parts.Add(current);
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            if (!string.IsNullOrEmpty(current))
                parts.Add(current);

            return parts;
        }

        // ═══════════════════════════════════════════════════════════════
        // EARTH DEFAULTS (static — usable from both instance and LoadStatic)
        // ═══════════════════════════════════════════════════════════════

        static void CreateEarthDefaultsInto(List<AtmosphereLayerData> target)
        {
            target.AddRange(new[]
            {
                new AtmosphereLayerData
                {
                    LayerName = "Troposphere", AltitudeMinKm = 0, AltitudeMaxKm = 12,
                    BaseColor = new Color(0.4f, 0.6f, 1f), AlphaBase = 0.045f,
                    DensityRelative = 1f, VisualScale = 8f, Description = "Where weather happens"
                },
                new AtmosphereLayerData
                {
                    LayerName = "Stratosphere", AltitudeMinKm = 12, AltitudeMaxKm = 50,
                    BaseColor = new Color(0.5f, 0.7f, 1f), AlphaBase = 0.035f,
                    DensityRelative = 0.1f, VisualScale = 6f, Description = "Home of the ozone layer"
                },
                new AtmosphereLayerData
                {
                    LayerName = "Mesosphere", AltitudeMinKm = 50, AltitudeMaxKm = 85,
                    BaseColor = new Color(0.6f, 0.8f, 1f), AlphaBase = 0.025f,
                    DensityRelative = 0.001f, VisualScale = 4f, Description = "The coldest layer"
                },
                new AtmosphereLayerData
                {
                    LayerName = "Thermosphere", AltitudeMinKm = 85, AltitudeMaxKm = 600,
                    BaseColor = new Color(0.7f, 0.85f, 1f), AlphaBase = 0.015f,
                    DensityRelative = 0.000001f, VisualScale = 2f, Description = "Where the ISS orbits"
                },
                new AtmosphereLayerData
                {
                    LayerName = "Exosphere", AltitudeMinKm = 600, AltitudeMaxKm = 10000,
                    BaseColor = new Color(0.9f, 0.95f, 1f), AlphaBase = 0.008f,
                    DensityRelative = 0.0000000001f, VisualScale = 1.5f, Description = "The edge of space"
                }
            });
        }

        // ═══════════════════════════════════════════════════════════════
        // QUERIES
        // ═══════════════════════════════════════════════════════════════

        public AtmosphereLayerData GetLayerAtAltitude(float altitudeKm)
        {
            foreach (var layer in Layers)
                if (altitudeKm >= layer.AltitudeMinKm && altitudeKm < layer.AltitudeMaxKm)
                    return layer;
            return null;
        }

        /// <summary>Kármán line at 100 km — conventional space boundary.</summary>
        public AtmosphereLayerData GetKarmanLineLayer() => GetLayerAtAltitude(100f);
    }
}
