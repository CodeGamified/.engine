// CodeGamified.Quality — Shared quality management framework
// MIT License
using UnityEngine;

namespace CodeGamified.Quality
{
    /// <summary>
    /// Static helpers mapping QualityTier → concrete geometry/rendering parameters.
    ///
    /// Used by:
    ///   - ProceduralAssembler consumers to pick segment counts
    ///   - Settings UI to display quality metrics
    ///   - IQualityResponsive implementations for mesh rebuild
    ///
    /// Games can use these as defaults or override with their own values.
    /// </summary>
    public static class QualityHints
    {
        // ═══════════════════════════════════════════════════════════════
        // SPHERE / MESH GEOMETRY
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Recommended sphere segment count for a given tier and detail role.
        /// </summary>
        public static int SphereSegments(QualityTier tier, DetailRole role = DetailRole.Primary)
        {
            return (tier, role) switch
            {
                (QualityTier.Low,    DetailRole.Primary)   => 16,
                (QualityTier.Low,    DetailRole.Secondary) => 12,
                (QualityTier.Low,    DetailRole.Effect)    => 8,

                (QualityTier.Medium, DetailRole.Primary)   => 32,
                (QualityTier.Medium, DetailRole.Secondary) => 24,
                (QualityTier.Medium, DetailRole.Effect)    => 12,

                (QualityTier.High,   DetailRole.Primary)   => 48,
                (QualityTier.High,   DetailRole.Secondary) => 36,
                (QualityTier.High,   DetailRole.Effect)    => 16,

                (QualityTier.Ultra,  DetailRole.Primary)   => 64,
                (QualityTier.Ultra,  DetailRole.Secondary) => 48,
                (QualityTier.Ultra,  DetailRole.Effect)    => 24,

                _ => 32
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // TEXTURES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Recommended max texture resolution (pixels) for a quality tier.
        /// </summary>
        public static int TextureResolution(QualityTier tier) => tier switch
        {
            QualityTier.Low    => 1024,
            QualityTier.Medium => 2048,
            QualityTier.High   => 4096,
            _                  => 8192
        };

        /// <summary>
        /// Human-readable texture resolution label (1K / 2K / 4K / 8K).
        /// </summary>
        public static string TextureLabel(QualityTier tier) => tier switch
        {
            QualityTier.Low    => "1K",
            QualityTier.Medium => "2K",
            QualityTier.High   => "4K",
            _                  => "8K"
        };

        // ═══════════════════════════════════════════════════════════════
        // TRAILS / PARTICLES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Recommended trail segment count.
        /// Ultra returns a very large value for persistent "light painting" trails.
        /// </summary>
        public static int TrailSegments(QualityTier tier) => tier switch
        {
            QualityTier.Low    => 16,
            QualityTier.Medium => 40,
            QualityTier.High   => 120,
            _                  => 8000  // Ultra: persistent full-match trail
        };

        /// <summary>
        /// Recommended particle emission budget multiplier (1.0 = baseline).
        /// </summary>
        public static float ParticleBudget(QualityTier tier) => tier switch
        {
            QualityTier.Low    => 0.25f,
            QualityTier.Medium => 0.5f,
            QualityTier.High   => 0.75f,
            _                  => 1.0f
        };

        // ═══════════════════════════════════════════════════════════════
        // AGGREGATE ESTIMATES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Rough estimated triangle count for a typical scene at this quality tier.
        /// Games should override with measured values if needed.
        /// </summary>
        public static int EstimatedTriangles(QualityTier tier) => tier switch
        {
            QualityTier.Low    => 7_900,
            QualityTier.Medium => 15_400,
            QualityTier.High   => 33_500,
            _                  => 58_300
        };

        // ═══════════════════════════════════════════════════════════════
        // COURT / PROCEDURAL DENSITY (Pong-style)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Recommended center-line dash density multiplier for court-style games.
        /// </summary>
        /// <remarks>
        /// A value >= 100 signals "solid line" mode (no dashes).
        /// </remarks>
        public static float CourtDashDensity(QualityTier tier) => tier switch
        {
            QualityTier.Low    => 0.5f,
            QualityTier.Medium => 0.75f,
            QualityTier.High   => 1.0f,
            QualityTier.Ultra  => 100f, // solid bar
            _                  => 1.0f
        };

        // ═══════════════════════════════════════════════════════════════
        // EMISSION / GLOW
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether emission/glow effects (bloom, point lights, emission materials)
        /// should be enabled at this quality tier.
        /// </summary>
        public static bool EmissionEnabled(QualityTier tier) => tier != QualityTier.Low;
    }
}
