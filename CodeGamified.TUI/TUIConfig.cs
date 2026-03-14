// ═══════════════════════════════════════════════════════════
//  TUI.Config — Brand gradient & settings
//  Python source: TUI.py § Config
// ═══════════════════════════════════════════════════════════
using UnityEngine;

namespace CodeGamified.TUI
{
    /// <summary>
    /// Brand gradient and runtime configuration.
    /// Python: MSFT_GRADIENT / load_settings()
    /// </summary>
    public static class TUIConfig
    {
        /// <summary>
        /// Brand gradient stops. Default: Blue → Green → Yellow → Red.
        /// Overwrite at runtime via Load() or ScriptableObject.
        /// </summary>
        public static Color32[] Gradient = DefaultGradient();

        static Color32[] DefaultGradient() => new Color32[]
        {
            new(0,   164, 239, 255),  // Blue
            new(127, 186, 0,   255),  // Green
            new(255, 185, 0,   255),  // Yellow
            new(242, 80,  34,  255),  // Red
        };

        /// <summary>
        /// Load gradient from a JSON accents array: ["r,g,b", "r,g,b", ...].
        /// Falls back to defaults on any parse error.
        /// </summary>
        public static void Load(string json)
        {
            try
            {
                var wrapper = JsonUtility.FromJson<AccentsWrapper>(json);
                if (wrapper?.accents != null && wrapper.accents.Length >= 2)
                {
                    var g = new Color32[wrapper.accents.Length];
                    for (int i = 0; i < wrapper.accents.Length; i++)
                        g[i] = ParseRgb(wrapper.accents[i]);
                    Gradient = g;
                    return;
                }
            }
            catch { /* fall through */ }

            Gradient = DefaultGradient();
        }

        /// <summary>Reset to hardcoded defaults.</summary>
        public static void Reset() => Gradient = DefaultGradient();

        static Color32 ParseRgb(string csv)
        {
            var parts = csv.Split(',');
            return new Color32(
                byte.Parse(parts[0].Trim()),
                byte.Parse(parts[1].Trim()),
                byte.Parse(parts[2].Trim()),
                255
            );
        }

        [System.Serializable]
        class AccentsWrapper { public string[] accents; }
    }
}
