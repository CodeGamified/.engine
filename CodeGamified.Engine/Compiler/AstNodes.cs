// CodeGamified.Engine — Shared code execution framework
// MIT License
using System;
using System.Collections.Generic;
using UnityEngine;

namespace CodeGamified.Engine.Compiler
{
    /// <summary>
    /// Shared AST node types for the Python-subset compiler.
    /// </summary>
    public static class AstNodes
    {
        public abstract class AstNode
        {
            public int SourceLine;
            public abstract void Compile(CompilerContext ctx);
        }

        public class ProgramNode : AstNode
        {
            public List<AstNode> Statements = new List<AstNode>();
            public override void Compile(CompilerContext ctx)
            {
                foreach (var stmt in Statements) stmt.Compile(ctx);
            }
        }

        public class WhileNode : AstNode
        {
            public ExprNode Condition;
            public List<AstNode> Body = new List<AstNode>();
            public bool IsInfinite;

            public override void Compile(CompilerContext ctx)
            {
                ctx.Extension?.OnWhileLoop(ctx, SourceLine);

                int loopStart = ctx.CurrentAddress;

                if (!IsInfinite)
                {
                    Condition.Compile(ctx, 0);
                    int zeroIdx = ctx.AddFloatConstant(0f);
                    ctx.Emit(OpCode.LOAD_FLOAT, 1, zeroIdx, sourceLine: SourceLine, comment: "load 0 for comparison");
                    ctx.Emit(OpCode.CMP, 0, 1, sourceLine: SourceLine, comment: "test condition");
                    int jumpPatch = ctx.CurrentAddress;
                    ctx.Emit(OpCode.JEQ, 0, sourceLine: SourceLine, comment: "exit loop if false");

                    foreach (var stmt in Body) stmt.Compile(ctx);
                    ctx.Emit(OpCode.JMP, loopStart, sourceLine: SourceLine, comment: "loop back");
                    ctx.PatchJump(jumpPatch, ctx.CurrentAddress);
                }
                else
                {
                    foreach (var stmt in Body) stmt.Compile(ctx);
                    ctx.Emit(OpCode.JMP, loopStart, sourceLine: SourceLine, comment: "loop back");
                }
            }
        }

        public class AssignNode : AstNode
        {
            public string VarName;
            public ExprNode Value;
            public override void Compile(CompilerContext ctx)
            {
                Value.Compile(ctx, 0);
                int addr = ctx.GetVariableAddress(VarName);
                ctx.Emit(OpCode.STORE_MEM, 0, addr, sourceLine: SourceLine, comment: $"store to {VarName}");
            }
        }

        /// <summary>
        /// for i in range(n):  →  i = 0; while i < n: { body; i++ }
        /// for i in range(start, end):  →  i = start; while i < end: { body; i++ }
        /// for i in range(start, end, step):  →  i = start; while i < end: { body; i += step }
        /// </summary>
        public class ForNode : AstNode
        {
            public string VarName;
            public List<ExprNode> RangeArgs = new List<ExprNode>();
            public List<AstNode> Body = new List<AstNode>();

            public override void Compile(CompilerContext ctx)
            {
                ctx.Extension?.OnForLoop(ctx, SourceLine);

                int varAddr = ctx.GetVariableAddress(VarName);

                // Determine start, end, step from range args
                ExprNode startExpr, endExpr, stepExpr;
                if (RangeArgs.Count == 1)
                {
                    startExpr = new NumberNode { Value = 0, SourceLine = SourceLine };
                    endExpr = RangeArgs[0];
                    stepExpr = new NumberNode { Value = 1, SourceLine = SourceLine };
                }
                else if (RangeArgs.Count == 2)
                {
                    startExpr = RangeArgs[0];
                    endExpr = RangeArgs[1];
                    stepExpr = new NumberNode { Value = 1, SourceLine = SourceLine };
                }
                else
                {
                    startExpr = RangeArgs[0];
                    endExpr = RangeArgs[1];
                    stepExpr = RangeArgs[2];
                }

                // i = start
                startExpr.Compile(ctx, 0);
                ctx.Emit(OpCode.STORE_MEM, 0, varAddr, sourceLine: SourceLine, comment: $"{VarName} = start");

                // Store end in a temp variable
                int endAddr = ctx.GetVariableAddress($"_for_end_{VarName}");
                endExpr.Compile(ctx, 0);
                ctx.Emit(OpCode.STORE_MEM, 0, endAddr, sourceLine: SourceLine, comment: $"store end for {VarName}");

                // Store step in a temp variable
                int stepAddr = ctx.GetVariableAddress($"_for_step_{VarName}");
                stepExpr.Compile(ctx, 0);
                ctx.Emit(OpCode.STORE_MEM, 0, stepAddr, sourceLine: SourceLine, comment: $"store step for {VarName}");

                // Loop start: compare i < end
                int loopStart = ctx.CurrentAddress;
                ctx.Emit(OpCode.LOAD_MEM, 0, varAddr, sourceLine: SourceLine, comment: $"load {VarName}");
                ctx.Emit(OpCode.LOAD_MEM, 1, endAddr, sourceLine: SourceLine, comment: "load end");
                ctx.Emit(OpCode.CMP, 0, 1, sourceLine: SourceLine, comment: "compare i < end");
                int exitJump = ctx.CurrentAddress;
                ctx.Emit(OpCode.JGE, 0, sourceLine: SourceLine, comment: "exit loop if i >= end");

                // Body
                foreach (var stmt in Body) stmt.Compile(ctx);

                // i += step
                ctx.Emit(OpCode.LOAD_MEM, 0, varAddr, sourceLine: SourceLine, comment: $"load {VarName}");
                ctx.Emit(OpCode.LOAD_MEM, 1, stepAddr, sourceLine: SourceLine, comment: "load step");
                ctx.Emit(OpCode.ADD, 0, 1, sourceLine: SourceLine, comment: "i += step");
                ctx.Emit(OpCode.STORE_MEM, 0, varAddr, sourceLine: SourceLine, comment: $"store {VarName}");

                // Loop back
                ctx.Emit(OpCode.JMP, loopStart, sourceLine: SourceLine, comment: "loop back");

                // Patch exit
                ctx.PatchJump(exitJump, ctx.CurrentAddress);
            }
        }

        public class AssignFromMethodNode : AstNode
        {
            public string VarName;
            public string ObjectName;
            public string MethodName;
            public List<ExprNode> Args = new List<ExprNode>();

            public override void Compile(CompilerContext ctx)
            {
                var methodCall = new MethodCallNode
                {
                    SourceLine = SourceLine,
                    ObjectName = ObjectName,
                    MethodName = MethodName,
                    Args = Args
                };
                methodCall.Compile(ctx);
                int addr = ctx.GetVariableAddress(VarName);
                ctx.Emit(OpCode.STORE_MEM, 0, addr, sourceLine: SourceLine, comment: $"store to {VarName}");
            }
        }

        public class CallNode : AstNode
        {
            public string FunctionName;
            public List<ExprNode> Args = new List<ExprNode>();

            public override void Compile(CompilerContext ctx)
            {
                // Try game extension first
                if (ctx.Extension != null &&
                    ctx.Extension.TryCompileCall(FunctionName, Args, ctx, SourceLine))
                    return;

                // Engine built-in: wait, min, max
                switch (FunctionName.ToLower())
                {
                    case "wait":
                        if (Args.Count > 0)
                        {
                            Args[0].Compile(ctx, 0);
                            ctx.Emit(OpCode.WAIT, 0, sourceLine: SourceLine, comment: "wait R0 seconds");
                        }
                        break;

                    case "min":
                    case "max":
                        if (Args.Count >= 2)
                        {
                            Args[0].Compile(ctx, 0);
                            int tmpAddr = ctx.GetVariableAddress("_minmax_tmp");
                            ctx.Emit(OpCode.STORE_MEM, 0, tmpAddr, sourceLine: SourceLine, comment: "save first arg");
                            Args[1].Compile(ctx, 0);
                            ctx.Emit(OpCode.MOV, 1, 0, sourceLine: SourceLine, comment: "R1 ← second arg");
                            ctx.Emit(OpCode.LOAD_MEM, 0, tmpAddr, sourceLine: SourceLine, comment: "R0 ← first arg");
                            var op = FunctionName.ToLower() == "min" ? OpCode.MIN : OpCode.MAX;
                            ctx.Emit(op, 0, 1, sourceLine: SourceLine, comment: FunctionName.ToLower());
                        }
                        break;

                    default:
                        // Check user-defined functions
                        if (ctx.FunctionAddresses.TryGetValue(FunctionName, out int funcAddr))
                        {
                            ctx.Emit(OpCode.CALL, funcAddr, sourceLine: SourceLine, comment: $"call {FunctionName}");
                        }
                        else
                        {
                            Debug.LogWarning($"[Compiler] Unknown function: {FunctionName}");
                        }
                        break;
                }
            }
        }

        public class MethodCallNode : AstNode
        {
            public string ObjectName;
            public string MethodName;
            public List<ExprNode> Args = new List<ExprNode>();

            public override void Compile(CompilerContext ctx)
            {
                if (ctx.Extension != null &&
                    ctx.Extension.TryCompileMethodCall(ObjectName, MethodName, Args, ctx, SourceLine))
                    return;

                Debug.LogWarning($"[Compiler] Unknown method: {ObjectName}.{MethodName}()");
            }
        }

        public class ObjectDeclNode : AstNode
        {
            public string TypeName;
            public string VarName;
            public List<ExprNode> ConstructorArgs = new List<ExprNode>();

            public override void Compile(CompilerContext ctx)
            {
                ctx.RegisterObject(VarName, TypeName, ConstructorArgs);

                if (ctx.Extension != null &&
                    ctx.Extension.TryCompileObjectDecl(TypeName, VarName, ConstructorArgs, ctx, SourceLine))
                    return;

                ctx.Emit(OpCode.NOP, sourceLine: SourceLine, comment: $"init {TypeName} {VarName}");
            }
        }

        public class IfNode : AstNode
        {
            public ExprNode Condition;
            public List<AstNode> ThenBody = new List<AstNode>();
            public List<AstNode> ElseBody = new List<AstNode>();

            public override void Compile(CompilerContext ctx)
            {
                Condition.Compile(ctx, 0);
                int zeroIdx = ctx.AddFloatConstant(0f);
                ctx.Emit(OpCode.LOAD_FLOAT, 1, zeroIdx, sourceLine: SourceLine, comment: "load 0 for comparison");
                ctx.Emit(OpCode.CMP, 0, 1, sourceLine: SourceLine, comment: "test condition");

                int jumpToElse = ctx.CurrentAddress;
                ctx.Emit(OpCode.JEQ, 0, sourceLine: SourceLine, comment: "jump to else if false");

                foreach (var stmt in ThenBody) stmt.Compile(ctx);

                if (ElseBody.Count > 0)
                {
                    int jumpPastElse = ctx.CurrentAddress;
                    ctx.Emit(OpCode.JMP, 0, sourceLine: SourceLine, comment: "jump past else");
                    ctx.PatchJump(jumpToElse, ctx.CurrentAddress);
                    foreach (var stmt in ElseBody) stmt.Compile(ctx);
                    ctx.PatchJump(jumpPastElse, ctx.CurrentAddress);
                }
                else
                {
                    ctx.PatchJump(jumpToElse, ctx.CurrentAddress);
                }
            }
        }

        /// <summary>
        /// User-defined function: def name(): body
        /// Compiles as JMP over body, body, RET.
        /// Stores address in ctx.FunctionAddresses.
        /// </summary>
        public class FuncDefNode : AstNode
        {
            public string FuncName;
            public List<AstNode> Body = new List<AstNode>();

            public override void Compile(CompilerContext ctx)
            {
                // JMP over the function body (main flow skips it)
                int jumpOver = ctx.CurrentAddress;
                ctx.Emit(OpCode.JMP, 0, sourceLine: SourceLine, comment: $"skip {FuncName}");

                // Record function start address
                int funcStart = ctx.CurrentAddress;
                ctx.FunctionAddresses[FuncName] = funcStart;

                // Compile body
                foreach (var stmt in Body) stmt.Compile(ctx);

                // Return to caller
                ctx.Emit(OpCode.RET, sourceLine: SourceLine, comment: $"end {FuncName}");

                // Patch the JMP to skip past function
                ctx.PatchJump(jumpOver, ctx.CurrentAddress);
            }
        }

        /// <summary>
        /// Event handler block: hit_opp: / hit_wall: etc.
        /// Compiles body as a labeled section jumped over in main flow.
        /// Stores handler start address in ctx.Metadata["handler:{EventName}"].
        /// Handler body ends with HALT (returns to idle).
        /// </summary>
        public class EventHandlerNode : AstNode
        {
            public string EventName;
            public List<AstNode> Body = new List<AstNode>();

            public override void Compile(CompilerContext ctx)
            {
                // JMP over the handler body (main flow skips it)
                int jumpOver = ctx.CurrentAddress;
                ctx.Emit(OpCode.JMP, 0, sourceLine: SourceLine, comment: $"skip {EventName} handler");

                // Record handler start address
                int handlerStart = ctx.CurrentAddress;
                ctx.Metadata[$"handler:{EventName}"] = handlerStart;

                // Compile body
                foreach (var stmt in Body) stmt.Compile(ctx);

                // End handler with HALT → returns to idle
                ctx.Emit(OpCode.HALT, sourceLine: SourceLine, comment: $"end {EventName}");

                // Patch the JMP to skip past handler
                ctx.PatchJump(jumpOver, ctx.CurrentAddress);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // MATCH/CASE (Python 3.10 switch)
        // ═══════════════════════════════════════════════════════════════

        public class MatchCaseClause
        {
            public ExprNode Value;
            public List<AstNode> Body = new List<AstNode>();
            public int SourceLine;
        }

        public class MatchNode : AstNode
        {
            public ExprNode Subject;
            public List<MatchCaseClause> Cases = new List<MatchCaseClause>();

            public override void Compile(CompilerContext ctx)
            {
                Subject.Compile(ctx, 0);
                int subjectAddr = ctx.GetVariableAddress("_match_subj");
                ctx.Emit(OpCode.STORE_MEM, 0, subjectAddr, sourceLine: SourceLine, comment: "match subject");

                var exitJumps = new List<int>();
                MatchCaseClause wildcard = null;

                for (int i = 0; i < Cases.Count; i++)
                {
                    var c = Cases[i];
                    if (c.Value == null) { wildcard = c; continue; }

                    ctx.Emit(OpCode.LOAD_MEM, 0, subjectAddr, sourceLine: c.SourceLine, comment: "load match subject");
                    c.Value.Compile(ctx, 1);
                    ctx.Emit(OpCode.CMP, 0, 1, sourceLine: c.SourceLine, comment: "case compare");
                    int skipBody = ctx.CurrentAddress;
                    ctx.Emit(OpCode.JNE, 0, sourceLine: c.SourceLine, comment: "skip if no match");

                    foreach (var stmt in c.Body) stmt.Compile(ctx);
                    exitJumps.Add(ctx.CurrentAddress);
                    ctx.Emit(OpCode.JMP, 0, sourceLine: c.SourceLine, comment: "exit match");

                    ctx.PatchJump(skipBody, ctx.CurrentAddress);
                }

                if (wildcard != null)
                    foreach (var stmt in wildcard.Body) stmt.Compile(ctx);

                int endAddr = ctx.CurrentAddress;
                foreach (int j in exitJumps)
                    ctx.PatchJump(j, endAddr);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // EXPRESSION NODES
        // ═══════════════════════════════════════════════════════════════

        public abstract class ExprNode : AstNode
        {
            public abstract void Compile(CompilerContext ctx, int targetReg);
            public override void Compile(CompilerContext ctx) => Compile(ctx, 0);
        }

        public class NumberNode : ExprNode
        {
            public float Value;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                int idx = ctx.AddFloatConstant(Value);
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg, idx,
                    sourceLine: SourceLine, comment: $"load {Value}");
            }
        }

        public class VarNode : ExprNode
        {
            public string Name;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                int addr = ctx.GetVariableAddress(Name);
                ctx.Emit(OpCode.LOAD_MEM, targetReg, addr, sourceLine: SourceLine, comment: $"load {Name}");
            }
        }

        public class BinaryOpNode : ExprNode
        {
            public ExprNode Left;
            public ExprNode Right;
            public string Op;

            private static int _tempCounter;

            public override void Compile(CompilerContext ctx, int targetReg)
            {
                Left.Compile(ctx, targetReg);

                // Spill Left result to temp memory before evaluating Right,
                // because Right may contain function calls that clobber R0.
                int tempAddr = ctx.GetVariableAddress($"_binop_tmp_{_tempCounter++}");
                ctx.Emit(OpCode.STORE_MEM, targetReg, tempAddr, sourceLine: SourceLine, comment: "spill left operand");

                Right.Compile(ctx, targetReg + 1);

                // Restore Left result
                ctx.Emit(OpCode.LOAD_MEM, targetReg, tempAddr, sourceLine: SourceLine, comment: "restore left operand");

                switch (Op)
                {
                    case "+": ctx.Emit(OpCode.ADD, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "add"); break;
                    case "-": ctx.Emit(OpCode.SUB, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "subtract"); break;
                    case "*": ctx.Emit(OpCode.MUL, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "multiply"); break;
                    case "/": ctx.Emit(OpCode.DIV, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "divide"); break;
                    case "%": ctx.Emit(OpCode.MOD, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "modulo"); break;
                    case "<": case ">": case "==": case "!=": case "<=": case ">=":
                        ctx.Emit(OpCode.CMP, targetReg, targetReg + 1, sourceLine: SourceLine, comment: $"compare {Op}");
                        EmitCompareResult(ctx, Op, targetReg, SourceLine);
                        break;
                }
            }

            private static void EmitCompareResult(CompilerContext ctx, string op, int reg, int line)
            {
                switch (op)
                {
                    case "<":
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(1f), sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JLT, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if less");
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(0f), sourceLine: line, comment: "was false");
                        break;
                    case ">":
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(1f), sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JGT, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if greater");
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(0f), sourceLine: line, comment: "was false");
                        break;
                    case "==":
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(1f), sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JEQ, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if equal");
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(0f), sourceLine: line, comment: "was false");
                        break;
                    case "!=":
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(1f), sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JNE, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if not equal");
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(0f), sourceLine: line, comment: "was false");
                        break;
                    case "<=":
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(1f), sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JLE, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if <=");
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(0f), sourceLine: line, comment: "was false");
                        break;
                    case ">=":
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(1f), sourceLine: line, comment: "assume true");
                        ctx.Emit(OpCode.JGE, ctx.CurrentAddress + 2, sourceLine: line, comment: "skip if >=");
                        ctx.Emit(OpCode.LOAD_FLOAT, reg, ctx.AddFloatConstant(0f), sourceLine: line, comment: "was false");
                        break;
                }
            }
        }

        public class BoolNode : ExprNode
        {
            public bool Value;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                int idx = ctx.AddFloatConstant(Value ? 1f : 0f);
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg, idx,
                    sourceLine: SourceLine, comment: Value ? "True" : "False");
            }
        }

        /// <summary>Logical NOT: evaluates operand, produces 1 if zero, 0 otherwise.</summary>
        public class NotNode : ExprNode
        {
            public ExprNode Operand;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                Operand.Compile(ctx, targetReg);
                int zeroIdx = ctx.AddFloatConstant(0f);
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg + 1, zeroIdx, sourceLine: SourceLine, comment: "load 0");
                ctx.Emit(OpCode.CMP, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "not: compare to 0");
                // If operand == 0 → result is 1 (truthy)
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg, ctx.AddFloatConstant(1f), sourceLine: SourceLine, comment: "assume true (was 0)");
                ctx.Emit(OpCode.JEQ, ctx.CurrentAddress + 2, sourceLine: SourceLine, comment: "skip if was 0");
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg, zeroIdx, sourceLine: SourceLine, comment: "was nonzero → false");
            }
        }

        /// <summary>Logical AND: short-circuit — if left is 0, result is 0.</summary>
        public class AndNode : ExprNode
        {
            public ExprNode Left;
            public ExprNode Right;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                Left.Compile(ctx, targetReg);
                int zeroIdx = ctx.AddFloatConstant(0f);
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg + 1, zeroIdx, sourceLine: SourceLine, comment: "load 0");
                ctx.Emit(OpCode.CMP, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "and: test left");
                int skipRight = ctx.CurrentAddress;
                ctx.Emit(OpCode.JEQ, 0, sourceLine: SourceLine, comment: "short-circuit and");
                Right.Compile(ctx, targetReg);
                ctx.PatchJump(skipRight, ctx.CurrentAddress);
            }
        }

        /// <summary>Logical OR: short-circuit — if left is nonzero, result is left.</summary>
        public class OrNode : ExprNode
        {
            public ExprNode Left;
            public ExprNode Right;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                Left.Compile(ctx, targetReg);
                int zeroIdx = ctx.AddFloatConstant(0f);
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg + 1, zeroIdx, sourceLine: SourceLine, comment: "load 0");
                ctx.Emit(OpCode.CMP, targetReg, targetReg + 1, sourceLine: SourceLine, comment: "or: test left");
                int skipRight = ctx.CurrentAddress;
                ctx.Emit(OpCode.JNE, 0, sourceLine: SourceLine, comment: "short-circuit or");
                Right.Compile(ctx, targetReg);
                ctx.PatchJump(skipRight, ctx.CurrentAddress);
            }
        }

        public class StringNode : ExprNode
        {
            public string Value;
            public override void Compile(CompilerContext ctx, int targetReg)
            {
                int idx = ctx.AddStringConstant(Value);
                int floatIdx = ctx.AddFloatConstant((float)idx);
                ctx.Emit(OpCode.LOAD_FLOAT, targetReg, floatIdx,
                    sourceLine: SourceLine, comment: $"load str \"{Value}\" (sidx={idx})");
            }
        }

        /// <summary>
        /// Function call used as an expression (RHS of assignment, argument, etc.).
        /// Result lands in targetReg (R0 by convention from CallNode, then MOV if needed).
        /// </summary>
        public class CallExprNode : ExprNode
        {
            public string FunctionName;
            public List<ExprNode> Args = new List<ExprNode>();

            public override void Compile(CompilerContext ctx, int targetReg)
            {
                // Compile as a statement call (result in R0)
                var call = new CallNode
                {
                    SourceLine = SourceLine,
                    FunctionName = FunctionName,
                    Args = Args
                };
                call.Compile(ctx);

                // Move result from R0 to targetReg if different
                if (targetReg != 0)
                    ctx.Emit(OpCode.MOV, targetReg, 0, sourceLine: SourceLine,
                        comment: $"mov R{targetReg} ← R0");
            }
        }
    }
}
