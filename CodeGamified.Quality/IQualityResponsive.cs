// CodeGamified.Quality — Shared quality management framework
// MIT License

namespace CodeGamified.Quality
{
    /// <summary>
    /// Implemented by any MonoBehaviour that adjusts its visuals when quality changes.
    ///
    /// Register/unregister with <see cref="QualityBridge"/>:
    ///   OnEnable  → QualityBridge.Register(this)
    ///   OnDisable → QualityBridge.Unregister(this)
    ///
    /// Examples:
    ///   Ship:      adjust hull rib count, rigging detail
    ///   Satellite: adjust solar panel segment count
    ///   Pong:      adjust trail length, court dash density
    ///   BitNaughts: adjust sphere segments on earth/moon, atmosphere layers
    /// </summary>
    public interface IQualityResponsive
    {
        /// <summary>
        /// Called when the global quality tier changes.
        /// Implementors should rebuild meshes, adjust LOD, etc.
        /// </summary>
        void OnQualityChanged(QualityTier tier);
    }
}
