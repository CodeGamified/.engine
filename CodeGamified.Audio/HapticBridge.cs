using System;

namespace CodeGamified.Audio
{
    /// <summary>
    /// Returns handler delegates you wire to CodeEditorWindow events.
    /// <code>
    /// var h = HapticBridge.CreateHandlers(myHapticProvider);
    /// editor.OnOptionSelected  += h.OptionSelected;
    /// editor.OnUndoPerformed   += h.UndoPerformed;
    /// editor.OnRedoPerformed   += h.RedoPerformed;
    /// editor.OnCompileError    += h.CompileError;
    /// editor.OnDocumentChanged += h.DocumentChanged;
    /// </code>
    /// </summary>
    public static class HapticBridge
    {
        public static Handlers CreateHandlers(IHapticProvider haptic)
        {
            return new Handlers
            {
                OptionSelected  = _ => haptic.TapLight(),
                UndoPerformed   = ()  => haptic.TapLight(),
                RedoPerformed   = ()  => haptic.TapLight(),
                CompileError    = _ => haptic.TapHeavy(),
                DocumentChanged = ()  => haptic.TapMedium(),
            };
        }

        public struct Handlers
        {
            public Action<string> OptionSelected;
            public Action         UndoPerformed;
            public Action         RedoPerformed;
            public Action<int>    CompileError;
            public Action         DocumentChanged;
        }
    }
}
