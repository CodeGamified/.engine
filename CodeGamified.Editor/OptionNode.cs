// CodeGamified.Editor — Tap-to-code editor for mobile
// MIT License
using System;
using System.Collections.Generic;

namespace CodeGamified.Editor
{
    /// <summary>
    /// A single node in the context-sensitive option tree.
    /// Leaf nodes have an Apply action. Branch nodes have Children.
    /// </summary>
    public class OptionNode
    {
        public string Label;
        public string Glyph;
        public string Hint;
        public bool Disabled;

        /// <summary>Children for drill-down. Null or empty = leaf node.</summary>
        public List<OptionNode> Children;

        /// <summary>Leaf action — mutates the document at the cursor position.</summary>
        public Action<CodeDocument, EditorCursor> Apply;

        public bool IsLeaf => Children == null || Children.Count == 0;
        public bool IsBranch => !IsLeaf;
    }
}
