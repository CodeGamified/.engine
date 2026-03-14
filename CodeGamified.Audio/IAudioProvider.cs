namespace CodeGamified.Audio
{
    /// <summary>
    /// Game-specific audio playback. Implement with your own AudioClip assets.
    /// </summary>
    public interface IAudioProvider
    {
        void PlayTap();              // option selected
        void PlayInsert();           // statement inserted
        void PlayDelete();           // statement deleted
        void PlayUndo();             // undo
        void PlayRedo();             // redo
        void PlayCompileSuccess();   // valid program compiled
        void PlayCompileError();     // compilation failed
        void PlayNavigate();         // cursor moved / option drilled into
    }
}
