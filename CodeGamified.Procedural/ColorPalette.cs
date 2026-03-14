// CodeGamified.Procedural — Shared procedural rendering framework
// MIT License
using UnityEngine;
using System.Collections.Generic;

namespace CodeGamified.Procedural
{
    /// <summary>
    /// Themeable color palette for procedural part assembly.
    ///
    /// Each game ships its own palette:
    ///   Ship:      nautical wood/iron/canvas tones
    ///   Satellite: metallic silver/blue/gold
    ///   Pong:      neon/retro arcade colors
    ///
    /// ScriptableObject so it lives as a Unity asset, editable in Inspector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewPalette", menuName = "CodeGamified/Color Palette")]
    public class ColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string key;
            public Color color;
        }

        [SerializeField] private Entry[] entries = System.Array.Empty<Entry>();

        /// <summary>Fallback color when key is not found.</summary>
        [SerializeField] private Color fallback = Color.magenta;

        // Runtime lookup built on first access
        private Dictionary<string, Color> _lookup;

        /// <summary>
        /// Resolve a color key. Returns fallback (magenta) if not found.
        /// </summary>
        public Color Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            EnsureLookup();
            return _lookup.TryGetValue(key, out var c) ? c : fallback;
        }

        /// <summary>
        /// Try to resolve a color key.
        /// </summary>
        public bool TryResolve(string key, out Color color)
        {
            if (string.IsNullOrEmpty(key)) { color = fallback; return false; }
            EnsureLookup();
            return _lookup.TryGetValue(key, out color);
        }

        /// <summary>
        /// Check if a key exists in the palette.
        /// </summary>
        public bool Contains(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            EnsureLookup();
            return _lookup.ContainsKey(key);
        }

        /// <summary>
        /// Build a palette from code (for games that don't use ScriptableObject assets).
        /// </summary>
        public static ColorPalette CreateRuntime(Dictionary<string, Color> colors, Color? fallback = null)
        {
            var palette = CreateInstance<ColorPalette>();
            palette.entries = new Entry[colors.Count];
            int i = 0;
            foreach (var kv in colors)
            {
                palette.entries[i++] = new Entry { key = kv.Key, color = kv.Value };
            }
            if (fallback.HasValue) palette.fallback = fallback.Value;
            palette._lookup = null; // force rebuild
            return palette;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Color>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                if (!string.IsNullOrEmpty(entries[i].key))
                    _lookup[entries[i].key] = entries[i].color;
            }
        }

        private void OnValidate()
        {
            _lookup = null; // rebuild on Inspector edit
        }
    }
}
