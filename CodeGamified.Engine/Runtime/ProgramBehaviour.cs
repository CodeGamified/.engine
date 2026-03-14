// CodeGamified.Engine — Shared code execution framework
// MIT License
using System;
using System.Text;
using UnityEngine;

namespace CodeGamified.Engine.Runtime
{
    /// <summary>
    /// Abstract MonoBehaviour wrapper for code execution on a GameObject.
    /// Games subclass to add their own audio, callbacks, and event processing.
    /// </summary>
    public abstract class ProgramBehaviour : MonoBehaviour
    {
        [Header("Program")]
        [SerializeField] protected string _programName = "Untitled";

        [TextArea(8, 20)]
        [SerializeField] protected string _sourceCode = "";

        [Header("Execution")]
        [SerializeField] protected bool _autoRun = true;
        [SerializeField] [Range(0.01f, 1f)] protected float _stepDelay = 0.1f;
        [SerializeField] protected float _stepThroughThreshold = 10f;

        protected CompiledProgram _program;
        protected CodeExecutor _executor;
        protected bool _isPaused;

        public CompiledProgram Program => _program;
        public CodeExecutor Executor => _executor;
        public MachineState State => _executor?.State;
        public string ProgramName => _programName;
        public bool IsRunning => _executor?.IsRunning ?? false;

        // ═══════════════════════════════════════════════════════════════
        // LIFECYCLE
        // ═══════════════════════════════════════════════════════════════

        protected virtual void Start()
        {
            if (_autoRun && !string.IsNullOrWhiteSpace(_sourceCode))
                LoadAndRun(_sourceCode);
        }

        protected virtual void Update()
        {
            if (_executor == null || !_executor.IsRunning || _isPaused) return;
            _executor.Update(Time.deltaTime);
            ProcessEvents();
        }

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        public virtual bool LoadAndRun(string source)
        {
            _sourceCode = source;

            _executor = new CodeExecutor
            {
                StepDelay = _stepDelay,
                StepThroughThreshold = _stepThroughThreshold
            };

            var ioHandler = CreateIOHandler();
            _executor.SetIOHandler(ioHandler);

            _program = CompileSource(source, _programName);

            if (!_program.IsValid)
            {
                Debug.LogWarning($"[{_programName}] Program has errors:");
                foreach (var err in _program.Errors)
                    Debug.LogWarning($"  {err}");
                return false;
            }

            _executor.LoadProgram(_program);
            _isPaused = false;

            Debug.Log($"[{_programName}] Loaded: {_program.Instructions.Length} instructions");
            return true;
        }

        public void Pause() => _isPaused = true;
        public void Resume() => _isPaused = false;
        public void Step() => _executor?.Step();

        public void Stop()
        {
            _isPaused = true;
            _executor = null;
        }

        public void Reload(string newSource)
        {
            Stop();
            LoadAndRun(newSource);
        }

        public string GetAnnotatedSource()
        {
            if (_program == null || _executor == null) return _sourceCode;

            var sb = new StringBuilder();
            int currentPC = _executor.State.PC;
            int activeLine = -1;

            if (currentPC >= 0 && currentPC < _program.Instructions.Length)
                activeLine = _program.Instructions[currentPC].SourceLine;

            for (int i = 0; i < _program.SourceLines.Length; i++)
            {
                string marker = (i + 1 == activeLine) ? ">>>" : "   ";
                sb.AppendLine($"{marker} {i + 1,3}: {_program.SourceLines[i]}");
            }
            return sb.ToString();
        }

        public string GetAssemblyListing() => _program?.ToAssemblyListing() ?? "; No program loaded";
        public string GetStateDebug() => _executor?.State?.ToDebugString() ?? "; No state";

        // ═══════════════════════════════════════════════════════════════
        // GAME HOOKS — override in subclass
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Create the game-specific I/O handler.</summary>
        protected abstract IGameIOHandler CreateIOHandler();

        /// <summary>Compile source using game-specific extension.</summary>
        protected abstract CompiledProgram CompileSource(string source, string name);

        /// <summary>Process output events each frame (audio, visuals, etc).</summary>
        protected abstract void ProcessEvents();
    }
}
