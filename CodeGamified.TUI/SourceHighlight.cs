// ═══════════════════════════════════════════════════════════
//  SourceHighlight — Maps machine instructions to source tokens
//  Used by debugger source panels to highlight the active
//  operator/call within a source line instead of the whole line.
//  Supports multiline continuation (unclosed parens).
//  Moved from Pong.UI → engine so all games share one copy.
// ═══════════════════════════════════════════════════════════
using CodeGamified.Engine;

namespace CodeGamified.TUI
{
    public static class SourceHighlight
    {
        /// <summary>
        /// Render a source line that is part of the active continuation group.
        /// If the current instruction's token is on THIS line, highlight it.
        /// Otherwise, show the line dimmed with a green line number (active context).
        /// </summary>
        public static string HighlightActiveLine(string sourceLine, string linePrefix,
                                                  Instruction inst)
        {
            string token = GetSourceToken(inst);
            if (token != null)
            {
                int idx = sourceLine.IndexOf(token);
                if (idx >= 0)
                {
                    string before = sourceLine.Substring(0, idx);
                    string after  = sourceLine.Substring(idx + token.Length);
                    string greenPrefix = TUIColors.Fg(TUIColors.BrightGreen, linePrefix);
                    string highlighted = TUIColors.Fg(TUIColors.BrightGreen, token);
                    return $"{greenPrefix}{TUIColors.Dimmed(before)}{highlighted}{TUIColors.Dimmed(after)}";
                }
            }

            string gp = TUIColors.Fg(TUIColors.BrightGreen, linePrefix);
            return $"{gp}{SynthwaveHighlighter.Highlight(sourceLine)}";
        }

        /// <summary>
        /// Find the last raw source line index (0-based) that belongs to
        /// the continuation group starting at startLine.
        /// Uses paren/bracket depth — same logic as the compiler's JoinContinuationLines.
        /// </summary>
        public static int GetContinuationEnd(string[] src, int startLine)
        {
            if (startLine < 0 || startLine >= src.Length) return startLine;
            int depth = 0;
            for (int i = startLine; i < src.Length; i++)
            {
                depth += ParenDelta(src[i]);
                if (depth <= 0 && i > startLine) return i;
                if (depth <= 0 && i == startLine) return i;
            }
            return src.Length - 1;
        }

        /// <summary>
        /// Derive the source-code token that a machine instruction represents.
        /// Returns null for internal/scaffolding instructions (spill, restore,
        /// comparison setup, jumps) — caller should fall back to full highlight.
        /// </summary>
        public static string GetSourceToken(Instruction inst)
        {
            string comment = inst.GetComment();
            if (string.IsNullOrEmpty(comment)) return null;

            // ── Arithmetic operators ────────────────────────────
            if (comment == "add")      return "+";
            if (comment == "subtract") return "-";
            if (comment == "multiply") return "*";
            if (comment == "divide")   return "/";
            if (comment == "modulo")   return "%";
            if (comment == "min")      return "min(";
            if (comment == "max")      return "max(";

            // ── Event waits ─────────────────────────────────────
            if (comment == "wait_for_opponent_hit") return "wait_for_opponent_hit()";
            if (comment == "wait_for_wall_hit")     return "wait_for_wall_hit()";

            // ── Comparison operators ────────────────────────────
            if (comment == "compare <")  return "<";
            if (comment == "compare >")  return ">";
            if (comment == "compare ==") return "==";
            if (comment == "compare !=") return "!=";
            if (comment == "compare <=") return "<=";
            if (comment == "compare >=") return ">=";

            // ── Function calls (comment format: "func_name → R0", "func_name(R0)", or "call func_name") ──
            if (comment.StartsWith("call "))
                return comment.Substring(5) + "()";
            int arrow = comment.IndexOf(" →");
            if (arrow > 0)
            {
                string funcName = comment.Substring(0, arrow);
                return funcName + "()";
            }
            int paren = comment.IndexOf('(');
            if (paren > 0 && comment.EndsWith(")"))
            {
                string funcName = comment.Substring(0, paren);
                return funcName + "(";
            }

            // ── Assignment (comment format: "store to varName") ──
            if (comment.StartsWith("store to "))
                return comment.Substring(9);

            // ── Variable load (comment format: "load varName") ──
            if (comment.StartsWith("load "))
            {
                string name = comment.Substring(5);
                if (name.Length > 0 && char.IsLetter(name[0]))
                    return name;
                return name;
            }

            return null;
        }

        private static int ParenDelta(string line)
        {
            int depth = 0;
            bool inString = false;
            char strChar = '\0';
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inString) { if (c == strChar) inString = false; continue; }
                if (c == '\'' || c == '"') { inString = true; strChar = c; continue; }
                if (c == '#') break;
                if (c == '(' || c == '[') depth++;
                else if (c == ')' || c == ']') depth--;
            }
            return depth;
        }
    }
}
