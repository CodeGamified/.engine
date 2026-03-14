// CodeGamified.Editor — Tap-to-code editor for mobile
// MIT License
using System.Collections.Generic;
using CodeGamified.Engine.Compiler;

namespace CodeGamified.Editor
{
    /// <summary>
    /// Tracks the player's position in the code document and the option tree navigation stack.
    /// </summary>
    public class EditorCursor
    {
        /// <summary>Which display line the cursor is on (index into BuildDisplayLines).</summary>
        public int Line;

        /// <summary>
        /// Which sub-expression slot is being edited within the current statement.
        /// -1 = statement level (not editing a sub-slot).
        /// </summary>
        public int Slot = -1;

        /// <summary>Option tree navigation stack. Push when drilling into a branch, pop on Back.</summary>
        public readonly Stack<List<OptionNode>> OptionStack = new();

        /// <summary>The currently displayed options (top of stack, or root).</summary>
        public List<OptionNode> CurrentOptions;

        /// <summary>Selected option index within CurrentOptions (for highlight).</summary>
        public int SelectedIndex;

        /// <summary>Scroll offset for the source view (top visible line).</summary>
        public int ScrollOffset;

        /// <summary>Scroll offset for the option list (#7).</summary>
        public int OptionScrollOffset;

        /// <summary>Clipboard — stores a copied/cut AST node for paste.</summary>
        public AstNodes.AstNode ClipboardNode;

        public void PushOptions(List<OptionNode> options)
        {
            if (CurrentOptions != null)
                OptionStack.Push(CurrentOptions);
            CurrentOptions = options;
            SelectedIndex = 0;
            OptionScrollOffset = 0;
        }

        public bool PopOptions()
        {
            if (OptionStack.Count == 0) return false;
            CurrentOptions = OptionStack.Pop();
            SelectedIndex = 0;
            OptionScrollOffset = 0;
            return true;
        }

        public void ClearStack()
        {
            OptionStack.Clear();
            CurrentOptions = null;
            SelectedIndex = 0;
            OptionScrollOffset = 0;
            Slot = -1;
        }

        public void MoveUp(int displayLineCount)
        {
            if (Line > 0) Line--;
            ClampScroll(displayLineCount);
        }

        public void MoveDown(int displayLineCount)
        {
            if (Line < displayLineCount - 1) Line++;
            ClampScroll(displayLineCount);
        }

        /// <summary>Keep cursor line visible within a viewport of given height.</summary>
        public void ClampScroll(int lineCount, int viewportRows = 10)
        {
            if (Line < ScrollOffset) ScrollOffset = Line;
            if (Line >= ScrollOffset + viewportRows)
                ScrollOffset = Line - viewportRows + 1;
            if (ScrollOffset < 0) ScrollOffset = 0;
        }

        /// <summary>Keep selected option visible within option viewport (#7).</summary>
        public void ClampOptionScroll(int optionCount, int visibleRows)
        {
            if (SelectedIndex < OptionScrollOffset)
                OptionScrollOffset = SelectedIndex;
            if (SelectedIndex >= OptionScrollOffset + visibleRows)
                OptionScrollOffset = SelectedIndex - visibleRows + 1;
            if (OptionScrollOffset < 0) OptionScrollOffset = 0;
        }
    }
}
