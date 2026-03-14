namespace CodeGamified.Audio
{
    /// <summary>Silent default — all methods are no-ops.</summary>
    public sealed class NullAudioProvider : IAudioProvider
    {
        public void PlayTap() { }
        public void PlayInsert() { }
        public void PlayDelete() { }
        public void PlayUndo() { }
        public void PlayRedo() { }
        public void PlayCompileSuccess() { }
        public void PlayCompileError() { }
        public void PlayNavigate() { }
    }
}
