// CodeGamified.Settings — Shared settings management framework
// MIT License

namespace CodeGamified.Settings
{
    /// <summary>
    /// Snapshot of all user-facing settings.
    /// Passed to <see cref="ISettingsListener.OnSettingsChanged"/> so listeners
    /// can read whichever fields they care about.
    ///
    /// Value type — cheap to copy, no allocations.
    /// </summary>
    public readonly struct SettingsSnapshot
    {
        // ── Quality ─────────────────────────────────────────────

        /// <summary>Graphics quality level (0 = Low … 3 = Ultra).</summary>
        public readonly int QualityLevel;

        // ── Audio ───────────────────────────────────────────────

        /// <summary>Master volume (0.0–1.0).</summary>
        public readonly float MasterVolume;

        /// <summary>Music volume (0.0–1.0).</summary>
        public readonly float MusicVolume;

        /// <summary>Sound-effects volume (0.0–1.0).</summary>
        public readonly float SfxVolume;

        // ── Display ─────────────────────────────────────────────

        /// <summary>TUI font size in points.</summary>
        public readonly float FontSize;

        public SettingsSnapshot(
            int qualityLevel,
            float masterVolume, float musicVolume, float sfxVolume,
            float fontSize)
        {
            QualityLevel = qualityLevel;
            MasterVolume = masterVolume;
            MusicVolume = musicVolume;
            SfxVolume = sfxVolume;
            FontSize = fontSize;
        }
    }
}
