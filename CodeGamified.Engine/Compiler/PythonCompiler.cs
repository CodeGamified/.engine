// CodeGamified.Engine — Shared code execution framework
// MIT License
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CodeGamified.Engine.Compiler
{
    /// <summary>
    /// Python-subset → AST → bytecode compiler.
    /// Core parser handles: while, if/else, assignment, function calls,
    /// method calls, object declarations, expressions.
    /// Game-specific builtins are handled by ICompilerExtension.
    /// </summary>
    public class PythonCompiler
    {
        private string[] _lines;
        private int _lineIndex;

        // ═══════════════════════════════════════════════════════════════
        // PUBLIC API
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Compile source code to a CompiledProgram.
        /// </summary>
        /// <param name="source">Python-subset source code</param>
        /// <param name="programName">Name for debug output</param>
        /// <param name="extension">Game-specific compiler extension (nullable)</param>
        /// <param name="metadata">Game-specific metadata to pass to CompilerContext</param>
        public static CompiledProgram Compile(string source, string programName = "Untitled",
                                              ICompilerExtension extension = null,
                                              Dictionary<string, object> metadata = null)
        {
            var compiler = new PythonCompiler();
            var ctx = new CompilerContext { Extension = extension };

            if (metadata != null)
                foreach (var kvp in metadata)
                    ctx.Metadata[kvp.Key] = kvp.Value;

            // Let game register its known types and builtins
            extension?.RegisterBuiltins(ctx);

            var ast = compiler.Parse(source, ctx);
            ast.Compile(ctx);

            // Add implicit HALT if needed
            if (ctx.Instructions.Count == 0 ||
                ctx.Instructions[ctx.Instructions.Count - 1].Op != OpCode.HALT)
            {
                var last = ctx.Instructions.Count > 0
                    ? ctx.Instructions[ctx.Instructions.Count - 1]
                    : default;
                if (last.Op != OpCode.JMP)
                    ctx.Emit(OpCode.HALT, comment: "end of program");
            }

            return new CompiledProgram
            {
                Name = programName,
                SourceCode = source,
                SourceLines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None),
                Instructions = ctx.Instructions.ToArray(),
                Variables = ctx.Variables,
                FloatConstants = ctx.FloatConstants.ToArray(),
                StringConstants = ctx.StringConstants.ToArray(),
                DeclaredObjects = new Dictionary<string, string>(ctx.ObjectTypes),
                Errors = ctx.Errors
            };
        }

        // ═══════════════════════════════════════════════════════════════
        // PARSER
        // ═══════════════════════════════════════════════════════════════

        public AstNodes.ProgramNode Parse(string source, CompilerContext ctx = null)
        {
            _lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _lineIndex = 0;
            _ctx = ctx;

            var program = new AstNodes.ProgramNode();
            program.Statements = ParseBlock(0);
            return program;
        }

        private CompilerContext _ctx;

        private List<AstNodes.AstNode> ParseBlock(int expectedIndent)
        {
            var statements = new List<AstNodes.AstNode>();

            while (_lineIndex < _lines.Length)
            {
                string line = _lines[_lineIndex];
                int indent = GetIndent(line);
                string trimmed = line.Trim();

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    _lineIndex++;
                    continue;
                }

                if (indent < expectedIndent) break;

                if (indent > expectedIndent)
                {
                    Debug.LogWarning($"[Parser] Unexpected indent at line {_lineIndex + 1}");
                    _lineIndex++;
                    continue;
                }

                var stmt = ParseStatement(trimmed, _lineIndex + 1);
                if (stmt != null) statements.Add(stmt);

                _lineIndex++;

                if (trimmed.EndsWith(":") && stmt != null)
                {
                    var body = ParseBlock(expectedIndent + 4);

                    if (stmt is AstNodes.WhileNode wn)
                        wn.Body = body;
                    else if (stmt is AstNodes.ForNode fn)
                        fn.Body = body;
                    else if (stmt is AstNodes.IfNode ifn)
                    {
                        ifn.ThenBody = body;

                        // Parse else / elif
                        while (_lineIndex < _lines.Length)
                        {
                            string nextLine = _lines[_lineIndex];
                            int nextIndent = GetIndent(nextLine);
                            string nextTrimmed = nextLine.Trim();

                            if (nextIndent != expectedIndent) break;

                            // elif condition:
                            var elifMatch = Regex.Match(nextTrimmed, @"^elif\s+(.+):$");
                            if (elifMatch.Success)
                            {
                                _lineIndex++;
                                var elifBody = ParseBlock(expectedIndent + 4);
                                var elifNode = new AstNodes.IfNode
                                {
                                    SourceLine = _lineIndex,
                                    Condition = ParseExpression(elifMatch.Groups[1].Value.Trim(), _lineIndex),
                                    ThenBody = elifBody
                                };
                                // Chain as else of current if
                                ifn.ElseBody = new List<AstNodes.AstNode> { elifNode };
                                ifn = elifNode; // subsequent elif/else attaches to this
                                continue;
                            }

                            // else:
                            if (nextTrimmed == "else:")
                            {
                                _lineIndex++;
                                ifn.ElseBody = ParseBlock(expectedIndent + 4);
                                break;
                            }

                            break; // not elif/else, done
                        }
                    }
                }
            }

            return statements;
        }

        private int GetIndent(string line)
        {
            int spaces = 0;
            foreach (char c in line)
            {
                if (c == ' ') spaces++;
                else if (c == '\t') spaces += 4;
                else break;
            }
            return spaces;
        }

        private AstNodes.AstNode ParseStatement(string trimmed, int lineNum)
        {
            // while condition:
            var whileMatch = Regex.Match(trimmed, @"^while\s+(.+):$");
            if (whileMatch.Success)
            {
                var cond = whileMatch.Groups[1].Value.Trim();
                return new AstNodes.WhileNode
                {
                    SourceLine = lineNum,
                    IsInfinite = (cond == "True" || cond == "1"),
                    Condition = cond == "True"
                        ? new AstNodes.BoolNode { Value = true, SourceLine = lineNum }
                        : ParseExpression(cond, lineNum)
                };
            }

            // for var in range(args):
            var forMatch = Regex.Match(trimmed, @"^for\s+(\w+)\s+in\s+range\((.+)\):$");
            if (forMatch.Success)
            {
                var node = new AstNodes.ForNode
                {
                    SourceLine = lineNum,
                    VarName = forMatch.Groups[1].Value
                };
                string argsStr = forMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        node.RangeArgs.Add(ParseExpression(arg.Trim(), lineNum));
                return node;
            }

            // if condition:
            var ifMatch = Regex.Match(trimmed, @"^if\s+(.+):$");
            if (ifMatch.Success)
            {
                return new AstNodes.IfNode
                {
                    SourceLine = lineNum,
                    Condition = ParseExpression(ifMatch.Groups[1].Value.Trim(), lineNum)
                };
            }

            // Object declaration: Type name = new Type(args)
            var declMatch = Regex.Match(trimmed, @"^(\w+)\s+(\w+)\s*=\s*new\s+(\w+)\((.*)\)$");
            if (declMatch.Success)
            {
                string typeName = declMatch.Groups[1].Value;
                // Only match if the type is registered by the game extension
                bool isKnown = _ctx != null && _ctx.KnownTypes.Contains(typeName);
                if (isKnown)
                {
                    var node = new AstNodes.ObjectDeclNode
                    {
                        SourceLine = lineNum,
                        TypeName = typeName,
                        VarName = declMatch.Groups[2].Value
                    };
                    string argsStr = declMatch.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(argsStr))
                        foreach (var arg in argsStr.Split(','))
                            node.ConstructorArgs.Add(ParseExpression(arg.Trim(), lineNum));
                    return node;
                }
            }

            // Method call: object.method(args)
            var methodCallMatch = Regex.Match(trimmed, @"^(\w+)\.(\w+)\((.*)\)$");
            if (methodCallMatch.Success)
            {
                var node = new AstNodes.MethodCallNode
                {
                    SourceLine = lineNum,
                    ObjectName = methodCallMatch.Groups[1].Value,
                    MethodName = methodCallMatch.Groups[2].Value
                };
                string argsStr = methodCallMatch.Groups[3].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        node.Args.Add(ParseExpression(arg.Trim(), lineNum));
                return node;
            }

            // Assignment: x = expr or x = object.method()
            var assignMatch = Regex.Match(trimmed, @"^(\w+)\s*=\s*(.+)$");
            if (assignMatch.Success)
            {
                string varName = assignMatch.Groups[1].Value;
                string valueExpr = assignMatch.Groups[2].Value.Trim();

                var rhsMethodMatch = Regex.Match(valueExpr, @"^(\w+)\.(\w+)\((.*)\)$");
                if (rhsMethodMatch.Success)
                {
                    return new AstNodes.AssignFromMethodNode
                    {
                        SourceLine = lineNum,
                        VarName = varName,
                        ObjectName = rhsMethodMatch.Groups[1].Value,
                        MethodName = rhsMethodMatch.Groups[2].Value,
                        Args = ParseArgs(rhsMethodMatch.Groups[3].Value, lineNum)
                    };
                }

                return new AstNodes.AssignNode
                {
                    SourceLine = lineNum,
                    VarName = varName,
                    Value = ParseExpression(valueExpr, lineNum)
                };
            }

            // Standalone function call: bell(), wait(3)
            var callMatch = Regex.Match(trimmed, @"^(\w+)\((.*)\)$");
            if (callMatch.Success)
            {
                var call = new AstNodes.CallNode
                {
                    SourceLine = lineNum,
                    FunctionName = callMatch.Groups[1].Value
                };
                string argsStr = callMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        call.Args.Add(ParseExpression(arg.Trim(), lineNum));
                return call;
            }

            Debug.LogWarning($"[Parser] Unparseable line {lineNum}: {trimmed}");
            return null;
        }

        private List<AstNodes.ExprNode> ParseArgs(string argsStr, int lineNum)
        {
            var args = new List<AstNodes.ExprNode>();
            argsStr = argsStr.Trim();
            if (!string.IsNullOrEmpty(argsStr))
                foreach (var arg in argsStr.Split(','))
                    args.Add(ParseExpression(arg.Trim(), lineNum));
            return args;
        }

        private AstNodes.ExprNode ParseExpression(string expr, int lineNum)
        {
            expr = expr.Trim();

            if (expr == "True") return new AstNodes.BoolNode { Value = true, SourceLine = lineNum };
            if (expr == "False") return new AstNodes.BoolNode { Value = false, SourceLine = lineNum };

            // String literal: "..." or '...'
            if ((expr.StartsWith("\"") && expr.EndsWith("\"") && expr.Length >= 2) ||
                (expr.StartsWith("'") && expr.EndsWith("'") && expr.Length >= 2))
                return new AstNodes.StringNode
                {
                    Value = expr.Substring(1, expr.Length - 2),
                    SourceLine = lineNum
                };

            // Strip outer parentheses: (expr) → expr
            if (expr.StartsWith("(") && FindMatchingParen(expr, 0) == expr.Length - 1)
                return ParseExpression(expr.Substring(1, expr.Length - 2), lineNum);

            if (float.TryParse(expr, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out float num))
                return new AstNodes.NumberNode { Value = num, SourceLine = lineNum };

            // Precedence layers (lowest to highest):
            //   1. Comparison: <=, >=, ==, !=, <, >
            //   2. Additive: +, -
            //   3. Multiplicative: *, /, %

            // Layer 1: comparison (scan right-to-left, skip parens)
            foreach (string op in new[] { "<=", ">=", "==", "!=", "<", ">" })
            {
                int idx = FindOperator(expr, op);
                if (idx > 0)
                    return MakeBinOp(expr, op, idx, lineNum);
            }

            // Layer 2: additive (scan right-to-left for lowest precedence binding)
            foreach (string op in new[] { "+", "-" })
            {
                int idx = FindOperator(expr, op);
                if (idx > 0)
                    return MakeBinOp(expr, op, idx, lineNum);
            }

            // Layer 3: multiplicative
            foreach (string op in new[] { "*", "/", "%" })
            {
                int idx = FindOperator(expr, op);
                if (idx > 0)
                    return MakeBinOp(expr, op, idx, lineNum);
            }

            // Function call as expression: func() or func(arg, ...)
            var callExprMatch = Regex.Match(expr, @"^(\w+)\((.*)\)$");
            if (callExprMatch.Success)
            {
                var node = new AstNodes.CallExprNode
                {
                    SourceLine = lineNum,
                    FunctionName = callExprMatch.Groups[1].Value
                };
                string argsStr = callExprMatch.Groups[2].Value.Trim();
                if (!string.IsNullOrEmpty(argsStr))
                    foreach (var arg in argsStr.Split(','))
                        node.Args.Add(ParseExpression(arg.Trim(), lineNum));
                return node;
            }

            // Variable reference
            if (Regex.IsMatch(expr, @"^\w+$"))
                return new AstNodes.VarNode { Name = expr, SourceLine = lineNum };

            Debug.LogWarning($"[Parser] Unparseable expression: {expr}");
            return new AstNodes.NumberNode { Value = 0, SourceLine = lineNum };
        }

        /// <summary>Find matching close paren for open paren at pos.</summary>
        private static int FindMatchingParen(string s, int openPos)
        {
            int depth = 0;
            for (int i = openPos; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')') { depth--; if (depth == 0) return i; }
            }
            return -1;
        }

        /// <summary>
        /// Find operator in expression, scanning right-to-left, skipping parenthesized regions.
        /// Returns index or -1.
        /// </summary>
        private static int FindOperator(string expr, string op)
        {
            int depth = 0;
            // Scan right to left for correct left-associativity
            for (int i = expr.Length - op.Length; i > 0; i--)
            {
                char c = expr[i];
                if (c == ')') depth++;
                else if (c == '(') depth--;

                if (depth != 0) continue;

                if (string.Compare(expr, i, op, 0, op.Length, StringComparison.Ordinal) == 0)
                {
                    // For < and >, don't match <= or >= or !=
                    if (op == "<" && i + 1 < expr.Length && expr[i + 1] == '=') continue;
                    if (op == ">" && i + 1 < expr.Length && expr[i + 1] == '=') continue;
                    if (op == "<" && i > 0 && expr[i - 1] == '!') continue; // part of !=
                    // For - don't match at start of sub-expression (unary negative)
                    if (op == "-" && i > 0)
                    {
                        char prev = expr[i - 1];
                        if (prev == '(' || prev == ',' || prev == ' ') continue;
                    }
                    return i;
                }
            }
            return -1;
        }

        private AstNodes.BinaryOpNode MakeBinOp(string expr, string op, int idx, int lineNum)
        {
            return new AstNodes.BinaryOpNode
            {
                SourceLine = lineNum,
                Left = ParseExpression(expr.Substring(0, idx), lineNum),
                Right = ParseExpression(expr.Substring(idx + op.Length), lineNum),
                Op = op
            };
        }
    }
}
