// CodeGamified.Settings — Shared settings management framework
// MIT License
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.Settings
{
    /// <summary>
    /// Static hub that owns all user-facing settings, persists them via PlayerPrefs,
    /// and broadcasts changes to registered <see cref="ISettingsListener"/> components.
    ///
    /// Replaces per-SettingsTerminal Save/Load/Apply scatter with a single source of truth.
    ///
    /// Usage — Settings UI:
    ///   SettingsBridge.SetQualityLevel(2);
    ///   SettingsBridge.SetMasterVolume(0.8f);
    ///   SettingsBridge.SetFontSize(14f);
    ///
    /// Usage — Game components:
    ///   void OnEnable()  => SettingsBridge.Register(this);
    ///   void OnDisable() => SettingsBridge.Unregister(this);
    ///   void OnSettingsChanged(SettingsSnapshot s, SettingsCategory c) { ... }
    ///
    /// Usage — One-off listeners:
    ///   SettingsBridge.OnChanged += (snapshot, cat) => ...;
    /// </summary>
    public static class SettingsBridge
    {
        // ── Persistence keys ────────────────────────────────────
        private const string KEY_QUALITY       = "CG_QualityLevel";
        private const string KEY_MASTER_VOL    = "CG_MasterVolume";
        private const string KEY_MUSIC_VOL     = "CG_MusicVolume";
        private const string KEY_SFX_VOL       = "CG_SfxVolume";
        private const string KEY_FONT_SIZE     = "CG_FontSize";

        // ── Defaults ────────────────────────────────────────────
        public const int    DEFAULT_QUALITY       = 3;      // Ultra
        public const float  DEFAULT_MASTER_VOLUME = 0.5f;
        public const float  DEFAULT_MUSIC_VOLUME  = 0.25f;
        public const float  DEFAULT_SFX_VOLUME    = 0.75f;
        public const float  DEFAULT_FONT_SIZE     = 20f;
        public const float  MIN_FONT_SIZE         = 8f;
        public const float  MAX_FONT_SIZE         = 48f;

        // ── Current state ───────────────────────────────────────
        private static int   _qualityLevel  = DEFAULT_QUALITY;
        private static float _masterVolume  = DEFAULT_MASTER_VOLUME;
        private static float _musicVolume   = DEFAULT_MUSIC_VOLUME;
        private static float _sfxVolume     = DEFAULT_SFX_VOLUME;
        private static float _fontSize      = DEFAULT_FONT_SIZE;
        private static bool  _loaded        = false;

        // ── Public read-only accessors ──────────────────────────
        public static int   QualityLevel => _qualityLevel;
        public static float MasterVolume => _masterVolume;
        public static float MusicVolume  => _musicVolume;
        public static float SfxVolume    => _sfxVolume;
        public static float FontSize     => _fontSize;

        /// <summary>Current snapshot of all settings.</summary>
        public static SettingsSnapshot Snapshot => new(
            _qualityLevel, _masterVolume, _musicVolume, _sfxVolume, _fontSize);

        // ── Events ──────────────────────────────────────────────

        /// <summary>Fired after any setting changes and all listeners are notified.</summary>
        public static event Action<SettingsSnapshot, SettingsCategory> OnChanged;

        // ── Listener registry ───────────────────────────────────
        private static readonly List<ISettingsListener> _listeners = new(16);

        public static void Register(ISettingsListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        public static void Unregister(ISettingsListener listener)
        {
            _listeners.Remove(listener);
        }

        public static int ListenerCount => _listeners.Count;

        // ═══════════════════════════════════════════════════════════════
        // SETTERS (each validates, stores, and notifies)
        // ═══════════════════════════════════════════════════════════════

        public static void SetQualityLevel(int level)
        {
            _qualityLevel = Mathf.Clamp(level, 0, 3);
            QualitySettings.SetQualityLevel(_qualityLevel, true);
            Notify(SettingsCategory.Quality);
        }

        public static void SetMasterVolume(float vol)
        {
            _masterVolume = Mathf.Clamp01(vol);
            AudioListener.volume = _masterVolume;
            Notify(SettingsCategory.Audio);
        }

        public static void SetMusicVolume(float vol)
        {
            _musicVolume = Mathf.Clamp01(vol);
            Notify(SettingsCategory.Audio);
        }

        public static void SetSfxVolume(float vol)
        {
            _sfxVolume = Mathf.Clamp01(vol);
            Notify(SettingsCategory.Audio);
        }

        public static void SetFontSize(float size)
        {
            _fontSize = Mathf.Clamp(size, MIN_FONT_SIZE, MAX_FONT_SIZE);
            Notify(SettingsCategory.Display);
        }

        // ═══════════════════════════════════════════════════════════════
        // PERSISTENCE
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Load all settings from PlayerPrefs. Safe to call multiple times;
        /// only loads once unless <paramref name="force"/> is true.
        /// </summary>
        public static void Load(bool force = false)
        {
            if (_loaded && !force) return;

            _qualityLevel = PlayerPrefs.GetInt(KEY_QUALITY, DEFAULT_QUALITY);
            _masterVolume = PlayerPrefs.GetFloat(KEY_MASTER_VOL, DEFAULT_MASTER_VOLUME);
            _musicVolume  = PlayerPrefs.GetFloat(KEY_MUSIC_VOL, DEFAULT_MUSIC_VOLUME);
            _sfxVolume    = PlayerPrefs.GetFloat(KEY_SFX_VOL, DEFAULT_SFX_VOLUME);
            _fontSize     = PlayerPrefs.GetFloat(KEY_FONT_SIZE, DEFAULT_FONT_SIZE);

            // Apply side-effects without broadcasting (listeners may not be registered yet)
            QualitySettings.SetQualityLevel(_qualityLevel, true);
            AudioListener.volume = _masterVolume;

            _loaded = true;
            Debug.Log("[SettingsBridge] Settings loaded");
        }

        /// <summary>
        /// Persist all current settings to PlayerPrefs.
        /// </summary>
        public static void Save()
        {
            PlayerPrefs.SetInt(KEY_QUALITY, _qualityLevel);
            PlayerPrefs.SetFloat(KEY_MASTER_VOL, _masterVolume);
            PlayerPrefs.SetFloat(KEY_MUSIC_VOL, _musicVolume);
            PlayerPrefs.SetFloat(KEY_SFX_VOL, _sfxVolume);
            PlayerPrefs.SetFloat(KEY_FONT_SIZE, _fontSize);
            PlayerPrefs.Save();
            Debug.Log("[SettingsBridge] Settings saved");
        }

        /// <summary>Reset all settings to defaults, save, and notify.</summary>
        public static void ResetToDefaults()
        {
            _qualityLevel = DEFAULT_QUALITY;
            _masterVolume = DEFAULT_MASTER_VOLUME;
            _musicVolume  = DEFAULT_MUSIC_VOLUME;
            _sfxVolume    = DEFAULT_SFX_VOLUME;
            _fontSize     = DEFAULT_FONT_SIZE;

            QualitySettings.SetQualityLevel(_qualityLevel, true);
            AudioListener.volume = _masterVolume;

            Save();

            // Notify all categories
            Notify(SettingsCategory.Quality);
            Notify(SettingsCategory.Audio);
            Notify(SettingsCategory.Display);
        }

        // ═══════════════════════════════════════════════════════════════
        // INTERNAL
        // ═══════════════════════════════════════════════════════════════

        private static void Notify(SettingsCategory category)
        {
            var snapshot = Snapshot;

            for (int i = _listeners.Count - 1; i >= 0; i--)
            {
                if (_listeners[i] is UnityEngine.Object obj && obj == null)
                {
                    _listeners.RemoveAt(i);
                    continue;
                }
                _listeners[i].OnSettingsChanged(snapshot, category);
            }

            OnChanged?.Invoke(snapshot, category);
        }
    }
}
