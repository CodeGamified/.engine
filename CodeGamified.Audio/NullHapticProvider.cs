namespace CodeGamified.Audio
{
    /// <summary>No-op default — all methods are no-ops.</summary>
    public sealed class NullHapticProvider : IHapticProvider
    {
        public void TapLight() { }
        public void TapMedium() { }
        public void TapHeavy() { }
        public void Buzz(float duration) { }
    }
}
