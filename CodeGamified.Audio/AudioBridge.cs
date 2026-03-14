using System;

namespace CodeGamified.Audio
{
    /// <summary>
    /// Factory for time-scale-gated audio handler objects.
    /// Each handler group has a <see cref="GatedHandlers.MaxTimeScale"/> you can
    /// tune at runtime. When <c>getTimeScale()</c> exceeds the threshold the
    /// handler silently skips the provider call.
    /// <code>
    /// var editor  = AudioBridge.ForEditor(audio, () => Time.timeScale);
    /// var engine  = AudioBridge.ForEngine(audio, () => Time.timeScale);
    /// var time    = AudioBridge.ForTime(audio, () => Time.timeScale);
    /// var persist = AudioBridge.ForPersistence(audio, () => Time.timeScale);
    /// </code>
    /// </summary>
    public static class AudioBridge
    {
        public static EditorHandlers      ForEditor(IAudioProvider audio, Func<float> getTimeScale = null)      => new(audio, getTimeScale);
        public static EngineHandlers      ForEngine(IAudioProvider audio, Func<float> getTimeScale = null)      => new(audio, getTimeScale);
        public static TimeHandlers        ForTime(IAudioProvider audio, Func<float> getTimeScale = null)        => new(audio, getTimeScale);
        public static PersistenceHandlers ForPersistence(IAudioProvider audio, Func<float> getTimeScale = null) => new(audio, getTimeScale);

        // ─────────────────────────────────────────────────────────
        //  Base — shared time-scale gate
        // ─────────────────────────────────────────────────────────

        public abstract class GatedHandlers
        {
            /// <summary>
            /// Sounds in this group are skipped when
            /// <c>getTimeScale()</c> exceeds this value.
            /// Set to <see cref="float.MaxValue"/> to never gate.
            /// </summary>
            public float MaxTimeScale;

            readonly Func<float> _getTimeScale;

            protected GatedHandlers(Func<float> getTimeScale, float defaultMax)
            {
                _getTimeScale = getTimeScale;
                MaxTimeScale  = defaultMax;
            }

            protected bool Gated => _getTimeScale != null && _getTimeScale() > MaxTimeScale;
        }

        // ─────────────────────────────────────────────────────────
        //  Editor  (direct-wire: all events use Action / Action<T>)
        // ─────────────────────────────────────────────────────────

        public sealed class EditorHandlers : GatedHandlers
        {
            readonly IAudioProvider _a;
            internal EditorHandlers(IAudioProvider a, Func<float> ts) : base(ts, float.MaxValue) => _a = a;

            public void OptionSelected(string _) { if (!Gated) _a.PlayTap(); }
            public void UndoPerformed()           { if (!Gated) _a.PlayUndo(); }
            public void RedoPerformed()           { if (!Gated) _a.PlayRedo(); }
            public void CompileError(int _)       { if (!Gated) _a.PlayCompileError(); }
            public void DocumentChanged()         { if (!Gated) _a.PlayInsert(); }
        }

        // ─────────────────────────────────────────────────────────
        //  Engine  (default threshold 10 — step-mode only)
        //  Events with Engine types need one-line lambda wrappers.
        // ─────────────────────────────────────────────────────────

        public sealed class EngineHandlers : GatedHandlers
        {
            readonly IAudioProvider _a;
            internal EngineHandlers(IAudioProvider a, Func<float> ts) : base(ts, 10f) => _a = a;

            /// <summary>Wire: executor.OnInstructionExecuted += (_, _) => h.InstructionStep();</summary>
            public void InstructionStep()  { if (!Gated) _a.PlayInstructionStep(); }
            /// <summary>Wire: executor.OnOutput += _ => h.Output();</summary>
            public void Output()           { if (!Gated) _a.PlayOutput(); }
            /// <summary>Wire: executor.OnHalted += h.Halted;  (direct)</summary>
            public void Halted()           { if (!Gated) _a.PlayHalted(); }
            /// <summary>Wire: executor.OnIOBlocked += _ => h.IOBlocked();</summary>
            public void IOBlocked()        { if (!Gated) _a.PlayIOBlocked(); }
            /// <summary>Wire: executor.OnWaitStateChanged += (_, _) => h.WaitStateChanged();</summary>
            public void WaitStateChanged() { if (!Gated) _a.PlayWaitStateChanged(); }
        }

        // ─────────────────────────────────────────────────────────
        //  Time  (warp sounds — direct-wire where Action, lambda for WarpState)
        // ─────────────────────────────────────────────────────────

        public sealed class TimeHandlers : GatedHandlers
        {
            readonly IAudioProvider _a;
            internal TimeHandlers(IAudioProvider a, Func<float> ts) : base(ts, float.MaxValue) => _a = a;

            /// <summary>Call from OnWarpStateChanged switch on Accelerating.</summary>
            public void WarpStart()      { if (!Gated) _a.PlayWarpStart(); }
            /// <summary>Call from OnWarpStateChanged switch on Cruising.</summary>
            public void WarpCruise()     { if (!Gated) _a.PlayWarpCruise(); }
            /// <summary>Call from OnWarpStateChanged switch on Decelerating.</summary>
            public void WarpDecelerate() { if (!Gated) _a.PlayWarpDecelerate(); }
            /// <summary>Wire: warp.OnWarpArrived += h.WarpArrived;  (direct)</summary>
            public void WarpArrived()    { if (!Gated) _a.PlayWarpArrived(); }
            /// <summary>Wire: warp.OnWarpCancelled += h.WarpCancelled;  (direct)</summary>
            public void WarpCancelled()  { if (!Gated) _a.PlayWarpCancelled(); }
            /// <summary>Wire: warp.OnWarpComplete += h.WarpComplete;  (direct)</summary>
            public void WarpComplete()   { if (!Gated) _a.PlayWarpComplete(); }
        }

        // ─────────────────────────────────────────────────────────
        //  Persistence
        // ─────────────────────────────────────────────────────────

        public sealed class PersistenceHandlers : GatedHandlers
        {
            readonly IAudioProvider _a;
            internal PersistenceHandlers(IAudioProvider a, Func<float> ts) : base(ts, float.MaxValue) => _a = a;

            /// <summary>Wire: persistence.OnSaveStarted += h.SaveStarted;  (direct)</summary>
            public void SaveStarted()    { if (!Gated) _a.PlaySaveStarted(); }
            /// <summary>Wire: persistence.OnSaveCompleted += _ => h.SaveCompleted();</summary>
            public void SaveCompleted()  { if (!Gated) _a.PlaySaveCompleted(); }
            /// <summary>Wire: persistence.OnSyncCompleted += _ => h.SyncCompleted();</summary>
            public void SyncCompleted()  { if (!Gated) _a.PlaySyncCompleted(); }
        }
    }
}
