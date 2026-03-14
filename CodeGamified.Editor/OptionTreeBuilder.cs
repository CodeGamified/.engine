// CodeGamified.Editor — Tap-to-code editor for mobile
// MIT License
using System;
using System.Collections.Generic;
using System.Linq;
using CodeGamified.Engine.Compiler;

namespace CodeGamified.Editor
{
    /// <summary>
    /// Builds context-sensitive option trees from the AST grammar,
    /// ICompilerExtension (game builtins), and current document state.
    ///
    /// The tree is rebuilt each time the cursor context changes.
    /// Games provide an IEditorExtension to inject game-specific options.
    /// </summary>
    public class OptionTreeBuilder
    {
        readonly ICompilerExtension _compilerExt;
        readonly IEditorExtension _editorExt;

        /// <summary>Cached reference to the current doc during tree building.</summary>
        CodeDocument _currentDoc;

        public OptionTreeBuilder(ICompilerExtension compilerExt, IEditorExtension editorExt = null)
        {
            _compilerExt = compilerExt;
            _editorExt = editorExt;
        }

        // ═══════════════════════════════════════════════════════════
        //  TOP-LEVEL: WHAT TO SHOW FOR CURRENT CONTEXT
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Build the root option list for the cursor's current position.
        /// Uses the DisplayLine at cursor.Line to determine context.
        /// </summary>
        public List<OptionNode> BuildRoot(CodeDocument doc, EditorCursor cursor)
        {
            _currentDoc = doc;
            var displayLines = doc.BuildDisplayLines();
            if (displayLines.Count == 0)
                return BuildNewStatementOptions(doc, cursor, null, 0);

            if (cursor.Line < 0) cursor.Line = 0;
            if (cursor.Line >= displayLines.Count) cursor.Line = displayLines.Count - 1;

            var dl = displayLines[cursor.Line];
            if (dl.Node == null)
                return BuildBodyInsertOptions(doc, cursor, dl);

            return BuildStatementActions(doc, cursor, dl);
        }

        // ═══════════════════════════════════════════════════════════
        //  STATEMENT-LEVEL ACTIONS (cursor on existing line)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildStatementActions(CodeDocument doc, EditorCursor cursor,
            CodeDocument.DisplayLine dl)
        {
            var node = dl.Node;
            var options = new List<OptionNode>();

            // Edit — drill into sub-expressions
            var editChildren = BuildEditOptions(doc, cursor, node);
            if (editChildren.Count > 0)
            {
                options.Add(new OptionNode
                {
                    Label = "Edit", Glyph = "◈", Hint = "modify this line",
                    Children = editChildren
                });
            }

            // If this is a compound header (while/for/if), offer "Add inside" (#3)
            if (dl.IsCompoundHeader)
            {
                var body = CodeDocument.GetBody(node);
                int bodyInsertAt = body?.Count ?? 0;
                options.Add(new OptionNode
                {
                    Label = "Add inside", Glyph = "↳", Hint = "add to body",
                    Children = BuildNewStatementOptions(doc, cursor, node, bodyInsertAt)
                });
            }

            // elif / else insertion (#8) — only on IfNode
            if (node is AstNodes.IfNode ifn)
            {
                var elifElseOptions = BuildElifElseOptions(doc, cursor, ifn);
                if (elifElseOptions.Count > 0)
                    options.AddRange(elifElseOptions);
            }

            // Insert above (into parent's list)
            options.Add(new OptionNode
            {
                Label = "Insert above", Glyph = "↑",
                Children = BuildNewStatementOptions(doc, cursor, dl.Parent, dl.IndexInParent)
            });

            // Insert below (into parent's list)
            options.Add(new OptionNode
            {
                Label = "Insert below", Glyph = "↓",
                Children = BuildNewStatementOptions(doc, cursor, dl.Parent, dl.IndexInParent + 1)
            });

            // Move up/down (#10)
            options.Add(new OptionNode
            {
                Label = "Move up", Glyph = "▲",
                Disabled = dl.IndexInParent <= 0,
                Apply = (d, c) =>
                {
                    if (dl.Parent != null)
                        d.MoveUpInBody(dl.Parent, dl.IndexInParent);
                    else
                        d.MoveUp(dl.IndexInParent);
                    if (c.Line > 0) c.Line--;
                    c.ClearStack();
                }
            });

            options.Add(new OptionNode
            {
                Label = "Move down", Glyph = "▼",
                Apply = (d, c) =>
                {
                    if (dl.Parent != null)
                        d.MoveDownInBody(dl.Parent, dl.IndexInParent);
                    else
                        d.MoveDown(dl.IndexInParent);
                    if (c.Line < doc.BuildDisplayLines().Count - 1) c.Line++;
                    c.ClearStack();
                }
            });

            // Delete — with confirmation for compound nodes (#5)
            bool isCompound = dl.Node is AstNodes.WhileNode or AstNodes.ForNode or AstNodes.IfNode;
            bool hasBody = false;
            if (isCompound)
            {
                var body = CodeDocument.GetBody(dl.Node);
                hasBody = body != null && body.Count > 0;
                // IfNode: also check ElseBody
                if (!hasBody && dl.Node is AstNodes.IfNode ifCheck)
                    hasBody = ifCheck.ElseBody.Count > 0;
            }

            if (hasBody)
            {
                // Compound with non-empty body → confirm before deleting
                options.Add(new OptionNode
                {
                    Label = "Delete", Glyph = "✗", Hint = "has body!",
                    Children = new List<OptionNode>
                    {
                        new OptionNode
                        {
                            Label = "Confirm delete (body will be lost)", Glyph = "⚠",
                            Apply = (d, c) =>
                            {
                                if (dl.Parent != null)
                                    d.RemoveFromBody(dl.Parent, dl.IndexInParent);
                                else
                                    d.RemoveAt(dl.IndexInParent);
                                var newDisplay = d.BuildDisplayLines();
                                if (c.Line >= newDisplay.Count && newDisplay.Count > 0)
                                    c.Line = newDisplay.Count - 1;
                                c.ClearStack();
                            }
                        },
                        new OptionNode { Label = "Cancel", Glyph = "←", Apply = (d, c) => c.ClearStack() }
                    }
                });
            }
            else
            {
                options.Add(new OptionNode
                {
                    Label = "Delete", Glyph = "✗",
                    Apply = (d, c) =>
                    {
                        if (dl.Parent != null)
                            d.RemoveFromBody(dl.Parent, dl.IndexInParent);
                        else
                            d.RemoveAt(dl.IndexInParent);
                        var newDisplay = d.BuildDisplayLines();
                        if (c.Line >= newDisplay.Count && newDisplay.Count > 0)
                            c.Line = newDisplay.Count - 1;
                        c.ClearStack();
                    }
                });
            }

            // Duplicate (#2)
            options.Add(new OptionNode
            {
                Label = "Duplicate", Glyph = "⊕",
                Apply = (d, c) =>
                {
                    d.Duplicate(dl);
                    c.ClearStack();
                }
            });

            // Copy (#1)
            options.Add(new OptionNode
            {
                Label = "Copy", Glyph = "◫",
                Apply = (d, c) =>
                {
                    c.ClipboardNode = CodeDocument.DeepClone(dl.Node);
                    c.ClearStack();
                }
            });

            // Cut (#1)
            options.Add(new OptionNode
            {
                Label = "Cut", Glyph = "✂",
                Apply = (d, c) =>
                {
                    c.ClipboardNode = CodeDocument.DeepClone(dl.Node);
                    if (dl.Parent != null)
                        d.RemoveFromBody(dl.Parent, dl.IndexInParent);
                    else
                        d.RemoveAt(dl.IndexInParent);
                    var newDisplay = d.BuildDisplayLines();
                    if (c.Line >= newDisplay.Count && newDisplay.Count > 0)
                        c.Line = newDisplay.Count - 1;
                    c.ClearStack();
                }
            });

            // Paste below (#1)
            options.Add(new OptionNode
            {
                Label = "Paste below", Glyph = "◧",
                Disabled = cursor.ClipboardNode == null,
                Hint = cursor.ClipboardNode == null ? "clipboard empty" : null,
                Apply = cursor.ClipboardNode != null
                    ? (d, c) =>
                    {
                        var clone = CodeDocument.DeepClone(c.ClipboardNode);
                        if (dl.Parent != null)
                            d.InsertIntoBody(dl.Parent, dl.IndexInParent + 1, clone);
                        else
                            d.InsertAt(dl.IndexInParent + 1, clone);
                        c.ClearStack();
                    }
                    : null
            });

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  BODY INSERT (cursor on a "pass" placeholder line)  (#3)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildBodyInsertOptions(CodeDocument doc, EditorCursor cursor,
            CodeDocument.DisplayLine dl)
        {
            // dl.Parent is the compound node, dl.IndexInParent is where to insert
            var options = BuildNewStatementOptions(doc, cursor, dl.Parent, dl.IndexInParent);

            // Also offer Paste if clipboard has content (#4 + #1)
            if (cursor.ClipboardNode != null)
            {
                options.Insert(0, new OptionNode
                {
                    Label = "Paste from clipboard", Glyph = "◧",
                    Apply = (d, c) =>
                    {
                        var clone = CodeDocument.DeepClone(c.ClipboardNode);
                        InsertNode(d, c, dl.Parent, dl.IndexInParent, clone);
                    }
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  INSERT: NEW STATEMENT OPTIONS
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Options for creating a new statement.
        /// parent = null means top-level; otherwise inserts into parent's body.
        /// </summary>
        List<OptionNode> BuildNewStatementOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex)
        {
            var options = new List<OptionNode>();

            // Variable assignment: x = <value>
            options.Add(new OptionNode
            {
                Label = "Variable", Glyph = "◆", Hint = "x = ...",
                Children = BuildVarNameOptions(doc, cursor, parent, insertIndex)
            });

            // Compound assignment: x += 1  (#5)
            var existingVars = GetDeclaredVariables(doc);
            if (existingVars.Count > 0)
            {
                options.Add(new OptionNode
                {
                    Label = "Increment", Glyph = "◆", Hint = "x += ...",
                    Children = BuildCompoundAssignOptions(doc, cursor, parent, insertIndex, "+")
                });
                options.Add(new OptionNode
                {
                    Label = "Decrement", Glyph = "◆", Hint = "x -= ...",
                    Children = BuildCompoundAssignOptions(doc, cursor, parent, insertIndex, "-")
                });
            }

            // Function call: func(args)
            var builtinFuncs = new List<OptionNode>();
            builtinFuncs.Add(new OptionNode
            {
                Label = "wait", Glyph = "◇", Hint = "wait(seconds)",
                Children = BuildNumberPickerOptions("wait", parent, insertIndex)
            });

            // Game-specific functions
            if (_editorExt != null)
            {
                foreach (var fn in _editorExt.GetAvailableFunctions())
                {
                    var captured = fn;
                    builtinFuncs.Add(new OptionNode
                    {
                        Label = captured.Name, Glyph = "◇", Hint = captured.Hint,
                        Children = captured.ArgCount > 0
                            ? BuildFuncArgOptions(captured.Name, captured.ArgCount, parent, insertIndex)
                            : null,
                        Apply = captured.ArgCount == 0
                            ? (d, c) => InsertNode(d, c, parent, insertIndex,
                                new AstNodes.CallNode { FunctionName = captured.Name, Args = new() })
                            : null
                    });
                }
            }

            options.Add(new OptionNode
            {
                Label = "Function", Glyph = "◆", Hint = "call()",
                Children = builtinFuncs
            });

            // Object declaration
            if (_editorExt != null)
            {
                var types = _editorExt.GetAvailableTypes();
                if (types.Count > 0)
                {
                    options.Add(new OptionNode
                    {
                        Label = "Object", Glyph = "◆", Hint = "Type x = new Type()",
                        Children = BuildObjectDeclOptions(doc, parent, insertIndex, types)
                    });
                }
            }

            // Method call: obj.method(args)
            var declaredObjects = GetDeclaredObjects(doc);
            if (declaredObjects.Count > 0)
            {
                options.Add(new OptionNode
                {
                    Label = "Method call", Glyph = "◆", Hint = "obj.method()",
                    Children = BuildMethodCallObjectOptions(doc, cursor, parent, insertIndex, declaredObjects)
                });
            }

            // while loop — tier-gated (#2)
            bool whileAllowed = _editorExt?.IsWhileLoopAllowed() ?? true;
            string whileGate = _editorExt?.GetWhileLoopGateReason() ?? "";
            options.Add(new OptionNode
            {
                Label = "while loop", Glyph = "◆",
                Hint = whileAllowed ? "while ...:" : whileGate,
                Disabled = !whileAllowed,
                Children = whileAllowed
                    ? BuildWhileConditionOptions(doc, cursor, parent, insertIndex)
                    : null
            });

            // for loop — tier-gated (#2)
            bool forAllowed = _editorExt?.IsForLoopAllowed() ?? true;
            string forGate = _editorExt?.GetForLoopGateReason() ?? "";
            options.Add(new OptionNode
            {
                Label = "for loop", Glyph = "◆",
                Hint = forAllowed ? "for i in range():" : forGate,
                Disabled = !forAllowed,
                Children = forAllowed
                    ? BuildForRangeOptions(doc, cursor, parent, insertIndex)
                    : null
            });

            // if statement
            options.Add(new OptionNode
            {
                Label = "if statement", Glyph = "◆", Hint = "if ...:",
                Children = BuildConditionOptions(doc, cursor, parent, insertIndex, isWhile: false)
            });

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  COMPOUND ASSIGNMENT: x += val  (#5)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildCompoundAssignOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex, string op)
        {
            var options = new List<OptionNode>();
            var vars = GetDeclaredVariables(doc);

            foreach (var name in vars)
            {
                var capturedName = name;
                options.Add(new OptionNode
                {
                    Label = capturedName, Glyph = "◇", Hint = $"{capturedName} {op}= ...",
                    Children = BuildSimpleValueOptions(doc, "value", (valExpr) =>
                    {
                        return new OptionNode
                        {
                            Label = $"{capturedName} {op}= {ExprPreview(valExpr)}", Glyph = "·",
                            Apply = (d, c) =>
                            {
                                // x += v  →  x = x + v
                                var assign = new AstNodes.AssignNode
                                {
                                    VarName = capturedName,
                                    Value = new AstNodes.BinaryOpNode
                                    {
                                        Left = new AstNodes.VarNode { Name = capturedName },
                                        Right = valExpr,
                                        Op = op
                                    }
                                };
                                InsertNode(d, c, parent, insertIndex, assign);
                            }
                        };
                    })
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  elif / else INSERTION  (#8)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildElifElseOptions(CodeDocument doc, EditorCursor cursor, AstNodes.IfNode ifn)
        {
            var options = new List<OptionNode>();

            // Walk to the tail of the elif chain
            var tail = ifn;
            while (tail.ElseBody.Count == 1 && tail.ElseBody[0] is AstNodes.IfNode next)
                tail = next;

            // Only offer elif/else if the tail doesn't already have an else body
            if (tail.ElseBody.Count > 0) return options;

            // Add elif
            var compOps = new[] { "<", ">", "==", "!=", "<=", ">=" };
            var elifChildren = new List<OptionNode>();

            foreach (var op in compOps)
            {
                var capturedOp = op;
                elifChildren.Add(new OptionNode
                {
                    Label = op, Glyph = "·", Hint = $"var {op} val",
                    Children = BuildSimpleValueOptions(doc, "left", (left) =>
                    {
                        return BuildSimpleValueOptions(doc, "right", (right) =>
                        {
                            var condExpr = new AstNodes.BinaryOpNode
                            {
                                Left = left, Right = right, Op = capturedOp
                            };
                            return new OptionNode
                            {
                                Label = $"elif {ExprPreview(left)} {capturedOp} {ExprPreview(right)}",
                                Glyph = "·",
                                Apply = (d, c) =>
                                {
                                    tail.ElseBody = new List<AstNodes.AstNode>
                                    {
                                        new AstNodes.IfNode
                                        {
                                            Condition = condExpr,
                                            ThenBody = new List<AstNodes.AstNode>()
                                        }
                                    };
                                    c.ClearStack();
                                }
                            };
                        });
                    })
                });
            }

            options.Add(new OptionNode
            {
                Label = "Add elif", Glyph = "◈", Hint = "elif ...:",
                Children = elifChildren
            });

            // Add else
            options.Add(new OptionNode
            {
                Label = "Add else", Glyph = "◈", Hint = "else:",
                Apply = (d, c) =>
                {
                    tail.ElseBody = new List<AstNodes.AstNode>();
                    c.ClearStack();
                }
            });

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  VARIABLE ASSIGNMENT FLOW: name → value  (#6 name suggestions)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildVarNameOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex)
        {
            var options = new List<OptionNode>();
            var existing = GetDeclaredVariables(doc);

            // Offer existing variable names first
            foreach (var name in existing)
            {
                var captured = name;
                options.Add(new OptionNode
                {
                    Label = captured, Glyph = "◇",
                    Children = BuildValueOptions(doc, (expr) =>
                    {
                        return (d, c) =>
                        {
                            InsertNode(d, c, parent, insertIndex,
                                new AstNodes.AssignNode { VarName = captured, Value = expr });
                        };
                    })
                });
            }

            // Game-contextual name suggestions (#6) then generic fallbacks
            var suggestions = _editorExt?.GetVariableNameSuggestions() ?? new List<string>();
            var genericNames = new[] { "x", "y", "z", "i", "n", "count", "total", "result" };
            var allNames = new List<string>(suggestions);
            foreach (var g in genericNames)
                if (!allNames.Contains(g)) allNames.Add(g);

            foreach (var name in allNames)
            {
                if (existing.Contains(name)) continue;
                var captured = name;
                bool isGameSuggestion = suggestions.Contains(name);
                options.Add(new OptionNode
                {
                    Label = captured, Glyph = "◇",
                    Hint = isGameSuggestion ? "suggested" : "new",
                    Children = BuildValueOptions(doc, (expr) =>
                    {
                        return (d, c) =>
                        {
                            InsertNode(d, c, parent, insertIndex,
                                new AstNodes.AssignNode { VarName = captured, Value = expr });
                        };
                    })
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  VALUE PICKER
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildValueOptions(CodeDocument doc,
            Func<AstNodes.ExprNode, Action<CodeDocument, EditorCursor>> onPick)
        {
            var options = new List<OptionNode>();

            // Common numbers
            foreach (float val in new[] { 0f, 1f, 2f, 3f, 5f, 10f, 0.5f, 100f })
            {
                var expr = new AstNodes.NumberNode { Value = val };
                string label = val % 1 == 0 ? ((int)val).ToString() : val.ToString();
                options.Add(new OptionNode
                {
                    Label = label, Glyph = "·",
                    Apply = onPick(expr)
                });
            }

            // Existing variables
            foreach (var name in GetDeclaredVariables(doc))
            {
                var expr = new AstNodes.VarNode { Name = name };
                options.Add(new OptionNode
                {
                    Label = name, Glyph = "◇", Hint = "var",
                    Apply = onPick(expr)
                });
            }

            // Expression: left op right
            options.Add(new OptionNode
            {
                Label = "Expression", Glyph = "◈", Hint = "a + b",
                Children = BuildBinaryExprOptions(doc, onPick)
            });

            // Method result: obj.method()
            var objects = GetDeclaredObjects(doc);
            if (objects.Count > 0 && _editorExt != null)
            {
                options.Add(new OptionNode
                {
                    Label = "Method result", Glyph = "◈", Hint = "obj.read()",
                    Children = BuildMethodResultOptions(doc, objects, onPick)
                });
            }

            // String literal (#10)
            options.Add(new OptionNode
            {
                Label = "String", Glyph = "◈", Hint = "\"text\"",
                Children = BuildStringLiteralOptions(doc, onPick)
            });

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  EXPRESSION BUILDERS
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildBinaryExprOptions(CodeDocument doc,
            Func<AstNodes.ExprNode, Action<CodeDocument, EditorCursor>> onPick)
        {
            var ops = new[] { "+", "-", "*", "/", "%" };
            var options = new List<OptionNode>();

            foreach (var op in ops)
            {
                var capturedOp = op;
                options.Add(new OptionNode
                {
                    Label = op, Glyph = "·",
                    Children = BuildBinaryOperandOptions(doc, capturedOp, onPick)
                });
            }

            return options;
        }

        List<OptionNode> BuildBinaryOperandOptions(CodeDocument doc, string op,
            Func<AstNodes.ExprNode, Action<CodeDocument, EditorCursor>> onPick)
        {
            return BuildSimpleValueOptions(doc, "left", (left) =>
            {
                return BuildSimpleValueOptions(doc, "right", (right) =>
                {
                    var expr = new AstNodes.BinaryOpNode { Left = left, Right = right, Op = op };
                    return new OptionNode
                    {
                        Label = $"{ExprPreview(left)} {op} {ExprPreview(right)}", Glyph = "·",
                        Apply = onPick(expr)
                    };
                });
            });
        }

        List<OptionNode> BuildSimpleValueOptions(CodeDocument doc, string hint,
            Func<AstNodes.ExprNode, List<OptionNode>> onPick)
        {
            var options = new List<OptionNode>();
            foreach (float val in new[] { 0f, 1f, 2f, 3f, 5f, 10f })
            {
                var expr = new AstNodes.NumberNode { Value = val };
                string label = ((int)val).ToString();
                var children = onPick(expr);
                options.Add(new OptionNode { Label = label, Glyph = "·", Hint = hint, Children = children });
            }
            foreach (var name in GetDeclaredVariables(doc))
            {
                var expr = new AstNodes.VarNode { Name = name };
                var children = onPick(expr);
                options.Add(new OptionNode { Label = name, Glyph = "◇", Hint = hint, Children = children });
            }
            return options;
        }

        List<OptionNode> BuildSimpleValueOptions(CodeDocument doc, string hint,
            Func<AstNodes.ExprNode, OptionNode> onPick)
        {
            var options = new List<OptionNode>();
            foreach (float val in new[] { 0f, 1f, 2f, 3f, 5f, 10f })
            {
                var expr = new AstNodes.NumberNode { Value = val };
                options.Add(onPick(expr));
            }
            foreach (var name in GetDeclaredVariables(doc))
            {
                var expr = new AstNodes.VarNode { Name = name };
                options.Add(onPick(expr));
            }
            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  CONDITION BUILDERS (while / if)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildWhileConditionOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex)
        {
            var options = new List<OptionNode>
            {
                new()
                {
                    Label = "True", Glyph = "·", Hint = "infinite loop",
                    Apply = (d, c) =>
                    {
                        InsertNode(d, c, parent, insertIndex, new AstNodes.WhileNode
                        {
                            IsInfinite = true,
                            Condition = new AstNodes.BoolNode { Value = true },
                            Body = new List<AstNodes.AstNode>()
                        });
                    }
                }
            };

            options.AddRange(BuildConditionOptions(doc, cursor, parent, insertIndex, isWhile: true));
            return options;
        }

        List<OptionNode> BuildConditionOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex, bool isWhile)
        {
            var compOps = new[] { "<", ">", "==", "!=", "<=", ">=" };
            var options = new List<OptionNode>();

            foreach (var op in compOps)
            {
                var capturedOp = op;
                options.Add(new OptionNode
                {
                    Label = op, Glyph = "·", Hint = $"var {op} val",
                    Children = BuildSimpleValueOptions(doc, "left", (left) =>
                    {
                        return BuildSimpleValueOptions(doc, "right", (right) =>
                        {
                            var condExpr = new AstNodes.BinaryOpNode
                            {
                                Left = left, Right = right, Op = capturedOp
                            };
                            return new OptionNode
                            {
                                Label = $"{ExprPreview(left)} {capturedOp} {ExprPreview(right)}",
                                Glyph = "·",
                                Apply = (d, c) =>
                                {
                                    AstNodes.AstNode node = isWhile
                                        ? new AstNodes.WhileNode
                                        {
                                            Condition = condExpr,
                                            Body = new List<AstNodes.AstNode>()
                                        }
                                        : new AstNodes.IfNode
                                        {
                                            Condition = condExpr,
                                            ThenBody = new List<AstNodes.AstNode>()
                                        };
                                    InsertNode(d, c, parent, insertIndex, node);
                                }
                            };
                        });
                    })
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  FOR LOOP BUILDER  (tier-gated via caller)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildForRangeOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex)
        {
            var varNames = new[] { "i", "j", "k", "n" };
            var options = new List<OptionNode>();

            foreach (var name in varNames)
            {
                var capturedName = name;
                options.Add(new OptionNode
                {
                    Label = capturedName, Glyph = "◇", Hint = $"for {capturedName} in range()",
                    Children = BuildSimpleValueOptions(doc, "range end", (endExpr) =>
                    {
                        return new OptionNode
                        {
                            Label = $"range({ExprPreview(endExpr)})", Glyph = "·",
                            Apply = (d, c) =>
                            {
                                InsertNode(d, c, parent, insertIndex, new AstNodes.ForNode
                                {
                                    VarName = capturedName,
                                    RangeArgs = new List<AstNodes.ExprNode> { endExpr },
                                    Body = new List<AstNodes.AstNode>()
                                });
                            }
                        };
                    })
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  OBJECT DECLARATION
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildObjectDeclOptions(CodeDocument doc,
            AstNodes.AstNode parent, int insertIndex, List<EditorTypeInfo> types)
        {
            var options = new List<OptionNode>();

            foreach (var type in types)
            {
                var capturedType = type;
                var varName = type.Name.ToLower();
                options.Add(new OptionNode
                {
                    Label = capturedType.Name, Glyph = "◆", Hint = capturedType.Hint,
                    Apply = (d, c) =>
                    {
                        InsertNode(d, c, parent, insertIndex, new AstNodes.ObjectDeclNode
                        {
                            TypeName = capturedType.Name,
                            VarName = varName,
                            ConstructorArgs = new List<AstNodes.ExprNode>()
                        });
                    }
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  METHOD CALL (statement + expression forms)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildMethodCallObjectOptions(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int insertIndex, List<DeclaredObject> objects)
        {
            var options = new List<OptionNode>();

            foreach (var obj in objects)
            {
                if (_editorExt == null) continue;
                var methods = _editorExt.GetMethodsForType(obj.TypeName);
                if (methods.Count == 0) continue;

                var capturedObj = obj;
                var methodOptions = new List<OptionNode>();

                foreach (var method in methods)
                {
                    var capturedMethod = method;
                    methodOptions.Add(new OptionNode
                    {
                        Label = capturedMethod.Name, Glyph = "◇", Hint = capturedMethod.Hint,
                        Apply = capturedMethod.ArgCount == 0
                            ? (d, c) =>
                            {
                                InsertNode(d, c, parent, insertIndex, new AstNodes.MethodCallNode
                                {
                                    ObjectName = capturedObj.Name,
                                    MethodName = capturedMethod.Name,
                                    Args = new()
                                });
                            }
                            : null,
                        Children = capturedMethod.ArgCount > 0
                            ? BuildMethodArgOptions(capturedObj.Name, capturedMethod.Name,
                                capturedMethod.ArgCount, parent, insertIndex)
                            : null
                    });
                }

                options.Add(new OptionNode
                {
                    Label = capturedObj.Name, Glyph = "◆",
                    Hint = capturedObj.TypeName,
                    Children = methodOptions
                });
            }

            return options;
        }

        List<OptionNode> BuildMethodResultOptions(CodeDocument doc, List<DeclaredObject> objects,
            Func<AstNodes.ExprNode, Action<CodeDocument, EditorCursor>> onPick)
        {
            var options = new List<OptionNode>();
            if (_editorExt == null) return options;

            foreach (var obj in objects)
            {
                var methods = _editorExt.GetMethodsForType(obj.TypeName);
                var capturedObj = obj;
                foreach (var method in methods)
                {
                    if (!method.HasReturnValue) continue;
                    var capturedMethod = method;
                    options.Add(new OptionNode
                    {
                        Label = $"{capturedObj.Name}.{capturedMethod.Name}()",
                        Glyph = "◇",
                        Apply = onPick(new AstNodes.CallExprNode
                        {
                            FunctionName = $"{capturedObj.Name}.{capturedMethod.Name}"
                        })
                    });
                }
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  EDIT EXISTING STATEMENT
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildEditOptions(CodeDocument doc, EditorCursor cursor, AstNodes.AstNode node)
        {
            var options = new List<OptionNode>();

            switch (node)
            {
                case AstNodes.AssignNode assign:
                    options.Add(new OptionNode
                    {
                        Label = $"name: {assign.VarName}", Glyph = "◇",
                        Children = BuildRenameOptions(doc, cursor, assign)
                    });
                    options.Add(new OptionNode
                    {
                        Label = $"value: {ExprPreview(assign.Value)}", Glyph = "◇",
                        Children = BuildValueOptions(doc, (expr) => (d, c) =>
                        {
                            assign.Value = expr;
                            c.ClearStack();
                        })
                    });
                    break;

                case AstNodes.CallNode call:
                    if (call.Args.Count > 0)
                    {
                        for (int i = 0; i < call.Args.Count; i++)
                        {
                            int argIdx = i;
                            options.Add(new OptionNode
                            {
                                Label = $"arg {i}: {ExprPreview(call.Args[i])}", Glyph = "◇",
                                Children = BuildValueOptions(doc, (expr) => (d, c) =>
                                {
                                    call.Args[argIdx] = expr;
                                    c.ClearStack();
                                })
                            });
                        }
                    }
                    break;

                case AstNodes.WhileNode wh:
                    if (!wh.IsInfinite)
                    {
                        options.Add(new OptionNode
                        {
                            Label = $"condition: {ExprPreview(wh.Condition)}", Glyph = "◇",
                            Children = BuildValueOptions(doc, (expr) => (d, c) =>
                            {
                                wh.Condition = expr;
                                c.ClearStack();
                            })
                        });
                    }
                    break;

                case AstNodes.ForNode forNode:
                    options.Add(new OptionNode
                    {
                        Label = $"range end: {ExprPreview(forNode.RangeArgs.LastOrDefault())}", Glyph = "◇",
                        Children = BuildValueOptions(doc, (expr) => (d, c) =>
                        {
                            if (forNode.RangeArgs.Count > 0)
                                forNode.RangeArgs[forNode.RangeArgs.Count - 1] = expr;
                            else
                                forNode.RangeArgs.Add(expr);
                            c.ClearStack();
                        })
                    });
                    break;

                case AstNodes.MethodCallNode mc:
                    if (mc.Args.Count > 0)
                    {
                        for (int i = 0; i < mc.Args.Count; i++)
                        {
                            int argIdx = i;
                            options.Add(new OptionNode
                            {
                                Label = $"arg {i}: {ExprPreview(mc.Args[i])}", Glyph = "◇",
                                Children = BuildValueOptions(doc, (expr) => (d, c) =>
                                {
                                    mc.Args[argIdx] = expr;
                                    c.ClearStack();
                                })
                            });
                        }
                    }
                    break;
            }

            return options;
        }

        List<OptionNode> BuildRenameOptions(CodeDocument doc, EditorCursor cursor, AstNodes.AssignNode assign)
        {
            var options = new List<OptionNode>();

            // Game suggestions first (#6)
            var suggestions = _editorExt?.GetVariableNameSuggestions() ?? new List<string>();
            var allNames = new List<string>(suggestions);
            foreach (var g in new[] { "x", "y", "z", "i", "n", "count", "total", "result" })
                if (!allNames.Contains(g)) allNames.Add(g);

            foreach (var name in allNames)
            {
                var captured = name;
                options.Add(new OptionNode
                {
                    Label = captured, Glyph = "◇",
                    Apply = (d, c) =>
                    {
                        assign.VarName = captured;
                        c.ClearStack();
                    }
                });
            }
            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  ARG PICKERS
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildNumberPickerOptions(string funcName,
            AstNodes.AstNode parent, int insertIndex)
        {
            var options = new List<OptionNode>();
            foreach (float val in new[] { 0.5f, 1f, 2f, 3f, 5f, 10f })
            {
                var expr = new AstNodes.NumberNode { Value = val };
                string label = val % 1 == 0 ? ((int)val).ToString() : val.ToString();
                options.Add(new OptionNode
                {
                    Label = label, Glyph = "·",
                    Apply = (d, c) =>
                    {
                        InsertNode(d, c, parent, insertIndex,
                            new AstNodes.CallNode { FunctionName = funcName, Args = new() { expr } });
                    }
                });
            }
            return options;
        }

        List<OptionNode> BuildFuncArgOptions(string funcName, int argCount,
            AstNodes.AstNode parent, int insertIndex)
        {
            if (argCount <= 1)
                return BuildNumberPickerOptions(funcName, parent, insertIndex);

            // Multi-arg chaining (#9): pick args sequentially
            return BuildFuncArgChain(funcName, argCount, 0,
                new List<AstNodes.ExprNode>(), parent, insertIndex);
        }

        List<OptionNode> BuildFuncArgChain(string funcName, int argCount, int argIdx,
            List<AstNodes.ExprNode> collectedArgs, AstNodes.AstNode parent, int insertIndex)
        {
            return BuildSimpleValueOptions(_currentDoc, $"arg {argIdx + 1}/{argCount}", (val) =>
            {
                var args = new List<AstNodes.ExprNode>(collectedArgs) { val };
                if (args.Count < argCount)
                {
                    return new OptionNode
                    {
                        Label = $"arg {argIdx + 1} = {ExprPreview(val)}", Glyph = "·",
                        Children = BuildFuncArgChain(funcName, argCount, argIdx + 1,
                            args, parent, insertIndex)
                    };
                }
                return new OptionNode
                {
                    Label = $"{funcName}({string.Join(", ", args.ConvertAll(ExprPreview))})", Glyph = "·",
                    Apply = (d, c) =>
                    {
                        InsertNode(d, c, parent, insertIndex,
                            new AstNodes.CallNode { FunctionName = funcName, Args = args });
                    }
                };
            });
        }

        List<OptionNode> BuildMethodArgOptions(string objName, string methodName, int argCount,
            AstNodes.AstNode parent, int insertIndex)
        {
            if (argCount <= 1)
            {
                var options = new List<OptionNode>();
                foreach (float val in new[] { 0.5f, 1f, 2f, 3f, 5f, 10f })
                {
                    var expr = new AstNodes.NumberNode { Value = val };
                    string label = val % 1 == 0 ? ((int)val).ToString() : val.ToString();
                    options.Add(new OptionNode
                    {
                        Label = label, Glyph = "·",
                        Apply = (d, c) =>
                        {
                            InsertNode(d, c, parent, insertIndex, new AstNodes.MethodCallNode
                            {
                                ObjectName = objName,
                                MethodName = methodName,
                                Args = new() { expr }
                            });
                        }
                    });
                }
                return options;
            }

            // Multi-arg chaining (#9)
            return BuildMethodArgChain(objName, methodName, argCount, 0,
                new List<AstNodes.ExprNode>(), parent, insertIndex);
        }

        List<OptionNode> BuildMethodArgChain(string objName, string methodName, int argCount,
            int argIdx, List<AstNodes.ExprNode> collectedArgs,
            AstNodes.AstNode parent, int insertIndex)
        {
            return BuildSimpleValueOptions(_currentDoc, $"arg {argIdx + 1}/{argCount}", (val) =>
            {
                var args = new List<AstNodes.ExprNode>(collectedArgs) { val };
                if (args.Count < argCount)
                {
                    return new OptionNode
                    {
                        Label = $"arg {argIdx + 1} = {ExprPreview(val)}", Glyph = "·",
                        Children = BuildMethodArgChain(objName, methodName, argCount,
                            argIdx + 1, args, parent, insertIndex)
                    };
                }
                return new OptionNode
                {
                    Label = $"{objName}.{methodName}({string.Join(", ", args.ConvertAll(ExprPreview))})",
                    Glyph = "·",
                    Apply = (d, c) =>
                    {
                        InsertNode(d, c, parent, insertIndex, new AstNodes.MethodCallNode
                        {
                            ObjectName = objName, MethodName = methodName, Args = args
                        });
                    }
                };
            });
        }

        // ═══════════════════════════════════════════════════════════
        //  STRING LITERAL PICKER  (#10)
        // ═══════════════════════════════════════════════════════════

        List<OptionNode> BuildStringLiteralOptions(CodeDocument doc,
            Func<AstNodes.ExprNode, Action<CodeDocument, EditorCursor>> onPick)
        {
            var options = new List<OptionNode>();

            // Game-suggested strings
            var suggestions = _editorExt?.GetStringLiteralSuggestions() ?? new List<string>();
            foreach (var s in suggestions)
            {
                var expr = new AstNodes.StringNode { Value = s };
                options.Add(new OptionNode
                {
                    Label = $"\"{s}\"", Glyph = "·", Hint = "suggested",
                    Apply = onPick(expr)
                });
            }

            // Common literals
            foreach (var s in new[] { "hello", "yes", "no", "ok", "done", "error" })
            {
                if (suggestions.Contains(s)) continue;
                var expr = new AstNodes.StringNode { Value = s };
                options.Add(new OptionNode
                {
                    Label = $"\"{s}\"", Glyph = "·",
                    Apply = onPick(expr)
                });
            }

            return options;
        }

        // ═══════════════════════════════════════════════════════════
        //  INSERT HELPER (body-aware)  (#3)
        // ═══════════════════════════════════════════════════════════

        static void InsertNode(CodeDocument doc, EditorCursor cursor,
            AstNodes.AstNode parent, int index, AstNodes.AstNode node)
        {
            if (parent != null)
                doc.InsertIntoBody(parent, index, node);
            else
                doc.InsertAt(index, node);
            cursor.ClearStack();
        }

        // ═══════════════════════════════════════════════════════════
        //  DOCUMENT QUERIES
        // ═══════════════════════════════════════════════════════════

        static HashSet<string> GetDeclaredVariables(CodeDocument doc)
        {
            var vars = new HashSet<string>();
            if (doc != null) CollectVariables(doc.Statements, vars);
            return vars;
        }

        static void CollectVariables(List<AstNodes.AstNode> nodes, HashSet<string> vars)
        {
            foreach (var stmt in nodes)
            {
                switch (stmt)
                {
                    case AstNodes.AssignNode a: vars.Add(a.VarName); break;
                    case AstNodes.AssignFromMethodNode am: vars.Add(am.VarName); break;
                    case AstNodes.ForNode f:
                        vars.Add(f.VarName);
                        CollectVariables(f.Body, vars);
                        break;
                    case AstNodes.WhileNode w: CollectVariables(w.Body, vars); break;
                    case AstNodes.IfNode ifn:
                        CollectVariables(ifn.ThenBody, vars);
                        CollectVariables(ifn.ElseBody, vars);
                        break;
                }
            }
        }

        static List<DeclaredObject> GetDeclaredObjects(CodeDocument doc)
        {
            var objects = new List<DeclaredObject>();
            if (doc != null) CollectObjects(doc.Statements, objects);
            return objects;
        }

        static void CollectObjects(List<AstNodes.AstNode> nodes, List<DeclaredObject> objects)
        {
            foreach (var stmt in nodes)
            {
                if (stmt is AstNodes.ObjectDeclNode od)
                    objects.Add(new DeclaredObject { Name = od.VarName, TypeName = od.TypeName });
                else if (stmt is AstNodes.WhileNode w) CollectObjects(w.Body, objects);
                else if (stmt is AstNodes.ForNode f) CollectObjects(f.Body, objects);
                else if (stmt is AstNodes.IfNode ifn)
                {
                    CollectObjects(ifn.ThenBody, objects);
                    CollectObjects(ifn.ElseBody, objects);
                }
            }
        }

        static string ExprPreview(AstNodes.ExprNode expr)
        {
            return expr switch
            {
                AstNodes.NumberNode n => n.Value % 1 == 0
                    ? ((int)n.Value).ToString() : n.Value.ToString(),
                AstNodes.VarNode v => v.Name,
                AstNodes.BoolNode b => b.Value ? "True" : "False",
                AstNodes.StringNode s => $"\"{s.Value}\"",
                AstNodes.BinaryOpNode bin =>
                    $"{ExprPreview(bin.Left)} {bin.Op} {ExprPreview(bin.Right)}",
                null => "?",
                _ => "..."
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SUPPORTING TYPES
    // ═══════════════════════════════════════════════════════════════

    public struct DeclaredObject
    {
        public string Name;
        public string TypeName;
    }
}
