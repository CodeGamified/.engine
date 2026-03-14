// CodeGamified.Engine — Shared code execution framework
// MIT License

namespace CodeGamified.Engine
{
    /// <summary>
    /// A single machine instruction with operands.
    /// Immutable struct for efficient execution.
    /// Tag is an opaque int for game metadata (e.g. CrewRole in SeaRauber).
    /// </summary>
    public readonly struct Instruction
    {
        public readonly OpCode Op;
        public readonly int Arg0;
        public readonly int Arg1;
        public readonly int Arg2;

        /// <summary>Source line number in original Python code (for visualization)</summary>
        public readonly int SourceLine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD || CODEGAMIFIED_DEBUG
        /// <summary>Human-readable description of this instruction (editor/debug only)</summary>
        public readonly string Comment;
#endif

        /// <summary>Opaque game-specific metadata (e.g. CrewRole cast to int)</summary>
        public readonly int Tag;

        public Instruction(OpCode op, int arg0 = 0, int arg1 = 0, int arg2 = 0,
                          int sourceLine = -1, string comment = null, int tag = 0)
        {
            Op = op;
            Arg0 = arg0;
            Arg1 = arg1;
            Arg2 = arg2;
            SourceLine = sourceLine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CODEGAMIFIED_DEBUG
            Comment = comment;
#endif
            Tag = tag;
        }

        /// <summary>Get comment string (empty in release builds).</summary>
        public string GetComment()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CODEGAMIFIED_DEBUG
            return Comment;
#else
            return null;
#endif
        }

        public string ToAssembly()
        {
            return Op switch
            {
                OpCode.LOAD_CONST => $"LOAD_CONST R{Arg0}, {Arg1}",
                OpCode.LOAD_FLOAT => $"LOAD_FLOAT R{Arg0}, #{Arg1}",
                OpCode.LOAD_MEM => $"LOAD_MEM R{Arg0}, [{Arg1}]",
                OpCode.STORE_MEM => $"STORE_MEM [{Arg1}], R{Arg0}",
                OpCode.MOV => $"MOV R{Arg0}, R{Arg1}",
                OpCode.ADD => $"ADD R{Arg0}, R{Arg1}",
                OpCode.SUB => $"SUB R{Arg0}, R{Arg1}",
                OpCode.MUL => $"MUL R{Arg0}, R{Arg1}",
                OpCode.DIV => $"DIV R{Arg0}, R{Arg1}",
                OpCode.MOD => $"MOD R{Arg0}, R{Arg1}",
                OpCode.INC => $"INC R{Arg0}",
                OpCode.DEC => $"DEC R{Arg0}",
                OpCode.CMP => $"CMP R{Arg0}, R{Arg1}",
                OpCode.JMP => $"JMP @{Arg0}",
                OpCode.JEQ => $"JEQ @{Arg0}",
                OpCode.JNE => $"JNE @{Arg0}",
                OpCode.JLT => $"JLT @{Arg0}",
                OpCode.JGT => $"JGT @{Arg0}",
                OpCode.JLE => $"JLE @{Arg0}",
                OpCode.JGE => $"JGE @{Arg0}",
                OpCode.PUSH => $"PUSH R{Arg0}",
                OpCode.POP => $"POP R{Arg0}",
                OpCode.CALL => $"CALL @{Arg0}",
                OpCode.RET => "RET",
                OpCode.WAIT => $"WAIT R{Arg0}",
                OpCode.NOP => "NOP",
                OpCode.HALT => "HALT",
                OpCode.BREAK => "BREAK",
                _ => $"CUSTOM_{(int)Op - 100} {Arg0}, {Arg1}, {Arg2}"
            };
        }

        public override string ToString() => ToAssembly();
    }
}
