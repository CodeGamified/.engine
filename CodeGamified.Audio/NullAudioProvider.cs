namespace CodeGamified.Audio
{
    /// <summary>Silent default — all methods are no-ops.</summary>
    public sealed class NullAudioProvider : IAudioProvider
    {
        // Editor
        public void PlayTap() { }
        public void PlayInsert() { }
        public void PlayDelete() { }
        public void PlayUndo() { }
        public void PlayRedo() { }
        public void PlayCompileSuccess() { }
        public void PlayCompileError() { }
        public void PlayNavigate() { }

        // Engine
        public void PlayInstructionStep() { }
        public void PlayOutput() { }
        public void PlayHalted() { }
        public void PlayIOBlocked() { }
        public void PlayWaitStateChanged() { }

        // Time
        public void PlayWarpStart() { }
        public void PlayWarpCruise() { }
        public void PlayWarpDecelerate() { }
        public void PlayWarpArrived() { }
        public void PlayWarpCancelled() { }
        public void PlayWarpComplete() { }

        // Persistence
        public void PlaySaveStarted() { }
        public void PlaySaveCompleted() { }
        public void PlaySyncCompleted() { }
    }
}
