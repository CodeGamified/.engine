// CodeGamified.Engine — Shared code execution framework
// MIT License
using System.Collections.Generic;

namespace CodeGamified.Engine.Compiler
{
    /// <summary>
    /// Interface for game-specific compiler extensions.
    /// Games register builtins (beep, bell, get_wind, etc.) and handle
    /// method calls on declared objects.
    /// </summary>
    public interface ICompilerExtension
    {
        /// <summary>
        /// Register built-in functions and known object types.
        /// Called once before compilation begins.
        /// </summary>
        void RegisterBuiltins(CompilerContext ctx);

        /// <summary>
        /// Try to compile a standalone function call (beep(), bell(), wait(), etc.).
        /// Return true if handled, false to fall through to engine default.
        /// </summary>
        bool TryCompileCall(string functionName, List<AstNodes.ExprNode> args,
                           CompilerContext ctx, int sourceLine);

        /// <summary>
        /// Try to compile a method call on a declared object (radio.beep(), helm.set_heading()).
        /// Return true if handled.
        /// </summary>
        bool TryCompileMethodCall(string objectName, string methodName,
                                  List<AstNodes.ExprNode> args,
                                  CompilerContext ctx, int sourceLine);

        /// <summary>
        /// Try to compile an object declaration (Radio radio = new Radio()).
        /// Return true if handled.
        /// </summary>
        bool TryCompileObjectDecl(string typeName, string varName,
                                  List<AstNodes.ExprNode> constructorArgs,
                                  CompilerContext ctx, int sourceLine);

        /// <summary>
        /// Optional: called when a while loop is encountered.
        /// Use for tier gating (e.g. "while loops require ChartTable tier").
        /// </summary>
        void OnWhileLoop(CompilerContext ctx, int sourceLine) { }

        /// <summary>
        /// Optional: called when a for loop is encountered.
        /// Use for tier gating (e.g. "for loops require NavigatorsOffice tier").
        /// </summary>
        void OnForLoop(CompilerContext ctx, int sourceLine) { }
    }
}
