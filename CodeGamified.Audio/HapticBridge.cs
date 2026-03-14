using System;

namespace CodeGamified.Audio
{
    /// <summary>
    /// Factory for time-scale-gated haptic handler objects.
    /// Maps module events → haptic intensity levels.
    /// <code>
    /// var editor  = HapticBridge.ForEditor(haptic, () => Time.timeScale);
    /// var engine  = HapticBridge.ForEngine(haptic, () => Time.timeScale);
    /// var time    = HapticBridge.ForTime(haptic, () => Time.timeScale);
    /// var persist = HapticBridge.ForPersistence(haptic, () => Time.timeScale);
    /// </code>
    /// </summary>
    public static class HapticBridge
    {
        public static EditorHandlers      ForEditor(IHapticProvider haptic, Func<float> getTimeScale = null)      => new(haptic, getTimeScale);
        public static EngineHandlers      ForEngine(IHapticProvider haptic, Func<float> getTimeScale = null)      => new(haptic, getTimeScale);
        public static TimeHandlers        ForTime(IHapticProvider haptic, Func<float> getTimeScale = null)        => new(haptic, getTimeScale);
        public static PersistenceHandlers ForPersistence(IHapticProvider haptic, Func<float> getTimeScale = null) => new(haptic, getTimeScale);

        // ─────────────────────────────────────────────────────────

        public abstract class GatedHandlers
        {
            public float MaxTimeScale;
            readonly Func<float> _getTimeScale;

            protected GatedHandlers(Func<float> getTimeScale, float defaultMax)
            {
                _getTimeScale = getTimeScale;
                MaxTimeScale  = defaultMax;
            }

            protected bool Gated => _getTimeScale != null && _getTimeScale() > MaxTimeScale;
        }

        // ── Editor ──────────────────────────────────────────────

        public sealed class EditorHandlers : GatedHandlers
        {
            readonly IHapticProvider _h;
            internal EditorHandlers(IHapticProvider h, Func<float> ts) : base(ts, float.MaxValue) => _h = h;

            public void OptionSelected(string _) { if (!Gated) _h.TapLight(); }
            public void UndoPerformed()           { if (!Gated) _h.TapLight(); }
            public void RedoPerformed()           { if (!Gated) _h.TapLight(); }
            public void CompileError(int _)       { if (!Gated) _h.TapHeavy(); }
            public void DocumentChanged()         { if (!Gated) _h.TapMedium(); }
        }

        // ── Engine (default threshold 10) ───────────────────────

        public sealed class EngineHandlers : GatedHandlers
        {
            readonly IHapticProvider _h;
            internal EngineHandlers(IHapticProvider h, Func<float> ts) : base(ts, 10f) => _h = h;

            public void InstructionStep()  { if (!Gated) _h.TapLight(); }
            public void Output()           { if (!Gated) _h.TapLight(); }
            public void Halted()           { if (!Gated) _h.TapMedium(); }
            public void IOBlocked()        { if (!Gated) _h.TapMedium(); }
            public void WaitStateChanged() { if (!Gated) _h.TapLight(); }
        }

        // ── Time ────────────────────────────────────────────────

        public sealed class TimeHandlers : GatedHandlers
        {
            readonly IHapticProvider _h;
            internal TimeHandlers(IHapticProvider h, Func<float> ts) : base(ts, float.MaxValue) => _h = h;

            public void WarpStart()      { if (!Gated) _h.TapMedium(); }
            public void WarpCruise()     { if (!Gated) _h.TapLight(); }
            public void WarpDecelerate() { if (!Gated) _h.TapMedium(); }
            public void WarpArrived()    { if (!Gated) _h.TapHeavy(); }
            public void WarpCancelled()  { if (!Gated) _h.TapMedium(); }
            public void WarpComplete()   { if (!Gated) _h.TapLight(); }
        }

        // ── Persistence ─────────────────────────────────────────

        public sealed class PersistenceHandlers : GatedHandlers
        {
            readonly IHapticProvider _h;
            internal PersistenceHandlers(IHapticProvider h, Func<float> ts) : base(ts, float.MaxValue) => _h = h;

            public void SaveStarted()    { if (!Gated) _h.TapLight(); }
            public void SaveCompleted()  { if (!Gated) _h.TapLight(); }
            public void SyncCompleted()  { if (!Gated) _h.TapLight(); }
        }
    }
}
