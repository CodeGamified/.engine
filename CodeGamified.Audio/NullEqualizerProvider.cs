namespace CodeGamified.Audio
{
    /// <summary>Silent default — always returns no data.</summary>
    public sealed class NullEqualizerProvider : IEqualizerProvider
    {
        readonly int _bandCount;

        public NullEqualizerProvider(int bandCount = 8) => _bandCount = bandCount;

        public int BandCount => _bandCount;

        public bool GetBands(float[] bands) => false;
    }
}
