namespace CodeGamified.Audio
{
    /// <summary>
    /// Game-specific haptic feedback. Implement per platform.
    /// </summary>
    public interface IHapticProvider
    {
        void TapLight();                // option tap
        void TapMedium();               // insert / delete
        void TapHeavy();                // compile error
        void Buzz(float duration);      // generic buzz
    }
}
