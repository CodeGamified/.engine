// ═══════════════════════════════════════════════════════════
//  CodeEditorWindow — Tap-to-code terminal editor
//  Top: source view │ Bottom: option picker + nav
//  Integrates Engine (AST) + TUI (rendering/buttons)
// ═══════════════════════════════════════════════════════════
using System;
using System.Collections.Generic;
using UnityEngine;
using CodeGamified.TUI;
using CodeGamified.Engine;
using CodeGamified.Engine.Compiler;

namespace CodeGamified.Editor
{
    /// <summary>
    /// TerminalWindow subclass for mobile-friendly code editing.
    ///
    /// Layout:
    /// ┌─────────────────────────────────────┐
    /// │ ◆━━ CODE EDITOR ━━━━━━━━━━━━━━━━◆  │  header
    /// │ program.py  [UNDO] [REDO]          │  subtitle
    /// │ ├── separator ──────────────────    │
    /// │  1│ Radio r = new Radio()           │  source
    /// │ >2│ while True:                     │  cursor (display line)
    /// │  3│   x = r.read()                  │
    /// │  ⚠ Line 2: while loops require...  │  compile error (#9)
    /// │                                     │
    /// │ ├── separator ──────────────────    │
    /// │  ◆ Edit          ▸                  │  options (scrollable #7)
    /// │  ◆ Add inside    ▸                  │
    /// │  ◆ Insert below  ▸                  │  ▲▼ scroll indicators
    /// │ ├── separator ──────────────────    │
    /// │  [▲] [▼] [BACK] [UNDO] [RUN]      │  nav
    /// └─────────────────────────────────────┘
    /// </summary>
    public class CodeEditorWindow : TerminalWindow
    {
        // ── Document & cursor ───────────────────────────────────
        CodeDocument _doc;
        EditorCursor _cursor;
        OptionTreeBuilder _treeBuilder;
        ICompilerExtension _compilerExt;

        // ── Display model (#4) ──────────────────────────────────
        List<CodeDocument.DisplayLine> _displayLines = new();

        // ── Layout zones ────────────────────────────────────────
        int _optionRowStart;
        int _optionRowCount = 5;
        const int NAV_ROWS = 2; // separator + nav buttons

        // ── Compile errors (#9) ─────────────────────────────────
        List<string> _compileErrors = new();

        // ── Events ──────────────────────────────────────────────
        public event Action<CompiledProgram> OnCompileAndRun;
        public event Action OnClose;

        // ── Audio / haptic feedback hooks (#8) ──────────────────
        /// <summary>Fired when the player selects any option. Arg: option label.</summary>
        public event Action<string> OnOptionSelected;
        /// <summary>Fired when undo is performed.</summary>
        public event Action OnUndoPerformed;
        /// <summary>Fired when redo is performed.</summary>
        public event Action OnRedoPerformed;
        /// <summary>Fired when compilation produces errors. Arg: error count.</summary>
        public event Action<int> OnCompileError;
        /// <summary>Fired when the document changes (for live preview / auto-save).</summary>
        public event Action OnDocumentChanged;

        // ── Button overlays ─────────────────────────────────────
        bool _navButtonsCreated;

        // ═══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();
            windowTitle = "CODE EDITOR";
            totalRows = 20;
        }

        protected override void Update()
        {
            base.Update();
            HandleKeyboardInput();
        }

        // ═══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ═══════════════════════════════════════════════════════════

        public void Open(CodeDocument doc, ICompilerExtension compilerExt,
                         IEditorExtension editorExt = null)
        {
            // Detach from previous document
            if (_doc != null) _doc.OnDocumentChanged -= HandleDocumentChanged;

            _doc = doc;
            _compilerExt = compilerExt;
            _cursor = new EditorCursor();
            _treeBuilder = new OptionTreeBuilder(compilerExt, editorExt);
            _compileErrors.Clear();

            // Relay document mutations to window event
            _doc.OnDocumentChanged += HandleDocumentChanged;

            RebuildDisplayLines();
            RefreshOptions();
        }

        public void OpenNew(string programName, ICompilerExtension compilerExt,
                            IEditorExtension editorExt = null)
        {
            var doc = new CodeDocument { Name = programName };
            Open(doc, compilerExt, editorExt);
        }

        public void OpenSource(string source, string programName,
                               ICompilerExtension compilerExt,
                               IEditorExtension editorExt = null)
        {
            var doc = new CodeDocument { Name = programName };
            var ctx = new CompilerContext { Extension = compilerExt };
            compilerExt?.RegisterBuiltins(ctx);
            doc.LoadFromSource(source, ctx);
            Open(doc, compilerExt, editorExt);
        }

        public CodeDocument Document => _doc;

        // ═══════════════════════════════════════════════════════════
        //  DISPLAY MODEL (#4)
        // ═══════════════════════════════════════════════════════════

        void RebuildDisplayLines()
        {
            _displayLines = _doc?.BuildDisplayLines() ?? new();
        }

        // ═══════════════════════════════════════════════════════════
        //  OPTION MANAGEMENT
        // ═══════════════════════════════════════════════════════════

        void RefreshOptions()
        {
            if (_doc == null || _treeBuilder == null) return;
            RebuildDisplayLines();
            var rootOptions = _treeBuilder.BuildRoot(_doc, _cursor);
            _cursor.ClearStack();
            _cursor.PushOptions(rootOptions);
        }

        void SelectOption(int index)
        {
            if (_cursor.CurrentOptions == null) return;
            if (index < 0 || index >= _cursor.CurrentOptions.Count) return;

            var option = _cursor.CurrentOptions[index];
            if (option.Disabled) return;

            OnOptionSelected?.Invoke(option.Label);

            if (option.IsLeaf && option.Apply != null)
            {
                option.Apply(_doc, _cursor);
                _compileErrors.Clear();
                RefreshOptions();
            }
            else if (option.IsBranch)
            {
                _cursor.PushOptions(option.Children);
            }
        }

        void GoBack()
        {
            if (!_cursor.PopOptions())
                RefreshOptions();
        }

        // ═══════════════════════════════════════════════════════════
        //  UNDO / REDO  (#1)
        // ═══════════════════════════════════════════════════════════

        void DoUndo()
        {
            if (_doc == null || !_doc.CanUndo) return;
            _doc.Undo();
            _compileErrors.Clear();
            RefreshOptions();
            OnUndoPerformed?.Invoke();
        }

        void DoRedo()
        {
            if (_doc == null || !_doc.CanRedo) return;
            _doc.Redo();
            _compileErrors.Clear();
            RefreshOptions();
            OnRedoPerformed?.Invoke();
        }

        // ═══════════════════════════════════════════════════════════
        //  KEYBOARD INPUT (desktop fallback)
        // ═══════════════════════════════════════════════════════════

        void HandleKeyboardInput()
        {
            if (_doc == null || !rowsReady) return;

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                if (_cursor.CurrentOptions != null && _cursor.SelectedIndex > 0)
                    _cursor.SelectedIndex--;
                else
                {
                    _cursor.MoveUp(_displayLines.Count);
                    RefreshOptions();
                }
            }

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                if (_cursor.CurrentOptions != null &&
                    _cursor.SelectedIndex < _cursor.CurrentOptions.Count - 1)
                    _cursor.SelectedIndex++;
                else
                {
                    _cursor.MoveDown(_displayLines.Count);
                    RefreshOptions();
                }
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                SelectOption(_cursor.SelectedIndex);

            if (Input.GetKeyDown(KeyCode.Backspace))
                GoBack();

            if (Input.GetKeyDown(KeyCode.Escape))
                OnClose?.Invoke();

            // Ctrl+Z / Ctrl+Y for undo/redo
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.Z)) DoUndo();
            if (ctrl && Input.GetKeyDown(KeyCode.Y)) DoRedo();

            if (Input.GetKeyDown(KeyCode.W))
            {
                _cursor.MoveUp(_displayLines.Count);
                RefreshOptions();
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                _cursor.MoveDown(_displayLines.Count);
                RefreshOptions();
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  RENDERING
        // ═══════════════════════════════════════════════════════════

        protected override void Render()
        {
            if (_doc == null) { RenderEmpty(); return; }

            ClearAllRows();
            ComputeLayout();

            int r = 0;
            r = RenderHeader(r);
            r = RenderSource(r);
            r = RenderOptionSeparator(r);
            r = RenderOptions(r);
            RenderNav();
        }

        void ComputeLayout()
        {
            _optionRowCount = Mathf.Clamp(totalRows / 3, 3, 8);
            _optionRowStart = totalRows - NAV_ROWS - _optionRowCount - 1;
        }

        int RenderHeader(int r)
        {
            Color32 accent = TUIGradient.Sample(0f);
            string indicator = TUIWidgets.SpinnerFrame(windowAge, TUIGlyphs.PulseDiamond, 0.2f);
            SetRow(r++, $"{TUIColors.Fg(accent, indicator)} {TUIColors.Bold(windowTitle)}");

            // Subtitle with undo/redo indicators
            string undoHint = _doc.CanUndo
                ? TUIColors.Fg(TUIColors.BrightCyan, "[UNDO]")
                : TUIColors.Dimmed("[UNDO]");
            string redoHint = _doc.CanRedo
                ? TUIColors.Fg(TUIColors.BrightCyan, "[REDO]")
                : TUIColors.Dimmed("[REDO]");
            SetRow(r++, TUIColors.Dimmed($"  {_doc.Name}") +
                        $"  {undoHint} {redoHint}");
            SetRow(r++, Separator());
            return r;
        }

        int RenderSource(int r)
        {
            int sourceRows = _optionRowStart - r;
            int lineCount = _displayLines.Count > 0 ? _displayLines.Count : 1;
            _cursor.ClampScroll(lineCount, sourceRows);

            if (_displayLines.Count == 0)
            {
                SetRow(r++, TUIColors.Dimmed("  (empty program — pick an option below)"));
                return r;
            }

            for (int i = 0; i < sourceRows && r < _optionRowStart; i++)
            {
                int dlIdx = _cursor.ScrollOffset + i;
                if (dlIdx < _displayLines.Count)
                {
                    var dl = _displayLines[dlIdx];
                    int ln = dlIdx + 1;
                    bool active = dlIdx == _cursor.Line;
                    string prefix = active
                        ? TUIColors.Fg(TUIColors.BrightGreen, TUIGlyphs.ArrowR)
                        : " ";
                    string num = TUIColors.Dimmed($"{ln,3}");
                    string lineText = active
                        ? TUIColors.Bold(dl.Text)
                        : dl.Text;
                    SetRow(r, $"{prefix}{num}{TUIGlyphs.BoxV}{lineText}");

                    // Inline compile error for this source line (#9)
                    if (dl.Node != null && _compileErrors.Count > 0)
                    {
                        string errPrefix = $"Line {dl.Node.SourceLine}:";
                        foreach (var err in _compileErrors)
                        {
                            if (err.StartsWith(errPrefix))
                            {
                                r++;
                                if (r < _optionRowStart)
                                    SetRow(r, TUIColors.Fg(TUIColors.Red, $"     {TUIGlyphs.Warn} {err}"));
                                break;
                            }
                        }
                    }
                }
                r++;
            }

            return r;
        }

        int RenderOptionSeparator(int r)
        {
            if (r < totalRows)
                SetRow(r++, Separator());
            return r;
        }

        int RenderOptions(int r)
        {
            var options = _cursor.CurrentOptions;
            if (options == null) return r;

            int available = totalRows - NAV_ROWS - r;

            // Scrollable option rendering (#7)
            _cursor.ClampOptionScroll(options.Count, available);
            int scrollStart = _cursor.OptionScrollOffset;
            bool hasMoreAbove = scrollStart > 0;
            bool hasMoreBelow = scrollStart + available < options.Count;

            // Show scroll-up indicator
            if (hasMoreAbove && available > 2)
            {
                SetRow(r++, TUIColors.Dimmed($"     {TUIGlyphs.ArrowU} more ({scrollStart} above)"));
                available--;
            }

            // Reserve a row for scroll-down indicator
            int renderSlots = hasMoreBelow && available > 1 ? available - 1 : available;

            for (int i = 0; i < renderSlots && r < totalRows - NAV_ROWS; i++)
            {
                int optIdx = scrollStart + i;
                if (optIdx >= options.Count) break;

                var opt = options[optIdx];
                bool selected = optIdx == _cursor.SelectedIndex;

                string glyph = opt.Glyph ?? "◆";
                string arrow = opt.IsBranch ? $" {TUIGlyphs.ArrowR}" : "";
                string hint = !string.IsNullOrEmpty(opt.Hint)
                    ? TUIColors.Dimmed($"  {opt.Hint}") : "";

                string line;
                if (selected)
                {
                    Color32 hi = TUIGradient.Sample(0.3f);
                    line = TUIColors.Fg(hi, $"  {glyph} {TUIColors.Bold(opt.Label)}{arrow}") + hint;
                }
                else if (opt.Disabled)
                {
                    line = TUIColors.Dimmed($"  {glyph} {opt.Label}{arrow}  {opt.Hint}");
                }
                else
                {
                    line = $"  {glyph} {opt.Label}{arrow}{hint}";
                }

                SetRow(r, line);
                CreateOptionButton(r, optIdx);
                r++;
            }

            // Show scroll-down indicator
            if (hasMoreBelow && r < totalRows - NAV_ROWS)
            {
                int remaining = options.Count - (scrollStart + renderSlots);
                SetRow(r++, TUIColors.Dimmed($"     {TUIGlyphs.ArrowD} more ({remaining} below)"));
            }

            return r;
        }

        void RenderNav()
        {
            SetRow(RowSepBot, Separator());

            bool canBack = _cursor.OptionStack.Count > 0;
            string back = canBack
                ? TUIColors.Fg(TUIColors.BrightCyan, "[BACK]")
                : TUIColors.Dimmed("[BACK]");
            string undo = _doc.CanUndo
                ? TUIColors.Fg(TUIColors.BrightYellow, "[UNDO]")
                : TUIColors.Dimmed("[UNDO]");

            SetRow(RowActions,
                $"  {TUIColors.Fg(TUIColors.BrightCyan, $"[{TUIGlyphs.ArrowU}]")} " +
                $"{TUIColors.Fg(TUIColors.BrightCyan, $"[{TUIGlyphs.ArrowD}]")}  " +
                $"{back}  {undo}  " +
                $"{TUIColors.Fg(TUIColors.BrightGreen, "[RUN]")}");

            CreateNavButtons();
        }

        void RenderEmpty()
        {
            ClearAllRows();
            Color32 accent = TUIGradient.Sample(0f);
            SetRow(0, $"{TUIColors.Fg(accent, TUIGlyphs.DiamondFilled)} {TUIColors.Bold(windowTitle)}");
            SetRow(2, TUIColors.Dimmed("  No document open."));
        }

        // ═══════════════════════════════════════════════════════════
        //  BUTTON OVERLAYS (tap targets)
        // ═══════════════════════════════════════════════════════════

        void CreateOptionButton(int rowIndex, int optionIndex)
        {
            if (rowIndex >= rows.Count) return;
            var row = rows[rowIndex];

            int capturedOption = optionIndex;
            row.CreateButtonOverlays(
                new[] { 0 },
                new[] { totalChars },
                new Action<int>[] { (_) => SelectOption(capturedOption) }
            );
        }

        void CreateNavButtons()
        {
            if (_navButtonsCreated || RowActions >= rows.Count) return;
            _navButtonsCreated = true;

            int btnWidth = 6;
            var navRow = rows[RowActions];
            navRow.CreateButtonOverlays(
                new[] { 2, 2 + btnWidth + 1, 2 + (btnWidth + 1) * 2,
                        2 + (btnWidth + 1) * 3, 2 + (btnWidth + 1) * 4 },
                new[] { btnWidth, btnWidth, btnWidth, btnWidth, btnWidth },
                new Action<int>[]
                {
                    (_) => { _cursor.MoveUp(_displayLines.Count); RefreshOptions(); },
                    (_) => { _cursor.MoveDown(_displayLines.Count); RefreshOptions(); },
                    (_) => GoBack(),
                    (_) => DoUndo(),
                    (_) => CompileAndRun()
                }
            );
        }

        // ═══════════════════════════════════════════════════════════
        //  COMPILE & RUN  (#9 error display)
        // ═══════════════════════════════════════════════════════════

        void CompileAndRun()
        {
            if (_doc == null) return;
            var program = _doc.Compile(_compilerExt);
            _compileErrors.Clear();

            if (program.IsValid)
            {
                OnCompileAndRun?.Invoke(program);
            }
            else
            {
                // Store errors for inline rendering (#9)
                _compileErrors.AddRange(program.Errors);
                OnCompileError?.Invoke(_compileErrors.Count);
            }
        }

        void HandleDocumentChanged()
        {
            OnDocumentChanged?.Invoke();
        }
    }
}
