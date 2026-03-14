// CodeGamified.Editor — Tap-to-code editor for mobile
// MIT License
using System.Collections.Generic;

namespace CodeGamified.Editor
{
    /// <summary>
    /// Game-specific extension for the editor option tree.
    /// Provides available types, functions, and methods for the current game context.
    /// Complements ICompilerExtension (which handles compilation) —
    /// this handles the UI/option side.
    /// </summary>
    public interface IEditorExtension
    {
        /// <summary>Object types available for declaration (filtered by tier/progression).</summary>
        List<EditorTypeInfo> GetAvailableTypes();

        /// <summary>Standalone functions available beyond engine builtins.</summary>
        List<EditorFuncInfo> GetAvailableFunctions();

        /// <summary>Methods callable on a declared object of the given type.</summary>
        List<EditorMethodInfo> GetMethodsForType(string typeName);

        /// <summary>
        /// Game-contextual variable name suggestions (#6).
        /// Return domain-specific names like "heading", "signal", "radiation"
        /// instead of generic x, y, z. Return empty to use defaults.
        /// </summary>
        List<string> GetVariableNameSuggestions() => new();

        /// <summary>
        /// Whether while loops are allowed at the current progression tier (#2).
        /// Default: true.
        /// </summary>
        bool IsWhileLoopAllowed() => true;

        /// <summary>
        /// Whether for loops are allowed at the current progression tier (#2).
        /// Default: true.
        /// </summary>
        bool IsForLoopAllowed() => true;

        /// <summary>
        /// Reason string when a construct is gated (#2).
        /// e.g. "requires Chart Table tier"
        /// </summary>
        string GetWhileLoopGateReason() => "";
        string GetForLoopGateReason() => "";

        /// <summary>
        /// Game-contextual string literal suggestions for the value picker.
        /// Return domain-specific phrases like "SOS", "CQ" etc.
        /// </summary>
        List<string> GetStringLiteralSuggestions() => new();
    }

    public struct EditorTypeInfo
    {
        public string Name;
        public string Hint;
    }

    public struct EditorFuncInfo
    {
        public string Name;
        public string Hint;
        public int ArgCount;
    }

    public struct EditorMethodInfo
    {
        public string Name;
        public string Hint;
        public int ArgCount;
        public bool HasReturnValue;
    }
}
