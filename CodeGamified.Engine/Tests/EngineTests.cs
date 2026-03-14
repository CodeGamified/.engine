// CodeGamified.Engine — Unit Tests
// MIT License
using NUnit.Framework;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace CodeGamified.Engine.Tests
{
    /// <summary>
    /// Stub I/O handler for testing — no game-specific ops, fixed time scale.
    /// </summary>
    public class TestIOHandler : IGameIOHandler
    {
        public float TimeScale = 100f; // batch mode
        public bool PreExecute(Instruction inst, MachineState state) => true;
        public void ExecuteIO(Instruction inst, MachineState state) { }
        public float GetTimeScale() => TimeScale;
        public double GetSimulationTime() => 0.0;
    }

    [TestFixture]
    public class CompilerTests
    {
        [Test]
        public void Compile_EmptySource_ProducesHalt()
        {
            var prog = PythonCompiler.Compile("");
            Assert.IsNotNull(prog);
            Assert.AreEqual(1, prog.Instructions.Length);
            Assert.AreEqual(OpCode.HALT, prog.Instructions[0].Op);
        }

        [Test]
        public void Compile_Assignment_ProducesStoreAndHalt()
        {
            var prog = PythonCompiler.Compile("x = 42");
            Assert.IsTrue(prog.IsValid);
            Assert.IsTrue(prog.Instructions.Length >= 2);
            // LOAD_FLOAT R0, #idx  +  STORE_MEM [0], R0  +  HALT
            Assert.AreEqual(OpCode.LOAD_FLOAT, prog.Instructions[0].Op);
            Assert.AreEqual(OpCode.STORE_MEM, prog.Instructions[1].Op);
        }

        [Test]
        public void FloatConstantTable_PreservesFullPrecision()
        {
            var prog = PythonCompiler.Compile("x = 3.14159");
            Assert.IsNotNull(prog.FloatConstants);
            Assert.IsTrue(prog.FloatConstants.Length > 0);
            Assert.AreEqual(3.14159f, prog.FloatConstants[0], 0.00001f);
        }

        [Test]
        public void Compile_WhileTrue_ProducesInfiniteLoop()
        {
            var prog = PythonCompiler.Compile("while True:\n    wait(1)");
            Assert.IsTrue(prog.IsValid);
            // Should end with JMP back, no HALT
            var last = prog.Instructions[prog.Instructions.Length - 1];
            // The last meaningful instruction before any trailing HALT should be JMP
            bool hasJmp = false;
            foreach (var inst in prog.Instructions)
                if (inst.Op == OpCode.JMP) hasJmp = true;
            Assert.IsTrue(hasJmp);
        }

        [Test]
        public void Compile_WhileCondition_PatchesExitJump()
        {
            var prog = PythonCompiler.Compile("x = 5\nwhile x > 0:\n    x = x - 1");
            Assert.IsTrue(prog.IsValid);
            // Check that JEQ target points past the loop body
            bool hasJeq = false;
            foreach (var inst in prog.Instructions)
            {
                if (inst.Op == OpCode.JEQ)
                {
                    hasJeq = true;
                    Assert.IsTrue(inst.Arg0 > 0, "JEQ should target a valid address");
                }
            }
            Assert.IsTrue(hasJeq, "Non-infinite while should have JEQ exit");
        }

        [Test]
        public void Compile_ForRange_ProducesLoopWithIncrement()
        {
            var prog = PythonCompiler.Compile("for i in range(5):\n    wait(1)");
            Assert.IsTrue(prog.IsValid);
            // Should have ADD (i += step) and JMP (loop back) and JGE (exit)
            bool hasAdd = false, hasJge = false;
            foreach (var inst in prog.Instructions)
            {
                if (inst.Op == OpCode.ADD) hasAdd = true;
                if (inst.Op == OpCode.JGE) hasJge = true;
            }
            Assert.IsTrue(hasAdd, "for loop should have ADD for increment");
            Assert.IsTrue(hasJge, "for loop should have JGE for exit condition");
        }

        [Test]
        public void Compile_ForRangeStartEnd_UsesStartValue()
        {
            var prog = PythonCompiler.Compile("for i in range(2, 8):\n    wait(1)");
            Assert.IsTrue(prog.IsValid);
            // Should have float constants for 2, 8, and 1 (step)
            Assert.IsTrue(prog.FloatConstants.Length >= 3);
        }

        [Test]
        public void Compile_IfElse_ProducesCorrectJumps()
        {
            var source = "x = 1\nif x > 0:\n    x = 2\nelse:\n    x = 3";
            var prog = PythonCompiler.Compile(source);
            Assert.IsTrue(prog.IsValid);
            // Should have both JEQ (to else) and JMP (past else)
            int jeqCount = 0, jmpCount = 0;
            foreach (var inst in prog.Instructions)
            {
                if (inst.Op == OpCode.JEQ) jeqCount++;
                if (inst.Op == OpCode.JMP) jmpCount++;
            }
            Assert.IsTrue(jeqCount >= 1, "if/else needs JEQ");
            Assert.IsTrue(jmpCount >= 1, "if/else needs JMP past else");
        }

        [Test]
        public void Compile_Elif_ChainsCorrectly()
        {
            var source = "x = 1\nif x > 2:\n    x = 10\nelif x > 0:\n    x = 20\nelse:\n    x = 30";
            var prog = PythonCompiler.Compile(source);
            Assert.IsTrue(prog.IsValid);
            // Should have multiple comparison branches
            int cmpCount = 0;
            foreach (var inst in prog.Instructions)
                if (inst.Op == OpCode.CMP) cmpCount++;
            Assert.IsTrue(cmpCount >= 2, "elif needs separate CMP");
        }

        [Test]
        public void Expression_Precedence_MulBeforeAdd()
        {
            // 2 + 3 * 4 should compile as 2 + (3 * 4) = 14, not (2+3) * 4 = 20
            var prog = PythonCompiler.Compile("x = 2 + 3 * 4");
            Assert.IsTrue(prog.IsValid);
            // MUL should appear before ADD in the instruction stream
            int mulIdx = -1, addIdx = -1;
            for (int i = 0; i < prog.Instructions.Length; i++)
            {
                if (prog.Instructions[i].Op == OpCode.MUL && mulIdx < 0) mulIdx = i;
                if (prog.Instructions[i].Op == OpCode.ADD && addIdx < 0) addIdx = i;
            }
            Assert.IsTrue(mulIdx >= 0, "Should have MUL");
            Assert.IsTrue(addIdx >= 0, "Should have ADD");
            Assert.IsTrue(mulIdx < addIdx, "MUL should come before ADD (precedence)");
        }

        [Test]
        public void Expression_Parentheses_OverridePrecedence()
        {
            // (2 + 3) * 4 should compile ADD before MUL
            var prog = PythonCompiler.Compile("x = (2 + 3) * 4");
            Assert.IsTrue(prog.IsValid);
            int mulIdx = -1, addIdx = -1;
            for (int i = 0; i < prog.Instructions.Length; i++)
            {
                if (prog.Instructions[i].Op == OpCode.MUL && mulIdx < 0) mulIdx = i;
                if (prog.Instructions[i].Op == OpCode.ADD && addIdx < 0) addIdx = i;
            }
            Assert.IsTrue(addIdx < mulIdx, "ADD should come before MUL when parenthesized");
        }
    }

    [TestFixture]
    public class ExecutorTests
    {
        [Test]
        public void Execute_Assignment_WritesToMemory()
        {
            var prog = PythonCompiler.Compile("x = 42");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            // Run until halted
            for (int i = 0; i < 100 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.IsFalse(exec.IsRunning);
            Assert.AreEqual(42f, exec.State.ReadMemory("x"), 0.001f);
        }

        [Test]
        public void Execute_Arithmetic_IsCorrect()
        {
            var prog = PythonCompiler.Compile("x = 10\ny = x + 5\nz = y * 2");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(10f, exec.State.ReadMemory("x"), 0.001f);
            Assert.AreEqual(15f, exec.State.ReadMemory("y"), 0.001f);
            Assert.AreEqual(30f, exec.State.ReadMemory("z"), 0.001f);
        }

        [Test]
        public void Execute_ForLoop_Iterates()
        {
            var prog = PythonCompiler.Compile("total = 0\nfor i in range(5):\n    total = total + 1");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 500 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(5f, exec.State.ReadMemory("total"), 0.001f);
        }

        [Test]
        public void Execute_IfElse_TakesThenBranch()
        {
            var prog = PythonCompiler.Compile("x = 10\nif x > 5:\n    y = 1\nelse:\n    y = 0");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(1f, exec.State.ReadMemory("y"), 0.001f);
        }

        [Test]
        public void Execute_IfElse_TakesElseBranch()
        {
            var prog = PythonCompiler.Compile("x = 2\nif x > 5:\n    y = 1\nelse:\n    y = 0");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(0f, exec.State.ReadMemory("y"), 0.001f);
        }

        [Test]
        public void Execute_Wait_SetsWaitState()
        {
            var prog = PythonCompiler.Compile("wait(5)");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            exec.ExecuteOne(); // LOAD_FLOAT
            exec.ExecuteOne(); // WAIT
            Assert.IsTrue(exec.State.IsWaiting);
            Assert.AreEqual(5f, exec.State.WaitTimeRemaining, 0.001f);
        }

        [Test]
        public void Execute_DivisionByZero_ReturnsZero()
        {
            var prog = PythonCompiler.Compile("x = 10\ny = 0\nz = x / y");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(0f, exec.State.ReadMemory("z"), 0.001f);
        }

        [Test]
        public void Execute_Precedence_2Plus3Times4_Equals14()
        {
            var prog = PythonCompiler.Compile("x = 2 + 3 * 4");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(14f, exec.State.ReadMemory("x"), 0.001f);
        }

        [Test]
        public void Execute_Parentheses_2Plus3_Times4_Equals20()
        {
            var prog = PythonCompiler.Compile("x = (2 + 3) * 4");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(20f, exec.State.ReadMemory("x"), 0.001f);
        }

        [Test]
        public void FloatPrecision_SubMillisecond()
        {
            // This value was lost with the old *1000 int scaling
            var prog = PythonCompiler.Compile("x = 0.00025");
            var exec = new CodeExecutor();
            exec.SetIOHandler(new TestIOHandler());
            exec.LoadProgram(prog);

            for (int i = 0; i < 200 && exec.IsRunning; i++)
                exec.ExecuteOne();

            Assert.AreEqual(0.00025f, exec.State.ReadMemory("x"), 0.000001f);
        }
    }

    [TestFixture]
    public class MachineStateTests
    {
        [Test]
        public void Registers_DefaultToZero()
        {
            var state = new MachineState();
            for (int i = 0; i < MachineState.REGISTER_COUNT; i++)
                Assert.AreEqual(0f, state.GetRegister(i));
        }

        [Test]
        public void Memory_ReadWrite_ByAddress()
        {
            var state = new MachineState();
            int addr = state.GetOrAllocateAddress("test");
            state.WriteMemory(addr, 42f);
            Assert.AreEqual(42f, state.ReadMemory(addr), 0.001f);
        }

        [Test]
        public void Memory_ReadWrite_ByName()
        {
            var state = new MachineState();
            state.WriteMemory("foo", 99f);
            Assert.AreEqual(99f, state.ReadMemory("foo"), 0.001f);
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var state = new MachineState();
            state.SetRegister(0, 42f);
            state.WriteMemory("x", 10f);
            state.PC = 50;
            state.IsHalted = true;

            state.Reset();

            Assert.AreEqual(0f, state.GetRegister(0));
            Assert.AreEqual(0, state.PC);
            Assert.IsFalse(state.IsHalted);
            Assert.AreEqual(0f, state.ReadMemory("x"));
        }

        [Test]
        public void CompareFlags_Zero()
        {
            var state = new MachineState();
            state.SetCompareFlags(5f, 5f);
            Assert.IsTrue(state.IsZero);
            Assert.IsFalse(state.IsNegative);
        }

        [Test]
        public void CompareFlags_Negative()
        {
            var state = new MachineState();
            state.SetCompareFlags(3f, 5f);
            Assert.IsFalse(state.IsZero);
            Assert.IsTrue(state.IsNegative);
        }

        [Test]
        public void CompareFlags_Positive()
        {
            var state = new MachineState();
            state.SetCompareFlags(8f, 3f);
            Assert.IsFalse(state.IsZero);
            Assert.IsFalse(state.IsNegative);
        }

        [Test]
        public void Stack_PushPop()
        {
            var state = new MachineState();
            state.Stack.Push(10f);
            state.Stack.Push(20f);
            Assert.AreEqual(20f, state.Stack.Pop());
            Assert.AreEqual(10f, state.Stack.Pop());
        }

        [Test]
        public void GameData_PersistsAcrossReset()
        {
            var state = new MachineState();
            state.GameData["crewCount"] = 5;
            state.Reset();
            Assert.AreEqual(5, state.GameData["crewCount"]);
        }
    }
}
