// CodeGamified.Engine — Shared code execution framework
// MIT License
using System.Collections.Generic;
using System.Text;

namespace CodeGamified.Engine
{
    /// <summary>
    /// A compiled program ready for execution.
    /// </summary>
    public class CompiledProgram
    {
        public string Name;
        public string SourceCode;
        public string[] SourceLines;
        public Instruction[] Instructions;
        public Dictionary<string, int> Variables;

        /// <summary>Float constant table for LOAD_FLOAT instructions</summary>
        public float[] FloatConstants;

        /// <summary>String constant table for StringNode values</summary>
        public string[] StringConstants;

        /// <summary>Declared objects (name → type) for visualization</summary>
        public Dictionary<string, string> DeclaredObjects = new Dictionary<string, string>();

        /// <summary>Game-specific compilation metadata</summary>
        public readonly Dictionary<string, object> Metadata = new Dictionary<string, object>();

        /// <summary>Compilation errors from game-specific validation</summary>
        public List<string> Errors = new List<string>();

        /// <summary>Did compilation succeed without errors?</summary>
        public bool IsValid => Errors.Count == 0;

        public string ToAssemblyListing()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"; {Name}");
            sb.AppendLine($"; {Instructions.Length} instructions");
            if (Errors.Count > 0)
            {
                sb.AppendLine($"; ⚠ {Errors.Count} ERRORS:");
                foreach (var err in Errors)
                    sb.AppendLine($";   {err}");
            }
            sb.AppendLine();

            for (int i = 0; i < Instructions.Length; i++)
            {
                var inst = Instructions[i];
                string addr = i.ToString("X3");
                string asm = inst.ToAssembly().PadRight(24);
                string tag = inst.Tag != 0 ? $"[tag:{inst.Tag}]" : "";
                string comment = !string.IsNullOrEmpty(inst.GetComment()) ? $"; {inst.GetComment()}" : "";
                sb.AppendLine($"{addr}: {asm} {tag,10} {comment}");
            }

            return sb.ToString();
        }
    }
}
