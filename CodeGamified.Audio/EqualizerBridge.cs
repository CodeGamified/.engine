using System;

namespace CodeGamified.Audio
{
    /// <summary>
    /// Factory for a time-scale-gated <see cref="Equalizer"/> wrapper.
    /// Follows the same pattern as <see cref="AudioBridge"/> /
    /// <see cref="HapticBridge"/>.
    /// <code>
    /// var eqHandler = EqualizerBridge.Create(eqProvider, () => Time.timeScale);
    /// // each frame:
    /// eqHandler.Update(Time.deltaTime);
    /// float[] bands = eqHandler.Equalizer.SmoothedBands;
    /// </code>
    /// </summary>
    public static class EqualizerBridge
    {
        /// <summary>
        /// Create a gated equalizer handler.
        /// Default <see cref="GatedEqualizer.MaxTimeScale"/> is
        /// <see cref="float.MaxValue"/> (always active).
        /// </summary>
        public static GatedEqualizer Create(
            IEqualizerProvider provider,
            Func<float> getTimeScale = null,
            float maxTimeScale = float.MaxValue)
            => new(provider, getTimeScale, maxTimeScale);

        public sealed class GatedEqualizer
        {
            /// <summary>
            /// When <c>getTimeScale()</c> exceeds this value the
            /// equalizer update is skipped (bands decay to zero).
            /// </summary>
            public float MaxTimeScale;

            /// <summary>The underlying equalizer data model.</summary>
            public readonly Equalizer Equalizer;

            readonly Func<float> _getTimeScale;

            internal GatedEqualizer(
                IEqualizerProvider provider,
                Func<float> getTimeScale,
                float maxTimeScale)
            {
                Equalizer      = new Equalizer(provider);
                _getTimeScale  = getTimeScale;
                MaxTimeScale   = maxTimeScale;
            }

            bool Gated => _getTimeScale != null && _getTimeScale() > MaxTimeScale;

            /// <summary>
            /// Call once per frame. When gated, bands decay naturally
            /// (the provider isn't polled, so target becomes 0).
            /// </summary>
            public void Update(float deltaTime)
            {
                if (Gated) return;
                Equalizer.Update(deltaTime);
            }
        }
    }
}
