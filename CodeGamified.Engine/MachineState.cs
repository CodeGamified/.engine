// CodeGamified.Engine — Shared code execution framework
// MIT License
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CodeGamified.Engine
{
    /// <summary>
    /// Complete machine state: registers, stack, heap, PC, flags.
    /// Game-specific state lives in GameData dictionary.
    /// </summary>
    public class MachineState
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        public const int REGISTER_COUNT = 8;
        public const int MAX_STACK = 64;
        public const int MAX_MEMORY = 256;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════

        public readonly float[] Registers = new float[REGISTER_COUNT];
        public readonly Stack<float> Stack = new Stack<float>(MAX_STACK);
        public readonly Dictionary<string, float> Memory = new Dictionary<string, float>();
        public readonly Dictionary<int, string> MemoryNames = new Dictionary<int, string>();
        public readonly Dictionary<string, int> NameToAddress = new Dictionary<string, int>();
        private int _nextAddress = 0;

        public int PC { get; set; } = 0;
        public CpuFlags Flags { get; set; } = CpuFlags.None;
        public bool IsHalted { get; set; } = false;
        public bool IsWaiting { get; set; } = false;
        public float WaitTimeRemaining { get; set; } = 0f;
        public long InstructionsExecuted { get; set; } = 0;
        public long CycleCount { get; set; } = 0;

        // ═══════════════════════════════════════════════════════════════
        // EXECUTION TRACE (for visualization)
        // ═══════════════════════════════════════════════════════════════

        public Instruction? LastInstruction { get; set; }
        public int LastMemoryAccess { get; set; } = -1;
        public bool LastMemoryWasWrite { get; set; }
        public int LastRegisterModified { get; set; } = -1;
        public Queue<GameEvent> OutputEvents { get; } = new Queue<GameEvent>();

        // ═══════════════════════════════════════════════════════════════
        // GAME-SPECIFIC EXTENSION DATA
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Opaque storage for game-specific state (crew dispatch, sensor ids, etc).
        /// Games cast values to their own types.
        /// </summary>
        public readonly Dictionary<string, object> GameData = new Dictionary<string, object>();

        // ═══════════════════════════════════════════════════════════════
        // MEMORY MANAGEMENT
        // ═══════════════════════════════════════════════════════════════

        public int GetOrAllocateAddress(string name)
        {
            if (NameToAddress.TryGetValue(name, out int addr))
                return addr;

            addr = _nextAddress++;
            NameToAddress[name] = addr;
            MemoryNames[addr] = name;
            Memory[name] = 0f;
            return addr;
        }

        public float ReadMemory(int address)
        {
            LastMemoryAccess = address;
            LastMemoryWasWrite = false;
            if (MemoryNames.TryGetValue(address, out string name))
                return Memory.TryGetValue(name, out float val) ? val : 0f;
            return 0f;
        }

        public float ReadMemory(string name)
        {
            if (NameToAddress.TryGetValue(name, out int addr))
                LastMemoryAccess = addr;
            LastMemoryWasWrite = false;
            return Memory.TryGetValue(name, out float val) ? val : 0f;
        }

        public void WriteMemory(int address, float value)
        {
            LastMemoryAccess = address;
            LastMemoryWasWrite = true;
            if (MemoryNames.TryGetValue(address, out string name))
            {
                Memory[name] = value;
            }
            else
            {
                string autoName = $"_mem{address}";
                MemoryNames[address] = autoName;
                NameToAddress[autoName] = address;
                Memory[autoName] = value;
            }
        }

        public void WriteMemory(string name, float value)
        {
            GetOrAllocateAddress(name);
            if (NameToAddress.TryGetValue(name, out int addr))
            {
                LastMemoryAccess = addr;
                LastMemoryWasWrite = true;
            }
            Memory[name] = value;
        }

        // ═══════════════════════════════════════════════════════════════
        // REGISTER ACCESS
        // ═══════════════════════════════════════════════════════════════

        public float GetRegister(int index)
        {
            if (index < 0 || index >= REGISTER_COUNT)
            {
                Debug.LogError($"[MachineState] Invalid register R{index}");
                return 0f;
            }
            return Registers[index];
        }

        public void SetRegister(int index, float value)
        {
            if (index < 0 || index >= REGISTER_COUNT)
            {
                Debug.LogError($"[MachineState] Invalid register R{index}");
                return;
            }
            Registers[index] = value;
            LastRegisterModified = index;
        }

        // ═══════════════════════════════════════════════════════════════
        // FLAG MANAGEMENT
        // ═══════════════════════════════════════════════════════════════

        public void SetCompareFlags(float a, float b)
        {
            float diff = a - b;
            Flags = CpuFlags.None;
            if (Mathf.Approximately(diff, 0f))
                Flags |= CpuFlags.Zero;
            if (diff < 0f)
                Flags |= CpuFlags.Negative;
        }

        public bool IsZero => (Flags & CpuFlags.Zero) != 0;
        public bool IsNegative => (Flags & CpuFlags.Negative) != 0;

        // ═══════════════════════════════════════════════════════════════
        // RESET
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reset core machine state. Does NOT clear GameData — games manage their own.
        /// </summary>
        public void Reset()
        {
            for (int i = 0; i < REGISTER_COUNT; i++)
                Registers[i] = 0f;

            Stack.Clear();
            Memory.Clear();
            MemoryNames.Clear();
            NameToAddress.Clear();
            _nextAddress = 0;

            PC = 0;
            Flags = CpuFlags.None;
            IsHalted = false;
            IsWaiting = false;
            WaitTimeRemaining = 0f;
            InstructionsExecuted = 0;
            CycleCount = 0;

            LastInstruction = null;
            LastMemoryAccess = -1;
            LastRegisterModified = -1;
            OutputEvents.Clear();
        }

        // ═══════════════════════════════════════════════════════════════
        // VISUALIZATION
        // ═══════════════════════════════════════════════════════════════

        public string ToDebugString()
        {
            var sb = new StringBuilder();

            sb.AppendLine("═══ REGISTERS ═══");
            for (int i = 0; i < REGISTER_COUNT; i++)
            {
                string highlight = (i == LastRegisterModified) ? "►" : " ";
                sb.AppendLine($"{highlight}R{i}: {Registers[i]:F2}");
            }

            sb.AppendLine($"\nFLAGS: {Flags}");
            sb.AppendLine($"PC: {PC}");

            sb.AppendLine("\n═══ STACK ═══");
            if (Stack.Count == 0)
                sb.AppendLine("  [empty]");
            else
                foreach (var val in Stack)
                    sb.AppendLine($"  {val:F2}");

            sb.AppendLine("\n═══ MEMORY ═══");
            foreach (var kvp in Memory)
            {
                int addr = NameToAddress.TryGetValue(kvp.Key, out int a) ? a : -1;
                string highlight = (addr == LastMemoryAccess) ? "►" : " ";
                sb.AppendLine($"{highlight}{kvp.Key}: {kvp.Value:F2}");
            }

            return sb.ToString();
        }
    }
}
