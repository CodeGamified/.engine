// CodeGamified.Quality — Shared quality management framework
// MIT License

namespace CodeGamified.Quality
{
    /// <summary>
    /// Quality tiers shared across all CodeGamified games.
    /// Int values match Unity's QualitySettings level indices.
    /// </summary>
    public enum QualityTier
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3
    }

    /// <summary>
    /// Detail role for geometry parameter lookups.
    /// Lets <see cref="QualityHints"/> return different segment counts
    /// for primary, secondary, and effect-layer objects.
    /// </summary>
    public enum DetailRole
    {
        Primary,    // Main focal objects (earth, player paddle, ship hull)
        Secondary,  // Supporting objects (moon, AI paddle, masts)
        Effect      // Visual effects (atmosphere layers, trails, particles)
    }
}
