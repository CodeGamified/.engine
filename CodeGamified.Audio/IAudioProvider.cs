namespace CodeGamified.Audio
{
    /// <summary>
    /// Game-specific audio playback. Implement with your own AudioClip assets.
    /// Every hookable event across all modules has a slot — leave methods
    /// empty for events you don't want sound on.
    /// </summary>
    public interface IAudioProvider
    {
        // ── Editor ──────────────────────────────────────────────
        void PlayTap();              // option selected
        void PlayInsert();           // statement inserted
        void PlayDelete();           // statement deleted
        void PlayUndo();             // undo
        void PlayRedo();             // redo
        void PlayCompileSuccess();   // valid program compiled
        void PlayCompileError();     // compilation failed
        void PlayNavigate();         // cursor moved / option drilled into

        // ── Engine ──────────────────────────────────────────────
        void PlayInstructionStep();  // single instruction executed
        void PlayOutput();           // game event emitted
        void PlayHalted();           // program stopped
        void PlayIOBlocked();        // I/O handler rejected instruction
        void PlayWaitStateChanged(); // executor entered/exited wait

        // ── Time ────────────────────────────────────────────────
        void PlayWarpStart();        // warp begins (accelerating)
        void PlayWarpCruise();       // reached cruise speed
        void PlayWarpDecelerate();   // decelerating toward target
        void PlayWarpArrived();      // arrived at target time
        void PlayWarpCancelled();    // warp cancelled
        void PlayWarpComplete();     // hold elapsed, back to idle

        // ── Persistence ─────────────────────────────────────────
        void PlaySaveStarted();      // save began
        void PlaySaveCompleted();    // save finished
        void PlaySyncCompleted();    // sync finished
    }
}
