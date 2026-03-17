// CodeGamified.Engine — Shared code execution framework
// MIT License
using System;
using UnityEngine;

namespace CodeGamified.Engine
{
    /// <summary>
    /// Time-scale aware bytecode executor.
    /// Step-through at low time scales, batch at high.
    /// Game-specific I/O delegated to IGameIOHandler.
    /// </summary>
    public class CodeExecutor
    {
        // ═══════════════════════════════════════════════════════════════
        // CONFIGURATION
        // ═══════════════════════════════════════════════════════════════

        public float StepThroughThreshold = 10f;
        public int MaxBatchSize = 1000;
        public float StepDelay = 0.1f;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════

        public CompiledProgram Program { get; private set; }
        public MachineState State { get; private set; } = new MachineState();

        private float _timeSinceLastStep = 0f;

        public bool IsRunning => Program != null && !State.IsHalted;
        public bool IsStepMode => (_ioHandler?.GetTimeScale() ?? 1f) < StepThroughThreshold;

        // ═══════════════════════════════════════════════════════════════
        // GAME I/O
        // ═══════════════════════════════════════════════════════════════

        private IGameIOHandler _ioHandler;

        /// <summary>Set the game-specific I/O handler.</summary>
        public void SetIOHandler(IGameIOHandler handler) => _ioHandler = handler;

        // ═══════════════════════════════════════════════════════════════
        // EVENTS
        // ═══════════════════════════════════════════════════════════════

        public event Action<Instruction, MachineState> OnInstructionExecuted;
        public event Action<GameEvent> OnOutput;
        public event Action OnHalted;
        public event Action<bool, float> OnWaitStateChanged;

        /// <summary>
        /// Fired when PreExecute returns false (e.g. crew dispatch failed).
        /// Game layer can cast inst.Tag to interpret the failure.
        /// </summary>
        public event Action<Instruction> OnIOBlocked;

        // ═══════════════════════════════════════════════════════════════
        // PROGRAM LOADING
        // ═══════════════════════════════════════════════════════════════

        public void LoadProgram(CompiledProgram program)
        {
            Program = program;
            State.Reset();

            foreach (var kvp in program.Variables)
                State.GetOrAllocateAddress(kvp.Key);

            Debug.Log($"[CodeExecutor] Loaded: {program.Name} ({program.Instructions.Length} instructions)");
        }

        // ═══════════════════════════════════════════════════════════════
        // TICK-BASED EXECUTION (deterministic, budget-limited)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Execute one simulation tick with a fixed instruction budget.
        /// Resets PC to 0 (re-runs program from top). Memory persists.
        /// Deterministic: same budget = same results regardless of time scale.
        /// </summary>
        public int ExecuteTick(int budget)
        {
            if (Program == null) return 0;

            // Reset PC — each tick is a fresh run of the script
            State.PC = 0;
            State.IsHalted = false;
            State.IsWaiting = false;

            int executed = 0;
            for (int i = 0; i < budget && !State.IsHalted; i++)
            {
                ExecuteOne();
                executed++;
            }

            return executed;
        }

        // ═══════════════════════════════════════════════════════════════
        // CONTINUOUS EXECUTION (time-scale aware, for SeaRäuber/BitNaughts)
        // ═══════════════════════════════════════════════════════════════

        public int Update(float deltaTime)
        {
            if (Program == null || State.IsHalted)
                return 0;

            // Handle WAIT instruction
            if (State.IsWaiting)
            {
                float timeScale = _ioHandler?.GetTimeScale() ?? 1f;
                State.WaitTimeRemaining -= deltaTime * timeScale;

                if (State.WaitTimeRemaining <= 0f)
                {
                    State.IsWaiting = false;
                    State.WaitTimeRemaining = 0f;
                    State.PC++;
                    OnWaitStateChanged?.Invoke(false, 0f);
                }
                return 0;
            }

            float scale = _ioHandler?.GetTimeScale() ?? 1f;

            if (scale < StepThroughThreshold)
            {
                _timeSinceLastStep += deltaTime;
                if (_timeSinceLastStep >= StepDelay)
                {
                    _timeSinceLastStep = 0f;
                    ExecuteOne();
                    return 1;
                }
                return 0;
            }
            else
            {
                // Run until WAIT, HALT, or MaxBatchSize — WAIT is the real throttle,
                // not an artificial instruction budget.
                int executed = 0;

                for (int i = 0; i < MaxBatchSize && !State.IsHalted && !State.IsWaiting; i++)
                {
                    ExecuteOne();
                    executed++;
                }

                return executed;
            }
        }

        public void ExecuteOne()
        {
            if (Program == null || State.IsHalted || State.IsWaiting)
                return;

            if (State.PC < 0 || State.PC >= Program.Instructions.Length)
            {
                State.IsHalted = true;
                OnHalted?.Invoke();
                return;
            }

            var inst = Program.Instructions[State.PC];

            // Game-specific pre-execution gate (e.g. crew dispatch)
            if (inst.Op >= OpCode.CUSTOM_0 && _ioHandler != null)
            {
                if (!_ioHandler.PreExecute(inst, State))
                {
                    OnIOBlocked?.Invoke(inst);
                    State.LastExecutedPC = State.PC;
                    State.PC++;
                    State.InstructionsExecuted++;
                    State.CycleCount++;
                    OnInstructionExecuted?.Invoke(inst, State);
                    return;
                }
            }

            State.LastExecutedPC = State.PC;
            State.LastInstruction = inst;
            State.PC++;
            State.InstructionsExecuted++;
            State.CycleCount++;

            ExecuteInstruction(inst);

            OnInstructionExecuted?.Invoke(inst, State);
        }

        public void Step()
        {
            if (State.IsWaiting)
            {
                State.IsWaiting = false;
                State.WaitTimeRemaining = 0f;
            }
            ExecuteOne();
        }

        public void Reset()
        {
            State.Reset();
            if (Program != null)
                foreach (var kvp in Program.Variables)
                    State.GetOrAllocateAddress(kvp.Key);
        }

        // ═══════════════════════════════════════════════════════════════
        // INSTRUCTION EXECUTION
        // ═══════════════════════════════════════════════════════════════

        private void ExecuteInstruction(Instruction inst)
        {
            switch (inst.Op)
            {
                // ── Data movement ──
                case OpCode.LOAD_CONST:
                    State.SetRegister(inst.Arg0, inst.Arg1 / 1000f);
                    break;
                case OpCode.LOAD_FLOAT:
                    if (Program.FloatConstants != null && inst.Arg1 >= 0 && inst.Arg1 < Program.FloatConstants.Length)
                        State.SetRegister(inst.Arg0, Program.FloatConstants[inst.Arg1]);
                    else
                        State.SetRegister(inst.Arg0, 0f);
                    break;
                case OpCode.LOAD_MEM:
                    State.SetRegister(inst.Arg0, State.ReadMemory(inst.Arg1));
                    break;
                case OpCode.STORE_MEM:
                    State.WriteMemory(inst.Arg1, State.GetRegister(inst.Arg0));
                    break;
                case OpCode.MOV:
                    State.SetRegister(inst.Arg0, State.GetRegister(inst.Arg1));
                    break;

                // ── Arithmetic ──
                case OpCode.ADD:
                    State.SetRegister(inst.Arg0,
                        State.GetRegister(inst.Arg0) + State.GetRegister(inst.Arg1));
                    break;
                case OpCode.SUB:
                    State.SetRegister(inst.Arg0,
                        State.GetRegister(inst.Arg0) - State.GetRegister(inst.Arg1));
                    break;
                case OpCode.MUL:
                    State.SetRegister(inst.Arg0,
                        State.GetRegister(inst.Arg0) * State.GetRegister(inst.Arg1));
                    break;
                case OpCode.DIV:
                    float divisor = State.GetRegister(inst.Arg1);
                    State.SetRegister(inst.Arg0,
                        divisor != 0 ? State.GetRegister(inst.Arg0) / divisor : 0f);
                    break;
                case OpCode.MOD:
                    float mod = State.GetRegister(inst.Arg1);
                    State.SetRegister(inst.Arg0,
                        mod != 0 ? State.GetRegister(inst.Arg0) % mod : 0f);
                    break;
                case OpCode.INC:
                    State.SetRegister(inst.Arg0, State.GetRegister(inst.Arg0) + 1);
                    break;
                case OpCode.DEC:
                    State.SetRegister(inst.Arg0, State.GetRegister(inst.Arg0) - 1);
                    break;

                // ── Comparison & control flow ──
                case OpCode.CMP:
                    State.SetCompareFlags(State.GetRegister(inst.Arg0), State.GetRegister(inst.Arg1));
                    break;
                case OpCode.JMP:
                    State.PC = inst.Arg0;
                    break;
                case OpCode.JEQ:
                    if (State.IsZero) State.PC = inst.Arg0;
                    break;
                case OpCode.JNE:
                    if (!State.IsZero) State.PC = inst.Arg0;
                    break;
                case OpCode.JLT:
                    if (State.IsNegative) State.PC = inst.Arg0;
                    break;
                case OpCode.JGT:
                    if (!State.IsNegative && !State.IsZero) State.PC = inst.Arg0;
                    break;
                case OpCode.JLE:
                    if (State.IsNegative || State.IsZero) State.PC = inst.Arg0;
                    break;
                case OpCode.JGE:
                    if (!State.IsNegative) State.PC = inst.Arg0;
                    break;

                // ── Stack ──
                case OpCode.PUSH:
                    State.Stack.Push(State.GetRegister(inst.Arg0));
                    break;
                case OpCode.POP:
                    State.SetRegister(inst.Arg0, State.Stack.Count > 0 ? State.Stack.Pop() : 0f);
                    break;
                case OpCode.CALL:
                    State.Stack.Push(State.PC);
                    State.PC = inst.Arg0;
                    break;
                case OpCode.RET:
                    State.PC = State.Stack.Count > 0 ? (int)State.Stack.Pop() : 0;
                    break;

                // ── Timing ──
                case OpCode.WAIT:
                    float waitTime = State.GetRegister(inst.Arg0);
                    if (waitTime > 0)
                    {
                        State.IsWaiting = true;
                        State.WaitTimeRemaining = waitTime;
                        State.PC--;
                        OnWaitStateChanged?.Invoke(true, waitTime);
                    }
                    break;

                // ── System ──
                case OpCode.NOP:
                    break;
                case OpCode.HALT:
                    State.IsHalted = true;
                    OnHalted?.Invoke();
                    break;
                case OpCode.BREAK:
                    Debug.Log($"[CodeExecutor] BREAK at PC={State.PC - 1}");
                    break;

                // ── Game-specific I/O ──
                default:
                    if (inst.Op >= OpCode.CUSTOM_0 && _ioHandler != null)
                        _ioHandler.ExecuteIO(inst, State);
                    else
                        Debug.LogWarning($"[CodeExecutor] Unknown opcode: {inst.Op}");
                    break;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════

        public int GetCurrentSourceLine()
        {
            if (Program == null || State.PC < 0 || State.PC >= Program.Instructions.Length)
                return -1;
            return Program.Instructions[State.PC].SourceLine;
        }

        public string GetDebugInfo()
        {
            if (Program == null)
                return "[No program loaded]";

            var inst = State.PC > 0 && State.PC <= Program.Instructions.Length
                ? Program.Instructions[State.PC - 1]
                : default;

            return $"PC: {State.PC}/{Program.Instructions.Length} | " +
                   $"Inst: {inst.ToAssembly()} | " +
                   $"Flags: {State.Flags} | " +
                   $"Cycle: {State.CycleCount}";
        }

        /// <summary>Emit an output event (for game layer to fire from ExecuteIO).</summary>
        public void EmitEvent(GameEvent evt)
        {
            State.OutputEvents.Enqueue(evt);
            OnOutput?.Invoke(evt);
        }
    }
}
