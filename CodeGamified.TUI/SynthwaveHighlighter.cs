// ═══════════════════════════════════════════════════════════
//  SynthwaveHighlighter — Python & ASM syntax highlighting
//  Synthwave '84 color palette for source code display.
//  Tokenizes Python source lines and wraps in TMP rich-text.
//  Moved from Pong.UI → engine so all games share one copy.
// ═══════════════════════════════════════════════════════════
using System.Collections.Generic;
using System.Text;

namespace CodeGamified.TUI
{
    public static class SynthwaveHighlighter
    {
        // ── Synthwave '84 palette ───────────────────────────────
        const string Keyword  = "#FEDE5D"; // neon yellow
        const string Function = "#F97E72"; // coral red
        const string Number   = "#FF8B39"; // warm orange
        const string String   = "#E8DA5E"; // soft gold
        const string Comment  = "#848BBD"; // muted lavender
        const string Operator = "#FF7EDB"; // hot pink
        const string Hook     = "#FF7EDB"; // hot pink (event labels)
        const string Punct    = "#BBBBBB"; // light gray

        static readonly HashSet<string> Keywords = new HashSet<string>
        {
            "while", "if", "else", "elif", "for", "def", "return",
            "True", "False", "None", "and", "or", "not", "in", "is",
            "break", "continue", "pass"
        };

        public static string Highlight(string line)
        {
            if (string.IsNullOrEmpty(line)) return line;

            var sb = new StringBuilder(line.Length * 2);
            int i = 0;
            int len = line.Length;

            while (i < len)
            {
                char c = line[i];

                // Whitespace
                if (c == ' ' || c == '\t')
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                // Comment — rest of line
                if (c == '#')
                {
                    Wrap(sb, Comment, line, i, len - i);
                    break;
                }

                // String literal
                if (c == '\'' || c == '"')
                {
                    int start = i;
                    char q = c;
                    i++;
                    while (i < len && line[i] != q)
                    {
                        if (line[i] == '\\' && i + 1 < len) i++;
                        i++;
                    }
                    if (i < len) i++; // closing quote
                    Wrap(sb, String, line, start, i - start);
                    continue;
                }

                // Number
                if (char.IsDigit(c) || (c == '.' && i + 1 < len && char.IsDigit(line[i + 1])))
                {
                    int start = i;
                    while (i < len && (char.IsDigit(line[i]) || line[i] == '.'))
                        i++;
                    Wrap(sb, Number, line, start, i - start);
                    continue;
                }

                // Identifier or keyword
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;
                    string word = line.Substring(start, i - start);

                    if (Keywords.Contains(word))
                    {
                        Wrap(sb, Keyword, word);
                    }
                    else
                    {
                        // Hook label: non-keyword identifier + ":" at end of meaningful content
                        bool isHook = false;
                        if (i < len && line[i] == ':')
                        {
                            int rest = i + 1;
                            while (rest < len && line[rest] == ' ') rest++;
                            if (rest >= len || line[rest] == '#')
                                isHook = true;
                        }

                        if (isHook)
                        {
                            Wrap(sb, Hook, word + ":");
                            i++; // consume the colon
                        }
                        else
                        {
                            // Peek ahead for function call
                            int peek = i;
                            while (peek < len && line[peek] == ' ') peek++;
                            if (peek < len && line[peek] == '(')
                                Wrap(sb, Function, word);
                            else
                                sb.Append(word);
                        }
                    }
                    continue;
                }

                // Operators
                if ("+-*/%=<>!&|^~".IndexOf(c) >= 0)
                {
                    if (i + 1 < len && IsDoubleOp(c, line[i + 1]))
                    {
                        Wrap(sb, Operator, line, i, 2);
                        i += 2;
                    }
                    else
                    {
                        Wrap(sb, Operator, c);
                        i++;
                    }
                    continue;
                }

                // Punctuation
                if (c == '(' || c == ')' || c == '[' || c == ']' ||
                    c == '{' || c == '}' || c == ':' || c == ',' || c == '.')
                {
                    Wrap(sb, Punct, c);
                    i++;
                    continue;
                }

                // Anything else
                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        static bool IsDoubleOp(char a, char b)
        {
            return (a == '=' && b == '=') || (a == '!' && b == '=') ||
                   (a == '<' && b == '=') || (a == '>' && b == '=') ||
                   (a == '*' && b == '*') || (a == '/' && b == '/') ||
                   (a == '+' && b == '=') || (a == '-' && b == '=') ||
                   (a == '*' && b == '=') || (a == '/' && b == '=');
        }

        static void Wrap(StringBuilder sb, string color, string text)
        {
            sb.Append("<color=").Append(color).Append('>').Append(text).Append("</color>");
        }

        static void Wrap(StringBuilder sb, string color, string source, int start, int length)
        {
            sb.Append("<color=").Append(color).Append('>');
            sb.Append(source, start, length);
            sb.Append("</color>");
        }

        static void Wrap(StringBuilder sb, string color, char c)
        {
            sb.Append("<color=").Append(color).Append('>').Append(c).Append("</color>");
        }

        // ── Machine code (assembly) highlighting ────────────────

        const string Mnemonic = "#FF7EDB"; // hot pink — opcodes
        const string Register = "#36F9F6"; // electric cyan — registers
        const string Address  = "#FEDE5D"; // neon yellow — jump targets
        const string IoLabel  = "#FF8B39"; // warm orange — I/O names
        const string AsmNum   = "#F97E72"; // coral — numeric literals
        const string AsmPunct2 = "#BBBBBB"; // light gray — commas, brackets

        static readonly HashSet<string> Mnemonics = new HashSet<string>
        {
            "LDI", "LDF", "LDM", "STM", "MOV",
            "ADD", "SUB", "MUL", "DIV", "MOD", "MIN", "MAX",
            "INC", "DEC", "CMP",
            "JMP", "JEQ", "JNE", "JLT", "JGT", "JLE", "JGE",
            "PUSH", "POP", "CALL", "RET",
            "WAIT", "NOP", "HLT", "BRK",
            "INP", "OUT"
        };

        public static string HighlightAsm(string asm)
        {
            if (string.IsNullOrEmpty(asm)) return asm;

            var sb = new StringBuilder(asm.Length * 2);
            int i = 0;
            int len = asm.Length;

            while (i < len)
            {
                char c = asm[i];

                if (c == ' ' || c == '\t') { sb.Append(c); i++; continue; }

                // Registers: R0, R1, etc.
                if (c == 'R' && i + 1 < len && char.IsDigit(asm[i + 1]))
                {
                    int start = i;
                    i++;
                    while (i < len && char.IsDigit(asm[i])) i++;
                    Wrap(sb, Register, asm, start, i - start);
                    continue;
                }

                // Address: @0001
                if (c == '@')
                {
                    int start = i;
                    i++;
                    while (i < len && char.IsDigit(asm[i])) i++;
                    Wrap(sb, Address, asm, start, i - start);
                    continue;
                }

                // Numeric: #42 or plain digits
                if (c == '#' || (char.IsDigit(c) && (i == 0 || !char.IsLetter(asm[i - 1]))))
                {
                    int start = i;
                    if (c == '#') i++;
                    while (i < len && (char.IsDigit(asm[i]) || asm[i] == '.' || asm[i] == '-')) i++;
                    Wrap(sb, AsmNum, asm, start, i - start);
                    continue;
                }

                // Identifiers: mnemonics or I/O labels
                if (char.IsLetter(c) || c == '_')
                {
                    int start = i;
                    while (i < len && (char.IsLetterOrDigit(asm[i]) || asm[i] == '_' || asm[i] == '.'))
                        i++;
                    string word = asm.Substring(start, i - start);
                    if (Mnemonics.Contains(word))
                        Wrap(sb, Mnemonic, word);
                    else
                        Wrap(sb, IoLabel, word);
                    continue;
                }

                // Punctuation: brackets, commas
                if (c == '[' || c == ']' || c == ',' || c == '('  || c == ')')
                {
                    Wrap(sb, AsmPunct2, c);
                    i++;
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }
    }
}
