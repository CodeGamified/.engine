# CodeGamified.Engine

Shared code execution framework for educational programming games.  
Python subset → AST → RISC-like bytecode → time-scale-aware executor.

## Architecture

```
Python Source → [PythonCompiler] → AST → [CompilerContext] → Instructions[]
                                           ↑ ICompilerExtension provides game builtins
                                           ↑ KnownTypes gates object declarations

Instructions[] → [CodeExecutor] → MachineState
                      ↑ IGameIOHandler handles CUSTOM_0+ opcodes
                      ↑ PreExecute gates I/O (crew dispatch, permissions)
                      ↑ time-scale aware (step <10x, batch ≥10x)
```

## Files

| File | Purpose |
|------|---------|
| `OpCode.cs` | 27 core opcodes + `CUSTOM_0..CUSTOM_31` for game I/O |
| `CpuFlags.cs` | Zero, Negative, Carry, Overflow |
| `Instruction.cs` | Immutable struct with `Tag` for game metadata |
| `GameEvent.cs` | Base output event (type, value, channel, time, tag) |
| `MachineState.cs` | Registers R0-R7, stack, memory, PC, flags, `GameData` dict |
| `CompiledProgram.cs` | Instructions[], variables, source, `Metadata` dict |
| `CodeExecutor.cs` | Update loop, step/batch, core opcode switch, `IGameIOHandler` delegation |
| `IGameIOHandler.cs` | `PreExecute()`, `ExecuteIO()`, `GetTimeScale()`, `GetSimulationTime()` |
| `Compiler/AstNodes.cs` | All shared AST nodes (While, If, Assign, Call, MethodCall, etc.) |
| `Compiler/CompilerContext.cs` | Emit, variables, jump patching, object tracking, `KnownTypes` |
| `Compiler/ICompilerExtension.cs` | `RegisterBuiltins()`, `TryCompileCall()`, `TryCompileMethodCall()` |
| `Compiler/PythonCompiler.cs` | Core parser + `Compile()` static method |
| `Runtime/ProgramBehaviour.cs` | Abstract `MonoBehaviour` with `LoadAndRun`, `Pause`, `Step`, etc. |
| `Runtime/IProgramDatabase.cs` | Generic interface for program storage |

## Integration Pattern

### 1. Implement `IGameIOHandler`

```csharp
public class SatelliteIOHandler : IGameIOHandler
{
    public bool PreExecute(Instruction inst, MachineState state) => true;

    public void ExecuteIO(Instruction inst, MachineState state)
    {
        switch ((SatelliteOp)(inst.Op - OpCode.CUSTOM_0))
        {
            case SatelliteOp.SensorRead: /* ... */ break;
            case SatelliteOp.Transmit:   /* ... */ break;
            case SatelliteOp.Output:     /* ... */ break;
        }
    }

    public float GetTimeScale() => SimulationTime.Instance?.timeScale ?? 1f;
    public double GetSimulationTime() => SimulationTime.Instance?.simulationTime ?? 0.0;
}
```

### 2. Implement `ICompilerExtension`

```csharp
public class SatelliteCompilerExtension : ICompilerExtension
{
    public void RegisterBuiltins(CompilerContext ctx)
    {
        ctx.KnownTypes.Add("Radio");
        ctx.KnownTypes.Add("Geiger");
        ctx.KnownTypes.Add("Thermometer");
    }

    public bool TryCompileCall(string fn, List<ExprNode> args, CompilerContext ctx, int line)
    {
        if (fn == "beep") { ctx.Emit(OpCode.CUSTOM_2, ...); return true; }
        return false;
    }

    public bool TryCompileMethodCall(string obj, string method, ...) { /* ... */ }
    public bool TryCompileObjectDecl(string type, string var, ...) { /* ... */ }
}
```

### 3. Subclass `ProgramBehaviour`

```csharp
public class SatelliteProgram : ProgramBehaviour
{
    protected override IGameIOHandler CreateIOHandler() => new SatelliteIOHandler();
    protected override CompiledProgram CompileSource(string source, string name)
        => PythonCompiler.Compile(source, name, new SatelliteCompilerExtension());
    protected override void ProcessEvents() { /* handle beeps, transmissions */ }
}
```

## Submodule Usage

```bash
# In your game repo:
git submodule add <engine-repo-url> Assets/CodeGamified.Engine
```

Both BitNaughts and SeaRauber import this as a submodule.  
Engine improvements propagate to both via `git submodule update`.

## CUSTOM Opcode Conventions

Games choose their own `CUSTOM_0..CUSTOM_31` mapping, but this convention avoids collisions  
if cross-game features (e.g. inter-program messaging) are desired:

| Slot | Convention | BitNaughts | SeaRauber |
|------|-----------|------------|-----------|
| `CUSTOM_0` | `MSG_SEND` | *(unused)* | Send to another program |
| `CUSTOM_1` | `MSG_RECV` | *(unused)* | Receive from inbox |
| `CUSTOM_2` | `MSG_PEEK` | *(unused)* | Peek inbox |
| `CUSTOM_3` | Game I/O read | `SENSOR_READ` | `QUERY` |
| `CUSTOM_4` | Game I/O write | `TRANSMIT` | `ORDER` |
| `CUSTOM_5` | Game output | `OUTPUT` (beep/LED) | `SIGNAL` |
| `CUSTOM_6` | Game log | *(unused)* | `LOG` |
| `CUSTOM_7+` | Game-specific | *(free)* | *(free)* |

This is a *convention*, not enforced. Games can remap freely.

## Refinements Over Original Implementation

| # | What | Effect |
|---|------|--------|
| 1 | `ForNode` | `for i in range(n)` / `range(start,end)` / `range(start,end,step)` |
| 2 | `else`/`elif` parsing | Properly chains if/elif/else blocks |
| 3 | Operator precedence | `*/%` before `+-` before comparisons; parentheses supported |
| 4 | `LOAD_FLOAT` + constant table | Full float precision (replaces `*1000` int truncation) |
| 5 | Non-infinite `while` exit jump | Correctly patched (BitNaughts had TODO) |
| 6 | Assembly definition | `CodeGamified.Engine.asmdef` for Unity project isolation |
| 7 | `IGameIOHandler` abstraction | No more direct `SimulationTime.Instance` coupling |
| 8 | Messaging convention | `CUSTOM_0..2` reserved for inter-program messaging |
| 9 | Conditional `Comment` field | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` — zero GC in release |
| 10 | Unit tests | 20+ NUnit tests covering compiler, executor, state, precedence |
