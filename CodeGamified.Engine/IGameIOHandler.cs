// CodeGamified.Engine — Shared code execution framework
// MIT License

namespace CodeGamified.Engine
{
    /// <summary>
    /// Interface for game-specific I/O handling.
    /// Games implement this to handle CUSTOM_0+ opcodes and provide time sources.
    /// </summary>
    public interface IGameIOHandler
    {
        /// <summary>
        /// Called before executing a CUSTOM opcode.
        /// Return false to skip the instruction (e.g. crew dispatch failed).
        /// Called only for CUSTOM_0+ opcodes.
        /// </summary>
        bool PreExecute(Instruction inst, MachineState state);

        /// <summary>
        /// Execute a game-specific I/O instruction (CUSTOM_0+).
        /// </summary>
        void ExecuteIO(Instruction inst, MachineState state);

        /// <summary>Current simulation time scale (for step-through vs batch mode).</summary>
        float GetTimeScale();

        /// <summary>Current simulation time in seconds.</summary>
        double GetSimulationTime();
    }
}
