namespace CodeGamified.Audio
{
    /// <summary>
    /// Supplies raw spectrum / frequency-band data from the game's audio system.
    /// The game project implements this — typically wrapping
    /// <c>AudioListener.GetSpectrumData</c> or an FFT plugin.
    /// </summary>
    public interface IEqualizerProvider
    {
        /// <summary>Number of frequency bands this provider outputs.</summary>
        int BandCount { get; }

        /// <summary>
        /// Fill <paramref name="bands"/> with current normalised amplitudes
        /// in [0, 1]. The array is pre-allocated by the <see cref="Equalizer"/>
        /// to length <see cref="BandCount"/>.
        /// Returns <c>true</c> if new data was written, <c>false</c> if idle.
        /// </summary>
        bool GetBands(float[] bands);
    }
}
