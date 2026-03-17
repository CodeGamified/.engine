using System;

namespace CodeGamified.Audio
{
    /// <summary>
    /// Pure-data equalizer model. Reads raw bands from an
    /// <see cref="IEqualizerProvider"/>, applies smoothing / peak-hold / decay,
    /// and exposes the result for visualisation (TUI, shader, etc.).
    /// <para>No Unity dependency — uses <see cref="Math"/> only.</para>
    /// <code>
    /// var eq = new Equalizer(provider);
    /// // each frame:
    /// eq.Update(deltaTime);
    /// float[] smoothed = eq.SmoothedBands;
    /// float[] peaks    = eq.PeakBands;
    /// </code>
    /// </summary>
    public sealed class Equalizer
    {
        readonly IEqualizerProvider _provider;
        readonly float[] _raw;

        /// <summary>Smoothed band levels [0,1] — use for bar heights.</summary>
        public readonly float[] SmoothedBands;

        /// <summary>Peak-hold band levels [0,1] — use for peak markers.</summary>
        public readonly float[] PeakBands;

        /// <summary>Number of frequency bands.</summary>
        public int BandCount => _provider.BandCount;

        // ── Tuning knobs ────────────────────────────────────────

        /// <summary>Rise speed (0→1 per second). Higher = snappier.</summary>
        public float RiseSpeed = 12f;

        /// <summary>Fall speed (1→0 per second). Lower = smoother.</summary>
        public float FallSpeed = 4f;

        /// <summary>Seconds a peak marker holds before falling.</summary>
        public float PeakHoldTime = 0.4f;

        /// <summary>Fall speed for peak markers once hold expires.</summary>
        public float PeakFallSpeed = 1.5f;

        readonly float[] _peakAge;

        public Equalizer(IEqualizerProvider provider)
        {
            _provider     = provider ?? throw new ArgumentNullException(nameof(provider));
            int n         = provider.BandCount;
            _raw          = new float[n];
            SmoothedBands = new float[n];
            PeakBands     = new float[n];
            _peakAge      = new float[n];
        }

        /// <summary>
        /// Call once per frame with <paramref name="deltaTime"/>.
        /// Reads the provider then updates smoothed + peak arrays.
        /// </summary>
        public void Update(float deltaTime)
        {
            bool hasData = _provider.GetBands(_raw);

            int n = BandCount;
            for (int i = 0; i < n; i++)
            {
                float target = hasData ? Clamp01(_raw[i]) : 0f;

                // Smooth toward target (fast rise, slow fall)
                float cur = SmoothedBands[i];
                if (target > cur)
                    cur += (target - cur) * Math.Min(1f, RiseSpeed * deltaTime);
                else
                    cur += (target - cur) * Math.Min(1f, FallSpeed * deltaTime);

                SmoothedBands[i] = Clamp01(cur);

                // Peak hold / decay
                if (cur >= PeakBands[i])
                {
                    PeakBands[i] = cur;
                    _peakAge[i]  = 0f;
                }
                else
                {
                    _peakAge[i] += deltaTime;
                    if (_peakAge[i] > PeakHoldTime)
                    {
                        PeakBands[i] -= PeakFallSpeed * deltaTime;
                        if (PeakBands[i] < cur) PeakBands[i] = cur;
                    }
                }
            }
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
