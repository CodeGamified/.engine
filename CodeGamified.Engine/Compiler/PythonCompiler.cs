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

            var compiled = new CompiledProgram
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

            // Copy metadata (event handler addresses, etc.)
            foreach (var kvp in ctx.Metadata)
                compiled.Metadata[kvp.Key] = kvp.Value;

            return compiled;
        }

        // ═══════════════════════════════════════════════════════════════
        // PARSER
        // ═══════════════════════════════════════════════════════════════

        public AstNodes.ProgramNode Parse(string source, CompilerContext ctx = null)
        {
            var rawLines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            _lines = JoinContinuationLines(rawLines, out _lineMap);
            _lineIndex = 0;
            _ctx = ctx;

            var program = new AstNodes.ProgramNode();
            program.Statements = ParseBlock(0);
            return program;
        }

        /// <summary>
        /// Join lines with unclosed parentheses (Python implicit continuation).
        /// Also supports explicit backslash continuation.
        /// lineMap[i] = 1-based raw line number for joined line i.
        /// </summary>
        private static string[] JoinContinuationLines(string[] rawLines, out int[] lineMap)
        {
            var result = new List<string>();
            var map = new List<int>();
            int i = 0;
            while (i < rawLines.Length)
            {
                int startLine = i + 1; // 1-based
                string line = rawLines[i];
                int depth = ParenDepth(line);

                // Explicit backslash continuation
                while (line.TrimEnd().EndsWith("\\") && i + 1 < rawLines.Length)
                {
                    line = line.TrimEnd();
                    line = line.Substring(0, line.Length - 1) + " " + rawLines[++i].Trim();
                    depth = ParenDepth(line);
                }

                // Implicit continuation: unclosed parens
                while (depth > 0 && i + 1 < rawLines.Length)
                {
                    line = line + " " + rawLines[++i].Trim();
                    depth = ParenDepth(line);
                }

                result.Add(line);
                map.Add(startLine);
                i++;
            }
            lineMap = map.ToArray();
            return result.ToArray();
        }

        private static int ParenDepth(string line)
        {
            int depth = 0;
            bool inString = false;
            char strChar = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == strChar) inString = false;
                    continue;
                }
                if (c == '\'' || c == '"') { inString = true; strChar = c; continue; }
                if (c == '#') break; // rest is comment
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
            }
            return depth;
        }

        private CompilerContext _ctx;
        private int[] _lineMap; // joined index → 1-based raw line number

        private int RawLineNum(int joinedIndex) =>
            _lineMap != null && joinedIndex >= 0 && joinedIndex < _lineMap.Length
                ? _lineMap[joinedIndex] : joinedIndex + 1;

        private List<AstNodes.AstNode> ParseBlock(int expectedIndent)
        {
            var statements = new List<AstNodes.AstNode>();

            while (_lineIndex < _lines.Length)
            {
                string line = _lines[_lineIndex];
                int indent = GetIndent(line);
                string trimmed = line.Trim();

                // Strip inline comments (preserve string contents)
                trimmed = StripInlineComment(trimmed);

                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                {
                    _lineIndex++;
                    continue;
                }

                if (indent < expectedIndent) break;

                if (indent > expectedIndent)
                {
                    Debug.LogWarning($"[Parser] Unexpected indent at line {RawLineNum(_lineIndex)}");
                    _lineIndex++;
                    continue;
                }

                var stmt = ParseStatement(trimmed, RawLineNum(_lineIndex));
                if (stmt != null) statements.Add(stmt);

                _lineIndex++;

                if (trimmed.EndsWith(":") && stmt != null)
                {
                    int bodyIndent = PeekNextIndent(expectedIndent);
                    var body = ParseBlock(bodyIndent);

                    if (stmt is AstNodes.WhileNode wn)
                        wn.Body = body;
                    else if (stmt is AstNodes.ForNode fn)
                        fn.Body = body;
                    else if (stmt is AstNodes.FuncDefNode fdn)
                        fdn.Body = body;
                    else if (stmt is AstNodes.EventHandlerNode ehn)
                        ehn.Body = body;
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
                                var elifBody = ParseBlock(PeekNextIndent(expectedIndent));
                                var elifNode = new AstNodes.IfNode
                                {
                                    SourceLine = RawLineNum(_lineIndex),
                                    Condition = ParseExpression(elifMatch.Groups[1].Value.Trim(), RawLineNum(_lineIndex)),
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
                                ifn.ElseBody = ParseBlock(PeekNextIndent(expectedIndent));
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

        /// <summary>
        /// Peek ahead to find the indentation of the next non-empty, non-comment line.
        /// Used after a colon-ending statement to detect actual body indent (2, 4, etc.).
        /// Falls back to parentIndent + 4 if no body line is found.
        /// </summary>
        private int PeekNextIndent(int parentIndent)
        {
            for (int i = _lineIndex; i < _lines.Length; i++)
            {
                string t = _lines[i].Trim();
                if (string.IsNullOrEmpty(t) || t.StartsWith("#")) continue;
                int ind = GetIndent(_lines[i]);
                if (ind > parentIndent) return ind;
                break;
            }
            return parentIndent + 4; // fallback
        }

        /// <summary>Strip inline # comments while respecting string literals.</summary>
        private static string StripInlineComment(string line)
        {
            bool inString = false;
            char strChar = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString)
                {
                    if (c == '\\' && i + 1 < line.Length) { i++; continue; }
                    if (c == strChar) inString = false;
                    continue;
                }
                if (c == '\'' || c == '"') { inString = true; strChar = c; continue; }
                if (c == '#') return line.Substring(0, i).TrimEnd();
            }
            return line;
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
                    foreach (var arg in SplitTopLevelArgs(argsStr))
                        node.RangeArgs.Add(ParseExpression(arg.Trim(), lineNum));
                return node;
            }

            // def name():
            var defMatch = Regex.Match(trimmed, @"^def\s+(\w+)\(\):$");
            if (defMatch.Success)
            {
                return new AstNodes.FuncDefNode
                {
                    SourceLine = lineNum,
                    FuncName = defMatch.Groups[1].Value
                };
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

            // Event handler block: hit: / hit_opp: / hit_wall: / hit_<name>: / serve:
            var handlerMatch = Regex.Match(trimmed, @"^(hit(?:_\w+)?|serve):$");
            if (handlerMatch.Success)
            {
                return new AstNodes.EventHandlerNode
                {
                    SourceLine = lineNum,
                    EventName = handlerMatch.Groups[1].Value
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
                        foreach (var arg in SplitTopLevelArgs(argsStr))
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
                    foreach (var arg in SplitTopLevelArgs(argsStr))
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
                    foreach (var arg in SplitTopLevelArgs(argsStr))
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
                foreach (var arg in SplitTopLevelArgs(argsStr))
                    args.Add(ParseExpression(arg.Trim(), lineNum));
            return args;
        }

        private static List<string> SplitTopLevelArgs(string argsStr)
        {
            var result = new List<string>();
            int depth = 0;
            int start = 0;
            for (int i = 0; i < argsStr.Length; i++)
            {
                char c = argsStr[i];
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(argsStr.Substring(start, i - start));
                    start = i + 1;
                }
            }
            result.Add(argsStr.Substring(start));
            return result;
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
                    foreach (var arg in SplitTopLevelArgs(argsStr))
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
                        if (prev == '(' || prev == ',') continue;
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
