// CodeGamified.Editor.Tests — Unit tests for the Editor module
// MIT License
using NUnit.Framework;
using System.Collections.Generic;
using CodeGamified.Engine.Compiler;
using CodeGamified.Editor;

namespace CodeGamified.Editor.Tests
{
    [TestFixture]
    public class EditorTests
    {
        // ═══════════════════════════════════════════════════════════
        //  ROUNDTRIP: source → AST → source
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void Roundtrip_Assignment()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            {
                VarName = "x",
                Value = new AstNodes.NumberNode { Value = 42 }
            });
            string src = doc.ToSource().Trim();
            Assert.AreEqual("x = 42", src);
        }

        [Test]
        public void Roundtrip_WhileTrue()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode>
                {
                    new AstNodes.CallNode { FunctionName = "wait", Args = new List<AstNodes.ExprNode>
                    { new AstNodes.NumberNode { Value = 1 } } }
                }
            });
            string src = doc.ToSource();
            Assert.That(src, Does.Contain("while True:"));
            Assert.That(src, Does.Contain("    wait(1)"));
        }

        [Test]
        public void Roundtrip_ForLoop()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.ForNode
            {
                VarName = "i",
                RangeArgs = new List<AstNodes.ExprNode> { new AstNodes.NumberNode { Value = 5 } },
                Body = new List<AstNodes.AstNode>()
            });
            string src = doc.ToSource();
            Assert.That(src, Does.Contain("for i in range(5):"));
            Assert.That(src, Does.Contain("    pass"));
        }

        [Test]
        public void Roundtrip_IfElse()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.IfNode
            {
                Condition = new AstNodes.BinaryOpNode
                {
                    Left = new AstNodes.VarNode { Name = "x" },
                    Right = new AstNodes.NumberNode { Value = 0 },
                    Op = ">"
                },
                ThenBody = new List<AstNodes.AstNode>
                {
                    new AstNodes.AssignNode { VarName = "y", Value = new AstNodes.NumberNode { Value = 1 } }
                },
                ElseBody = new List<AstNodes.AstNode>
                {
                    new AstNodes.AssignNode { VarName = "y", Value = new AstNodes.NumberNode { Value = 0 } }
                }
            });
            string src = doc.ToSource();
            Assert.That(src, Does.Contain("if x > 0:"));
            Assert.That(src, Does.Contain("else:"));
        }

        [Test]
        public void Roundtrip_StringLiteral()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            {
                VarName = "msg",
                Value = new AstNodes.StringNode { Value = "hello" }
            });
            string src = doc.ToSource().Trim();
            Assert.AreEqual("msg = \"hello\"", src);
        }

        // ═══════════════════════════════════════════════════════════
        //  UNDO / REDO SYMMETRY
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void UndoRedo_InsertThenUndo_RestoresEmpty()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } });
            Assert.AreEqual(1, doc.LineCount);

            doc.Undo();
            Assert.AreEqual(0, doc.LineCount);

            doc.Redo();
            Assert.AreEqual(1, doc.LineCount);
        }

        [Test]
        public void UndoRedo_MultipleOps()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            { VarName = "a", Value = new AstNodes.NumberNode { Value = 1 } });
            doc.Append(new AstNodes.AssignNode
            { VarName = "b", Value = new AstNodes.NumberNode { Value = 2 } });
            doc.Append(new AstNodes.AssignNode
            { VarName = "c", Value = new AstNodes.NumberNode { Value = 3 } });

            Assert.AreEqual(3, doc.LineCount);

            doc.Undo(); // remove c
            Assert.AreEqual(2, doc.LineCount);

            doc.Undo(); // remove b
            Assert.AreEqual(1, doc.LineCount);

            doc.Redo(); // restore b
            Assert.AreEqual(2, doc.LineCount);
        }

        [Test]
        public void UndoRedo_Swap()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            { VarName = "a", Value = new AstNodes.NumberNode { Value = 1 } });
            doc.Append(new AstNodes.AssignNode
            { VarName = "b", Value = new AstNodes.NumberNode { Value = 2 } });

            doc.MoveDown(0); // swap a and b
            Assert.AreEqual("b", ((AstNodes.AssignNode)doc.Statements[0]).VarName);

            doc.Undo();
            Assert.AreEqual("a", ((AstNodes.AssignNode)doc.Statements[0]).VarName);
        }

        // ═══════════════════════════════════════════════════════════
        //  BODY-AWARE OPERATIONS
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void InsertIntoBody_AddsToWhileBody()
        {
            var doc = new CodeDocument();
            var whileNode = new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode>()
            };
            doc.Append(whileNode);

            var assign = new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } };
            doc.InsertIntoBody(whileNode, 0, assign);

            Assert.AreEqual(1, whileNode.Body.Count);
            Assert.AreSame(assign, whileNode.Body[0]);
        }

        [Test]
        public void RemoveFromBody_RemovesFromWhileBody()
        {
            var assign = new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } };
            var whileNode = new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode> { assign }
            };
            var doc = new CodeDocument();
            doc.Append(whileNode);

            doc.RemoveFromBody(whileNode, 0);
            Assert.AreEqual(0, whileNode.Body.Count);
        }

        [Test]
        public void MoveUpInBody_Swaps()
        {
            var a1 = new AstNodes.AssignNode
            { VarName = "a", Value = new AstNodes.NumberNode { Value = 1 } };
            var a2 = new AstNodes.AssignNode
            { VarName = "b", Value = new AstNodes.NumberNode { Value = 2 } };
            var whileNode = new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode> { a1, a2 }
            };
            var doc = new CodeDocument();
            doc.Append(whileNode);

            bool moved = doc.MoveUpInBody(whileNode, 1);
            Assert.IsTrue(moved);
            Assert.AreSame(a2, whileNode.Body[0]);
            Assert.AreSame(a1, whileNode.Body[1]);
        }

        // ═══════════════════════════════════════════════════════════
        //  DISPLAY LINES
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void DisplayLines_WhileWithBody()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode>
                {
                    new AstNodes.AssignNode
                    { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } }
                }
            });

            var lines = doc.BuildDisplayLines();
            Assert.AreEqual(2, lines.Count);
            Assert.That(lines[0].Text, Does.Contain("while True:"));
            Assert.IsTrue(lines[0].IsCompoundHeader);
            Assert.That(lines[1].Text, Does.Contain("x = 1"));
        }

        [Test]
        public void DisplayLines_EmptyWhile_ShowsPass()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode>()
            });

            var lines = doc.BuildDisplayLines();
            Assert.AreEqual(2, lines.Count);
            Assert.That(lines[1].Text, Does.Contain("pass"));
            Assert.IsNull(lines[1].Node); // pass is a placeholder
        }

        // ═══════════════════════════════════════════════════════════
        //  DEEP CLONE
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void DeepClone_Assignment_IsIndependent()
        {
            var original = new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 42 } };

            var clone = CodeDocument.DeepClone(original) as AstNodes.AssignNode;
            Assert.IsNotNull(clone);
            Assert.AreEqual("x", clone.VarName);
            Assert.AreEqual(42, ((AstNodes.NumberNode)clone.Value).Value);

            // Mutating clone doesn't affect original
            clone.VarName = "y";
            Assert.AreEqual("x", original.VarName);
        }

        [Test]
        public void DeepClone_WhileWithBody_ClonesDeep()
        {
            var whileNode = new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode>
                {
                    new AstNodes.AssignNode
                    { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } }
                }
            };

            var clone = CodeDocument.DeepClone(whileNode) as AstNodes.WhileNode;
            Assert.IsNotNull(clone);
            Assert.AreEqual(1, clone.Body.Count);
            Assert.AreNotSame(whileNode.Body[0], clone.Body[0]);
        }

        [Test]
        public void DeepClone_StringNode()
        {
            var original = new AstNodes.AssignNode
            { VarName = "msg", Value = new AstNodes.StringNode { Value = "hello" } };

            var clone = CodeDocument.DeepClone(original) as AstNodes.AssignNode;
            Assert.IsNotNull(clone);
            Assert.AreEqual("hello", ((AstNodes.StringNode)clone.Value).Value);
        }

        // ═══════════════════════════════════════════════════════════
        //  DUPLICATE
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void Duplicate_InsertsCloneBelow()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } });

            var lines = doc.BuildDisplayLines();
            doc.Duplicate(lines[0]);

            Assert.AreEqual(2, doc.LineCount);
            Assert.AreNotSame(doc.Statements[0], doc.Statements[1]);
            Assert.AreEqual("x", ((AstNodes.AssignNode)doc.Statements[1]).VarName);
        }

        // ═══════════════════════════════════════════════════════════
        //  SERIALIZATION
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void ToJson_FromJson_Roundtrip()
        {
            var doc = new CodeDocument { Name = "test_prog" };
            doc.Append(new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 5 } });
            doc.Append(new AstNodes.CallNode
            { FunctionName = "wait", Args = new List<AstNodes.ExprNode>
            { new AstNodes.NumberNode { Value = 1 } } });

            string json = doc.ToJson();
            Assert.That(json, Does.Contain("test_prog"));

            var restored = CodeDocument.FromJson(json);
            Assert.AreEqual("test_prog", restored.Name);
            Assert.AreEqual(2, restored.LineCount);
            Assert.That(restored.ToSource(), Does.Contain("x = 5"));
            Assert.That(restored.ToSource(), Does.Contain("wait(1)"));
        }

        [Test]
        public void ToJson_FromJson_WithNewlines()
        {
            var doc = new CodeDocument { Name = "multiline" };
            doc.Append(new AstNodes.WhileNode
            {
                IsInfinite = true,
                Condition = new AstNodes.BoolNode { Value = true },
                Body = new List<AstNodes.AstNode>
                {
                    new AstNodes.AssignNode
                    { VarName = "x", Value = new AstNodes.NumberNode { Value = 0 } }
                }
            });

            string json = doc.ToJson();
            var restored = CodeDocument.FromJson(json);
            Assert.AreEqual(1, restored.LineCount);
            Assert.IsInstanceOf<AstNodes.WhileNode>(restored.Statements[0]);
        }

        // ═══════════════════════════════════════════════════════════
        //  TIER GATING (via mock extension)
        // ═══════════════════════════════════════════════════════════

        class MockEditorExtension : IEditorExtension
        {
            public bool WhileAllowed = true;
            public bool ForAllowed = true;

            public List<EditorTypeInfo> GetAvailableTypes() => new();
            public List<EditorFuncInfo> GetAvailableFunctions() => new();
            public List<EditorMethodInfo> GetMethodsForType(string typeName) => new();
            public bool IsWhileLoopAllowed() => WhileAllowed;
            public bool IsForLoopAllowed() => ForAllowed;
            public string GetWhileLoopGateReason() => "locked";
            public string GetForLoopGateReason() => "locked";
        }

        [Test]
        public void TierGating_WhileDisabled_ShowsDisabledOption()
        {
            var ext = new MockEditorExtension { WhileAllowed = false };
            var builder = new OptionTreeBuilder(null, ext);
            var doc = new CodeDocument();
            var cursor = new EditorCursor();

            var options = builder.BuildRoot(doc, cursor);
            var whileOpt = options.Find(o => o.Label == "while loop");
            Assert.IsNotNull(whileOpt);
            Assert.IsTrue(whileOpt.Disabled);
        }

        [Test]
        public void TierGating_ForDisabled_ShowsDisabledOption()
        {
            var ext = new MockEditorExtension { ForAllowed = false };
            var builder = new OptionTreeBuilder(null, ext);
            var doc = new CodeDocument();
            var cursor = new EditorCursor();

            var options = builder.BuildRoot(doc, cursor);
            var forOpt = options.Find(o => o.Label == "for loop");
            Assert.IsNotNull(forOpt);
            Assert.IsTrue(forOpt.Disabled);
        }

        // ═══════════════════════════════════════════════════════════
        //  DOCUMENT CHANGED EVENT
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void OnDocumentChanged_FiresOnMutation()
        {
            var doc = new CodeDocument();
            int fireCount = 0;
            doc.OnDocumentChanged += () => fireCount++;

            doc.Append(new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 1 } });
            Assert.AreEqual(1, fireCount);

            doc.Undo();
            Assert.AreEqual(2, fireCount);

            doc.Redo();
            Assert.AreEqual(3, fireCount);
        }

        // ═══════════════════════════════════════════════════════════
        //  CLIPBOARD
        // ═══════════════════════════════════════════════════════════

        [Test]
        public void Clipboard_CopyAndPaste()
        {
            var doc = new CodeDocument();
            doc.Append(new AstNodes.AssignNode
            { VarName = "x", Value = new AstNodes.NumberNode { Value = 5 } });

            var cursor = new EditorCursor();
            var lines = doc.BuildDisplayLines();

            // Copy
            cursor.ClipboardNode = CodeDocument.DeepClone(lines[0].Node);
            Assert.IsNotNull(cursor.ClipboardNode);

            // Paste
            var clone = CodeDocument.DeepClone(cursor.ClipboardNode);
            doc.InsertAt(1, clone);
            Assert.AreEqual(2, doc.LineCount);
            Assert.AreNotSame(doc.Statements[0], doc.Statements[1]);
        }
    }
}
