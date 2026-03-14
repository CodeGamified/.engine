using System;

namespace CodeGamified.Audio
{
    /// <summary>
    /// Returns handler delegates you wire to CodeEditorWindow events.
    /// Audio package stays zero-dep — the game project does the += wiring.
    /// <code>
    /// var h = AudioBridge.CreateHandlers(myAudioProvider);
    /// editor.OnOptionSelected  += h.OptionSelected;
    /// editor.OnUndoPerformed   += h.UndoPerformed;
    /// editor.OnRedoPerformed   += h.RedoPerformed;
    /// editor.OnCompileError    += h.CompileError;
    /// editor.OnDocumentChanged += h.DocumentChanged;
    /// </code>
    /// </summary>
    public static class AudioBridge
    {
        public static Handlers CreateHandlers(IAudioProvider audio)
        {
            return new Handlers
            {
                OptionSelected  = _ => audio.PlayTap(),
                UndoPerformed   = ()  => audio.PlayUndo(),
                RedoPerformed   = ()  => audio.PlayRedo(),
                CompileError    = _ => audio.PlayCompileError(),
                DocumentChanged = ()  => audio.PlayInsert(),
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
