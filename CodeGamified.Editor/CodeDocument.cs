// CodeGamified.Editor — Tap-to-code editor for mobile
// MIT License
using System;
using System.Collections.Generic;
using System.Text;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace CodeGamified.Editor
{
    /// <summary>
    /// Mutable wrapper around a list of AST statements.
    /// Players edit the AST — source text is always regenerated.
    /// This is the single source of truth for the program.
    /// </summary>
    public class CodeDocument
    {
        public string Name = "Untitled";
        public readonly List<AstNodes.AstNode> Statements = new();

        public int LineCount => Statements.Count;
        public bool IsEmpty => Statements.Count == 0;

        // ═══════════════════════════════════════════════════════════
        //  AUDIO / HAPTIC FEEDBACK HOOKS  (#8)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Fired after any mutation (insert, remove, replace, swap, undo, redo).</summary>
        public event Action OnDocumentChanged;

        // ═══════════════════════════════════════════════════════════
        //  UNDO / REDO  (#1)
        // ═══════════════════════════════════════════════════════════

        readonly Stack<EditAction> _undoStack = new();
        readonly Stack<EditAction> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Undo()
        {
            if (_undoStack.Count == 0) return;
            var action = _undoStack.Pop();
            action.Undo(this);
            _redoStack.Push(action);
            OnDocumentChanged?.Invoke();
        }

        public void Redo()
        {
            if (_redoStack.Count == 0) return;
            var action = _redoStack.Pop();
            action.Redo(this);
            _undoStack.Push(action);
            OnDocumentChanged?.Invoke();
        }

        void RecordAction(EditAction action)
        {
            _undoStack.Push(action);
            _redoStack.Clear();
            OnDocumentChanged?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════
        //  MUTATION (top-level)
        // ═══════════════════════════════════════════════════════════

        public void InsertAt(int index, AstNodes.AstNode node)
        {
            if (index < 0) index = 0;
            if (index > Statements.Count) index = Statements.Count;
            Statements.Insert(index, node);
            RenumberLines();
            RecordAction(new InsertAction(null, index, node));
        }

        public void Append(AstNodes.AstNode node)
        {
            int idx = Statements.Count;
            Statements.Add(node);
            RenumberLines();
            RecordAction(new InsertAction(null, idx, node));
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < Statements.Count)
            {
                var removed = Statements[index];
                Statements.RemoveAt(index);
                RenumberLines();
                RecordAction(new RemoveAction(null, index, removed));
            }
        }

        public void ReplaceAt(int index, AstNodes.AstNode node)
        {
            if (index >= 0 && index < Statements.Count)
            {
                var old = Statements[index];
                Statements[index] = node;
                RenumberLines();
                RecordAction(new ReplaceAction(null, index, old, node));
            }
        }

        public AstNodes.AstNode GetAt(int index)
        {
            return index >= 0 && index < Statements.Count ? Statements[index] : null;
        }

        public void Clear()
        {
            Statements.Clear();
            _undoStack.Clear();
            _redoStack.Clear();
        }

        // ═══════════════════════════════════════════════════════════
        //  MOVE LINE UP/DOWN  (#10)
        // ═══════════════════════════════════════════════════════════

        public bool MoveUp(int index)
        {
            if (index <= 0 || index >= Statements.Count) return false;
            (Statements[index], Statements[index - 1]) = (Statements[index - 1], Statements[index]);
            RenumberLines();
            RecordAction(new SwapAction(null, index, index - 1));
            return true;
        }

        public bool MoveDown(int index)
        {
            if (index < 0 || index >= Statements.Count - 1) return false;
            (Statements[index], Statements[index + 1]) = (Statements[index + 1], Statements[index]);
            RenumberLines();
            RecordAction(new SwapAction(null, index, index + 1));
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  BODY-AWARE OPERATIONS  (#3)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Get the body list that a statement's children live in, or null.</summary>
        public static List<AstNodes.AstNode> GetBody(AstNodes.AstNode parent)
        {
            return parent switch
            {
                AstNodes.WhileNode w => w.Body,
                AstNodes.ForNode f => f.Body,
                AstNodes.IfNode ifn => ifn.ThenBody,
                _ => null
            };
        }

        /// <summary>Insert a statement into a parent's body at the given index.</summary>
        public void InsertIntoBody(AstNodes.AstNode parent, int bodyIndex, AstNodes.AstNode child)
        {
            var body = GetBody(parent);
            if (body == null) return;
            if (bodyIndex < 0) bodyIndex = 0;
            if (bodyIndex > body.Count) bodyIndex = body.Count;
            body.Insert(bodyIndex, child);
            RenumberLines();
            RecordAction(new InsertAction(parent, bodyIndex, child));
        }

        /// <summary>Remove a statement from a parent's body.</summary>
        public void RemoveFromBody(AstNodes.AstNode parent, int bodyIndex)
        {
            var body = GetBody(parent);
            if (body == null || bodyIndex < 0 || bodyIndex >= body.Count) return;
            var removed = body[bodyIndex];
            body.RemoveAt(bodyIndex);
            RenumberLines();
            RecordAction(new RemoveAction(parent, bodyIndex, removed));
        }

        /// <summary>Swap two lines inside a parent's body.</summary>
        public bool MoveUpInBody(AstNodes.AstNode parent, int bodyIndex)
        {
            var body = GetBody(parent);
            if (body == null || bodyIndex <= 0 || bodyIndex >= body.Count) return false;
            (body[bodyIndex], body[bodyIndex - 1]) = (body[bodyIndex - 1], body[bodyIndex]);
            RenumberLines();
            RecordAction(new SwapAction(parent, bodyIndex, bodyIndex - 1));
            return true;
        }

        public bool MoveDownInBody(AstNodes.AstNode parent, int bodyIndex)
        {
            var body = GetBody(parent);
            if (body == null || bodyIndex < 0 || bodyIndex >= body.Count - 1) return false;
            (body[bodyIndex], body[bodyIndex + 1]) = (body[bodyIndex + 1], body[bodyIndex]);
            RenumberLines();
            RecordAction(new SwapAction(parent, bodyIndex, bodyIndex + 1));
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  FLATTENED VIEW MODEL  (#4)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// A single display row mapping a rendered source line back to its AST owner.
        /// </summary>
        public struct DisplayLine
        {
            public AstNodes.AstNode Node;
            public AstNodes.AstNode Parent;
            public int IndexInParent;
            public int Depth;
            public string Text;
            public bool IsCompoundHeader;
        }

        /// <summary>
        /// Build a flat list of display lines from the nested AST.
        /// Each source-visible line gets an entry with its parent context.
        /// </summary>
        public List<DisplayLine> BuildDisplayLines()
        {
            var result = new List<DisplayLine>();
            BuildDisplayLinesRecursive(Statements, null, 0, result);
            return result;
        }

        void BuildDisplayLinesRecursive(List<AstNodes.AstNode> nodes, AstNodes.AstNode parent,
            int depth, List<DisplayLine> result)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                string pad = new string(' ', depth * 4);

                switch (node)
                {
                    case AstNodes.WhileNode w:
                        result.Add(new DisplayLine
                        {
                            Node = w, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}while {ExprToSource(w.Condition)}:", IsCompoundHeader = true
                        });
                        if (w.Body.Count > 0)
                            BuildDisplayLinesRecursive(w.Body, w, depth + 1, result);
                        else
                            result.Add(new DisplayLine
                            {
                                Node = null, Parent = w, IndexInParent = 0, Depth = depth + 1,
                                Text = $"{new string(' ', (depth + 1) * 4)}pass"
                            });
                        break;

                    case AstNodes.ForNode f:
                        string rangeArgs = string.Join(", ", f.RangeArgs.ConvertAll(ExprToSource));
                        result.Add(new DisplayLine
                        {
                            Node = f, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}for {f.VarName} in range({rangeArgs}):", IsCompoundHeader = true
                        });
                        if (f.Body.Count > 0)
                            BuildDisplayLinesRecursive(f.Body, f, depth + 1, result);
                        else
                            result.Add(new DisplayLine
                            {
                                Node = null, Parent = f, IndexInParent = 0, Depth = depth + 1,
                                Text = $"{new string(' ', (depth + 1) * 4)}pass"
                            });
                        break;

                    case AstNodes.IfNode ifn:
                        AppendIfDisplayLines(ifn, parent, i, depth, pad, result, false);
                        break;

                    case AstNodes.AssignNode a:
                        result.Add(new DisplayLine
                        {
                            Node = a, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}{a.VarName} = {ExprToSource(a.Value)}"
                        });
                        break;

                    case AstNodes.AssignFromMethodNode am:
                        string amArgs = string.Join(", ", am.Args.ConvertAll(ExprToSource));
                        result.Add(new DisplayLine
                        {
                            Node = am, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}{am.VarName} = {am.ObjectName}.{am.MethodName}({amArgs})"
                        });
                        break;

                    case AstNodes.ObjectDeclNode od:
                        string ctorArgs = string.Join(", ", od.ConstructorArgs.ConvertAll(ExprToSource));
                        result.Add(new DisplayLine
                        {
                            Node = od, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}{od.TypeName} {od.VarName} = new {od.TypeName}({ctorArgs})"
                        });
                        break;

                    case AstNodes.CallNode c:
                        string callArgs = string.Join(", ", c.Args.ConvertAll(ExprToSource));
                        result.Add(new DisplayLine
                        {
                            Node = c, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}{c.FunctionName}({callArgs})"
                        });
                        break;

                    case AstNodes.MethodCallNode mc:
                        string mcArgs = string.Join(", ", mc.Args.ConvertAll(ExprToSource));
                        result.Add(new DisplayLine
                        {
                            Node = mc, Parent = parent, IndexInParent = i, Depth = depth,
                            Text = $"{pad}{mc.ObjectName}.{mc.MethodName}({mcArgs})"
                        });
                        break;
                }
            }
        }

        void AppendIfDisplayLines(AstNodes.IfNode ifn, AstNodes.AstNode parent, int indexInParent,
            int depth, string pad, List<DisplayLine> result, bool isElif)
        {
            string keyword = isElif ? "elif" : "if";
            result.Add(new DisplayLine
            {
                Node = ifn, Parent = parent, IndexInParent = indexInParent, Depth = depth,
                Text = $"{pad}{keyword} {ExprToSource(ifn.Condition)}:", IsCompoundHeader = true
            });
            if (ifn.ThenBody.Count > 0)
                BuildDisplayLinesRecursive(ifn.ThenBody, ifn, depth + 1, result);
            else
                result.Add(new DisplayLine
                {
                    Node = null, Parent = ifn, IndexInParent = 0, Depth = depth + 1,
                    Text = $"{new string(' ', (depth + 1) * 4)}pass"
                });

            if (ifn.ElseBody.Count > 0)
            {
                if (ifn.ElseBody.Count == 1 && ifn.ElseBody[0] is AstNodes.IfNode elifNode)
                {
                    AppendIfDisplayLines(elifNode, ifn, 0, depth, pad, result, true);
                }
                else
                {
                    result.Add(new DisplayLine
                    {
                        Node = ifn, Parent = parent, IndexInParent = indexInParent, Depth = depth,
                        Text = $"{pad}else:"
                    });
                    BuildDisplayLinesRecursive(ifn.ElseBody, ifn, depth + 1, result);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  AST → SOURCE (Pretty Printer)
        // ═══════════════════════════════════════════════════════════

        public string ToSource()
        {
            var sb = new StringBuilder();
            foreach (var stmt in Statements)
                AppendStatement(sb, stmt, 0);
            return sb.ToString();
        }

        public string[] ToSourceLines()
        {
            string src = ToSource();
            if (string.IsNullOrEmpty(src)) return System.Array.Empty<string>();
            return src.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        // ═══════════════════════════════════════════════════════════
        //  COMPILE
        // ═══════════════════════════════════════════════════════════

        public CompiledProgram Compile(ICompilerExtension extension = null,
                                      Dictionary<string, object> metadata = null)
        {
            return PythonCompiler.Compile(ToSource(), Name, extension, metadata);
        }

        // ═══════════════════════════════════════════════════════════
        //  LOAD FROM SOURCE
        // ═══════════════════════════════════════════════════════════

        public void LoadFromSource(string source, CompilerContext ctx = null)
        {
            var compiler = new PythonCompiler();
            var program = compiler.Parse(source, ctx);
            Statements.Clear();
            Statements.AddRange(program.Statements);
            RenumberLines();
            _undoStack.Clear();
            _redoStack.Clear();
        }

        // ═══════════════════════════════════════════════════════════
        //  CLIPBOARD & DUPLICATE  (#1, #2)
        // ═══════════════════════════════════════════════════════════

        /// <summary>Deep-clone an AST node for clipboard/duplicate.</summary>
        public static AstNodes.AstNode DeepClone(AstNodes.AstNode node)
        {
            if (node == null) return null;
            switch (node)
            {
                case AstNodes.AssignNode a:
                    return new AstNodes.AssignNode
                    { VarName = a.VarName, Value = CloneExpr(a.Value) };
                case AstNodes.AssignFromMethodNode am:
                    return new AstNodes.AssignFromMethodNode
                    {
                        VarName = am.VarName, ObjectName = am.ObjectName,
                        MethodName = am.MethodName, Args = CloneExprList(am.Args)
                    };
                case AstNodes.CallNode c:
                    return new AstNodes.CallNode
                    { FunctionName = c.FunctionName, Args = CloneExprList(c.Args) };
                case AstNodes.MethodCallNode mc:
                    return new AstNodes.MethodCallNode
                    {
                        ObjectName = mc.ObjectName, MethodName = mc.MethodName,
                        Args = CloneExprList(mc.Args)
                    };
                case AstNodes.ObjectDeclNode od:
                    return new AstNodes.ObjectDeclNode
                    {
                        TypeName = od.TypeName, VarName = od.VarName,
                        ConstructorArgs = CloneExprList(od.ConstructorArgs)
                    };
                case AstNodes.WhileNode w:
                    return new AstNodes.WhileNode
                    {
                        IsInfinite = w.IsInfinite, Condition = CloneExpr(w.Condition),
                        Body = CloneStatements(w.Body)
                    };
                case AstNodes.ForNode f:
                    return new AstNodes.ForNode
                    {
                        VarName = f.VarName, RangeArgs = CloneExprList(f.RangeArgs),
                        Body = CloneStatements(f.Body)
                    };
                case AstNodes.IfNode ifn:
                    return new AstNodes.IfNode
                    {
                        Condition = CloneExpr(ifn.Condition),
                        ThenBody = CloneStatements(ifn.ThenBody),
                        ElseBody = CloneStatements(ifn.ElseBody)
                    };
                default: return null;
            }
        }

        static AstNodes.ExprNode CloneExpr(AstNodes.ExprNode expr)
        {
            if (expr == null) return null;
            return expr switch
            {
                AstNodes.NumberNode n => new AstNodes.NumberNode { Value = n.Value },
                AstNodes.VarNode v => new AstNodes.VarNode { Name = v.Name },
                AstNodes.BoolNode b => new AstNodes.BoolNode { Value = b.Value },
                AstNodes.StringNode s => new AstNodes.StringNode { Value = s.Value },
                AstNodes.BinaryOpNode bin => new AstNodes.BinaryOpNode
                {
                    Left = CloneExpr(bin.Left), Right = CloneExpr(bin.Right), Op = bin.Op
                },
                AstNodes.CallExprNode ce => new AstNodes.CallExprNode
                {
                    FunctionName = ce.FunctionName, Args = CloneExprList(ce.Args)
                },
                _ => new AstNodes.NumberNode { Value = 0 }
            };
        }

        static List<AstNodes.ExprNode> CloneExprList(List<AstNodes.ExprNode> list)
        {
            var result = new List<AstNodes.ExprNode>(list.Count);
            foreach (var e in list) result.Add(CloneExpr(e));
            return result;
        }

        static List<AstNodes.AstNode> CloneStatements(List<AstNodes.AstNode> stmts)
        {
            var result = new List<AstNodes.AstNode>(stmts.Count);
            foreach (var s in stmts) result.Add(DeepClone(s));
            return result;
        }

        /// <summary>Duplicate the node at the given display line, inserting a clone below it.</summary>
        public void Duplicate(DisplayLine dl)
        {
            if (dl.Node == null) return;
            var clone = DeepClone(dl.Node);
            if (clone == null) return;
            if (dl.Parent != null)
                InsertIntoBody(dl.Parent, dl.IndexInParent + 1, clone);
            else
                InsertAt(dl.IndexInParent + 1, clone);
        }

        // ═══════════════════════════════════════════════════════════
        //  SERIALIZATION — ToJson / FromJson  (#7)
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Serialize document to a simple JSON string (AST + name).
        /// Uses the source-roundtrip approach: stores source + name.
        /// </summary>
        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append($"\"name\":\"{EscapeJson(Name)}\",");
            sb.Append($"\"source\":\"{EscapeJson(ToSource())}\"");
            sb.Append('}');
            return sb.ToString();
        }

        /// <summary>
        /// Restore document from JSON (created by ToJson).
        /// </summary>
        public static CodeDocument FromJson(string json, CompilerContext ctx = null)
        {
            var doc = new CodeDocument();
            string name = ExtractJsonString(json, "name");
            string source = ExtractJsonString(json, "source");
            if (name != null) doc.Name = name;
            if (source != null) doc.LoadFromSource(source, ctx);
            return doc;
        }

        static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\n", "\\n").Replace("\r", "\\r")
                    .Replace("\t", "\\t");
        }

        static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\":\"";
            int start = json.IndexOf(pattern, StringComparison.Ordinal);
            if (start < 0) return null;
            start += pattern.Length;
            var sb = new StringBuilder();
            for (int i = start; i < json.Length; i++)
            {
                if (json[i] == '\\' && i + 1 < json.Length)
                {
                    char next = json[i + 1];
                    switch (next)
                    {
                        case '"': sb.Append('"'); i++; continue;
                        case '\\': sb.Append('\\'); i++; continue;
                        case 'n': sb.Append('\n'); i++; continue;
                        case 'r': sb.Append('\r'); i++; continue;
                        case 't': sb.Append('\t'); i++; continue;
                    }
                }
                if (json[i] == '"') break;
                sb.Append(json[i]);
            }
            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════════════
        //  INTERNALS
        // ═══════════════════════════════════════════════════════════

        void RenumberLines()
        {
            for (int i = 0; i < Statements.Count; i++)
                Statements[i].SourceLine = i + 1;
        }

        void AppendStatement(StringBuilder sb, AstNodes.AstNode node, int depth)
        {
            string pad = new string(' ', depth * 4);

            switch (node)
            {
                case AstNodes.WhileNode w:
                    sb.AppendLine($"{pad}while {ExprToSource(w.Condition)}:");
                    foreach (var s in w.Body) AppendStatement(sb, s, depth + 1);
                    if (w.Body.Count == 0) sb.AppendLine($"{new string(' ', (depth + 1) * 4)}pass");
                    break;

                case AstNodes.ForNode f:
                    string rangeArgs = string.Join(", ", f.RangeArgs.ConvertAll(ExprToSource));
                    sb.AppendLine($"{pad}for {f.VarName} in range({rangeArgs}):");
                    foreach (var s in f.Body) AppendStatement(sb, s, depth + 1);
                    if (f.Body.Count == 0) sb.AppendLine($"{new string(' ', (depth + 1) * 4)}pass");
                    break;

                case AstNodes.IfNode ifn:
                    sb.AppendLine($"{pad}if {ExprToSource(ifn.Condition)}:");
                    foreach (var s in ifn.ThenBody) AppendStatement(sb, s, depth + 1);
                    if (ifn.ThenBody.Count == 0) sb.AppendLine($"{new string(' ', (depth + 1) * 4)}pass");
                    if (ifn.ElseBody.Count > 0)
                    {
                        if (ifn.ElseBody.Count == 1 && ifn.ElseBody[0] is AstNodes.IfNode elifNode)
                        {
                            sb.Append($"{pad}el");
                            AppendStatement(sb, elifNode, depth);
                        }
                        else
                        {
                            sb.AppendLine($"{pad}else:");
                            foreach (var s in ifn.ElseBody) AppendStatement(sb, s, depth + 1);
                        }
                    }
                    break;

                case AstNodes.AssignNode a:
                    sb.AppendLine($"{pad}{a.VarName} = {ExprToSource(a.Value)}");
                    break;

                case AstNodes.AssignFromMethodNode am:
                    string amArgs = string.Join(", ", am.Args.ConvertAll(ExprToSource));
                    sb.AppendLine($"{pad}{am.VarName} = {am.ObjectName}.{am.MethodName}({amArgs})");
                    break;

                case AstNodes.ObjectDeclNode od:
                    string ctorArgs = string.Join(", ", od.ConstructorArgs.ConvertAll(ExprToSource));
                    sb.AppendLine($"{pad}{od.TypeName} {od.VarName} = new {od.TypeName}({ctorArgs})");
                    break;

                case AstNodes.CallNode c:
                    string callArgs = string.Join(", ", c.Args.ConvertAll(ExprToSource));
                    sb.AppendLine($"{pad}{c.FunctionName}({callArgs})");
                    break;

                case AstNodes.MethodCallNode mc:
                    string mcArgs = string.Join(", ", mc.Args.ConvertAll(ExprToSource));
                    sb.AppendLine($"{pad}{mc.ObjectName}.{mc.MethodName}({mcArgs})");
                    break;
            }
        }

        static string ExprToSource(AstNodes.ExprNode expr)
        {
            return expr switch
            {
                AstNodes.NumberNode n => n.Value % 1 == 0
                    ? ((int)n.Value).ToString()
                    : n.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
                AstNodes.VarNode v => v.Name,
                AstNodes.BoolNode b => b.Value ? "True" : "False",
                AstNodes.StringNode s => $"\"{s.Value}\"",
                AstNodes.BinaryOpNode bin =>
                    $"{ExprToSource(bin.Left)} {bin.Op} {ExprToSource(bin.Right)}",
                AstNodes.CallExprNode ce =>
                    $"{ce.FunctionName}({string.Join(", ", ce.Args.ConvertAll(ExprToSource))})",
                _ => "0"
            };
        }

        static string ExprToSource(AstNodes.AstNode node)
        {
            return node is AstNodes.ExprNode expr ? ExprToSource(expr) : "0";
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  EDIT ACTIONS (Undo/Redo)  (#1)
    // ═══════════════════════════════════════════════════════════════

    abstract class EditAction
    {
        public abstract void Undo(CodeDocument doc);
        public abstract void Redo(CodeDocument doc);

        protected static List<AstNodes.AstNode> GetList(CodeDocument doc, AstNodes.AstNode parent)
        {
            if (parent == null) return doc.Statements;
            return CodeDocument.GetBody(parent) ?? doc.Statements;
        }
    }

    sealed class InsertAction : EditAction
    {
        readonly AstNodes.AstNode _parent;
        readonly int _index;
        readonly AstNodes.AstNode _node;

        public InsertAction(AstNodes.AstNode parent, int index, AstNodes.AstNode node)
        { _parent = parent; _index = index; _node = node; }

        public override void Undo(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            if (_index < list.Count) list.RemoveAt(_index);
        }

        public override void Redo(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            list.Insert(Math.Min(_index, list.Count), _node);
        }
    }

    sealed class RemoveAction : EditAction
    {
        readonly AstNodes.AstNode _parent;
        readonly int _index;
        readonly AstNodes.AstNode _node;

        public RemoveAction(AstNodes.AstNode parent, int index, AstNodes.AstNode node)
        { _parent = parent; _index = index; _node = node; }

        public override void Undo(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            list.Insert(Math.Min(_index, list.Count), _node);
        }

        public override void Redo(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            if (_index < list.Count) list.RemoveAt(_index);
        }
    }

    sealed class ReplaceAction : EditAction
    {
        readonly AstNodes.AstNode _parent;
        readonly int _index;
        readonly AstNodes.AstNode _oldNode, _newNode;

        public ReplaceAction(AstNodes.AstNode parent, int index,
            AstNodes.AstNode oldNode, AstNodes.AstNode newNode)
        { _parent = parent; _index = index; _oldNode = oldNode; _newNode = newNode; }

        public override void Undo(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            if (_index < list.Count) list[_index] = _oldNode;
        }

        public override void Redo(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            if (_index < list.Count) list[_index] = _newNode;
        }
    }

    sealed class SwapAction : EditAction
    {
        readonly AstNodes.AstNode _parent;
        readonly int _indexA, _indexB;

        public SwapAction(AstNodes.AstNode parent, int indexA, int indexB)
        { _parent = parent; _indexA = indexA; _indexB = indexB; }

        public override void Undo(CodeDocument doc) => DoSwap(doc);
        public override void Redo(CodeDocument doc) => DoSwap(doc);

        void DoSwap(CodeDocument doc)
        {
            var list = GetList(doc, _parent);
            if (_indexA < list.Count && _indexB < list.Count)
                (list[_indexA], list[_indexB]) = (list[_indexB], list[_indexA]);
        }
    }
}
