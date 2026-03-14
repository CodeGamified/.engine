// CodeGamified.Engine — Shared code execution framework
// MIT License
using System;

namespace CodeGamified.Engine
{
    /// <summary>
    /// Core RISC-like opcodes for the code execution engine.
    /// Games extend I/O via CUSTOM_0..CUSTOM_31.
    /// </summary>
    public enum OpCode
    {
        // ═══════════════════════════════════════════════════════════════
        // DATA MOVEMENT
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Load immediate value into register (legacy *1000 scaling). Args: [destReg, value]</summary>
        LOAD_CONST,

        /// <summary>Load float from constant table by index. Args: [destReg, constIndex]</summary>
        LOAD_FLOAT,

        /// <summary>Load from memory address into register. Args: [destReg, address]</summary>
        LOAD_MEM,

        /// <summary>Store register value to memory address. Args: [srcReg, address]</summary>
        STORE_MEM,

        /// <summary>Copy register to register. Args: [destReg, srcReg]</summary>
        MOV,

        // ═══════════════════════════════════════════════════════════════
        // ARITHMETIC
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Add two registers, store in first. Args: [destReg, srcReg]</summary>
        ADD,

        /// <summary>Subtract second from first, store in first. Args: [destReg, srcReg]</summary>
        SUB,

        /// <summary>Multiply two registers, store in first. Args: [destReg, srcReg]</summary>
        MUL,

        /// <summary>Divide first by second, store in first. Args: [destReg, srcReg]</summary>
        DIV,

        /// <summary>Modulo first by second, store in first. Args: [destReg, srcReg]</summary>
        MOD,

        /// <summary>Increment register by 1. Args: [reg]</summary>
        INC,

        /// <summary>Decrement register by 1. Args: [reg]</summary>
        DEC,

        // ═══════════════════════════════════════════════════════════════
        // COMPARISON & CONTROL FLOW
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Compare two registers, set flags. Args: [reg1, reg2]</summary>
        CMP,

        /// <summary>Unconditional jump. Args: [address]</summary>
        JMP,

        /// <summary>Jump if equal (zero flag set). Args: [address]</summary>
        JEQ,

        /// <summary>Jump if not equal. Args: [address]</summary>
        JNE,

        /// <summary>Jump if less than. Args: [address]</summary>
        JLT,

        /// <summary>Jump if greater than. Args: [address]</summary>
        JGT,

        /// <summary>Jump if less than or equal. Args: [address]</summary>
        JLE,

        /// <summary>Jump if greater than or equal. Args: [address]</summary>
        JGE,

        // ═══════════════════════════════════════════════════════════════
        // STACK OPERATIONS
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Push register onto stack. Args: [reg]</summary>
        PUSH,

        /// <summary>Pop from stack into register. Args: [reg]</summary>
        POP,

        /// <summary>Call subroutine (push PC, jump). Args: [address]</summary>
        CALL,

        /// <summary>Return from subroutine (pop PC). Args: none</summary>
        RET,

        // ═══════════════════════════════════════════════════════════════
        // TIMING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Wait for simulation time. Args: [durationReg]</summary>
        WAIT,

        // ═══════════════════════════════════════════════════════════════
        // SYSTEM
        // ═══════════════════════════════════════════════════════════════

        /// <summary>No operation. Args: none</summary>
        NOP,

        /// <summary>Halt execution. Args: none</summary>
        HALT,

        /// <summary>Debug breakpoint. Args: none</summary>
        BREAK,

        // ═══════════════════════════════════════════════════════════════
        // GAME-SPECIFIC I/O — Games cast to their own enums
        // Reserve 32 slots so enum values stay stable.
        // ═══════════════════════════════════════════════════════════════

        CUSTOM_0 = 100,
        CUSTOM_1, CUSTOM_2, CUSTOM_3, CUSTOM_4,
        CUSTOM_5, CUSTOM_6, CUSTOM_7, CUSTOM_8,
        CUSTOM_9, CUSTOM_10, CUSTOM_11, CUSTOM_12,
        CUSTOM_13, CUSTOM_14, CUSTOM_15, CUSTOM_16,
        CUSTOM_17, CUSTOM_18, CUSTOM_19, CUSTOM_20,
        CUSTOM_21, CUSTOM_22, CUSTOM_23, CUSTOM_24,
        CUSTOM_25, CUSTOM_26, CUSTOM_27, CUSTOM_28,
        CUSTOM_29, CUSTOM_30, CUSTOM_31
    }
}
