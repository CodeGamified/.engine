// CodeGamified.Engine — Shared code execution framework
// MIT License
using System.Collections.Generic;

namespace CodeGamified.Engine.Compiler
{
    /// <summary>
    /// Compiler context: instruction buffer, variable allocation, jump patching,
    /// object type tracking. Games extend via ICompilerExtension + Metadata dict.
    /// </summary>
    public class CompilerContext
    {
        public List<Instruction> Instructions = new List<Instruction>();
        public Dictionary<string, int> Variables = new Dictionary<string, int>();
        private int _nextVarAddress = 0;

        /// <summary>Float constant table — avoids *1000 int truncation.</summary>
        public List<float> FloatConstants = new List<float>();

        // ═══════════════════════════════════════════════════════════════
        // OOP OBJECT TRACKING
        // ═══════════════════════════════════════════════════════════════

        public Dictionary<string, string> ObjectTypes = new Dictionary<string, string>();
        public Dictionary<string, int> ObjectChannels = new Dictionary<string, int>();
        public Dictionary<string, int> ObjectSensorIds = new Dictionary<string, int>();
        private int _nextChannel = 0;
        private int _nextSensorId = 0;

        // ═══════════════════════════════════════════════════════════════
        // GAME EXTENSION
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Game-specific compiler extension (builtins, method calls).</summary>
        public ICompilerExtension Extension;

        /// <summary>Game-specific compilation errors.</summary>
        public List<string> Errors = new List<string>();

        /// <summary>Opaque game metadata (tier level, etc).</summary>
        public readonly Dictionary<string, object> Metadata = new Dictionary<string, object>();

        /// <summary>Known object type names that the parser should recognize as declarations.</summary>
        public readonly HashSet<string> KnownTypes = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

        // ═══════════════════════════════════════════════════════════════
        // EMIT
        // ═══════════════════════════════════════════════════════════════

        public int CurrentAddress => Instructions.Count;

        public void Emit(OpCode op, int arg0 = 0, int arg1 = 0, int arg2 = 0,
                        int sourceLine = -1, string comment = null, int tag = 0)
        {
            Instructions.Add(new Instruction(op, arg0, arg1, arg2, sourceLine, comment, tag));
        }

        public int GetVariableAddress(string name)
        {
            if (!Variables.TryGetValue(name, out int addr))
            {
                addr = _nextVarAddress++;
                Variables[name] = addr;
            }
            return addr;
        }

        public void PatchJump(int instructionIndex, int targetAddress)
        {
            if (instructionIndex >= 0 && instructionIndex < Instructions.Count)
            {
                var old = Instructions[instructionIndex];
                Instructions[instructionIndex] = new Instruction(
                    old.Op, targetAddress, old.Arg1, old.Arg2, old.SourceLine, old.Comment, old.Tag);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // OBJECT HELPERS
        // ═══════════════════════════════════════════════════════════════

        public void RegisterObject(string name, string typeName, List<AstNodes.ExprNode> constructorArgs)
        {
            ObjectTypes[name] = typeName;
        }

        public string GetObjectType(string name) =>
            ObjectTypes.TryGetValue(name, out string type) ? type : "Unknown";

        public int GetObjectChannel(string name) =>
            ObjectChannels.TryGetValue(name, out int ch) ? ch : 0;

        public int AllocateChannel(string name)
        {
            int ch = _nextChannel++;
            ObjectChannels[name] = ch;
            return ch;
        }

        public int GetObjectSensorId(string name) =>
            ObjectSensorIds.TryGetValue(name, out int id) ? id : 0;

        public int AllocateSensorId(string name)
        {
            int id = _nextSensorId++;
            ObjectSensorIds[name] = id;
            return id;
        }

        /// <summary>Report a compilation error.</summary>
        public void Error(int line, string message)
        {
            Errors.Add($"Line {line}: {message}");
        }

        /// <summary>Add a float constant, return its index.</summary>
        public int AddFloatConstant(float value)
        {
            // Reuse existing constant if present
            for (int i = 0; i < FloatConstants.Count; i++)
                if (FloatConstants[i] == value) return i;
            FloatConstants.Add(value);
            return FloatConstants.Count - 1;
        }
    }
}
