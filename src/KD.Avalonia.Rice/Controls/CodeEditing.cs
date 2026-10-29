using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// An edit of a <see cref="RiceCodeEditor"/>: <see cref="Length"/> characters from <see cref="Start"/> are replaced by
/// <see cref="Replacement"/>, then the selection is set. Applied as one undo step.
/// </summary>
internal readonly record struct CodeEdit(int Start, int Length, string Replacement, int SelectionStart, int SelectionEnd);

/// <summary>The editing commands of <see cref="RiceCodeEditor"/>, on plain text (no UI), so they can be tested.</summary>
internal static class CodeEditing
{
    /// <summary>The line comment of a language (as <see cref="CodeHighlighter"/> names it), null when it has none.</summary>
    public static string? LineCommentPrefix(string? language) => CodeHighlighter.LanguageName(language) switch
    {
        "csharp" or "javascript" => "//",
        "python" or "bash" or "yaml" => "#",
        "sql" => "--",
        "plantuml" => "'",
        "mermaid" => "%%",
        _ => null,
    };

    /// <summary>
    /// The lines touched by the selection: from the start of the first line to the end of the last one (before its
    /// line break). A selection that ends at the very start of a line does not take that line.
    /// </summary>
    public static (int Start, int End) LineSpan(string text, int selectionStart, int selectionEnd)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        if (to > from && to > 0 && text[to - 1] == '\n')
            to--;
        var start = from == 0 ? 0 : text.LastIndexOf('\n', from - 1) + 1;
        var end = text.IndexOf('\n', to);
        if (end < 0)
            end = text.Length;
        else if (end > start && text[end - 1] == '\r')
            end--;
        return (start, Math.Max(start, end));
    }

    /// <summary>Whether the selection covers more than one line (Tab then indents the lines instead of replacing them).</summary>
    public static bool IsMultiLine(string text, int selectionStart, int selectionEnd)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        return text.IndexOf('\n', from, to - from) >= 0;
    }

    /// <summary>Tab without a multi line selection: spaces up to the next tab stop (or a tab character) instead of the selection.</summary>
    public static CodeEdit InsertTab(string text, int selectionStart, int selectionEnd, int tabSize, bool useSpaces)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        var insert = "\t";
        if (useSpaces)
        {
            var column = Column(text, from, tabSize);
            insert = new string(' ', tabSize - column % tabSize);
        }
        var caret = from + insert.Length;
        return new CodeEdit(from, to - from, insert, caret, caret);
    }

    /// <summary>Tab with a multi line selection: one indent unit before every non empty line.</summary>
    public static CodeEdit Indent(string text, int selectionStart, int selectionEnd, string unit)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        var (start, end) = LineSpan(text, from, to);
        var lines = text[start..end].Split('\n');
        var added = new int[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r').Length == 0)
                continue;
            lines[i] = unit + lines[i];
            added[i] = unit.Length;
        }
        var replacement = string.Join('\n', lines);
        var newFrom = from == start ? from : from + added[0];
        return new CodeEdit(start, end - start, replacement, newFrom, to + added.Sum());
    }

    /// <summary>Shift+Tab: one indent unit (a tab, or up to <paramref name="tabSize"/> spaces) less on every line; null when nothing changes.</summary>
    public static CodeEdit? Outdent(string text, int selectionStart, int selectionEnd, int tabSize)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        var (start, end) = LineSpan(text, from, to);
        var lines = text[start..end].Split('\n');
        var removed = new int[lines.Length];
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var count = line.StartsWith('\t') ? 1 : line.TakeWhile(c => c == ' ').Take(tabSize).Count();
            lines[i] = line[count..];
            removed[i] = count;
        }
        var total = removed.Sum();
        if (total == 0)
            return null;
        var newFrom = Math.Max(start, from - removed[0]);
        return new CodeEdit(start, end - start, string.Join('\n', lines), newFrom, Math.Max(newFrom, to - total));
    }

    /// <summary>
    /// Enter: a line break with the indentation of the current line, one unit more after an opening bracket; between a
    /// pair of brackets the closing one goes to a line of its own.
    /// </summary>
    public static CodeEdit NewLine(string text, int selectionStart, int selectionEnd, string newLine, string unit)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        var lineStart = from == 0 ? 0 : text.LastIndexOf('\n', from - 1) + 1;
        var indent = LeadingWhitespace(text, lineStart, from);
        var before = LastNonSpace(text, lineStart, from);
        var after = to < text.Length ? text[to] : '\0';
        var opens = before is '{' or '[' or '(';
        string insert;
        int caret;
        if (opens && after == Closer(before))
        {
            insert = newLine + indent + unit + newLine + indent;
            caret = from + newLine.Length + indent.Length + unit.Length;
        }
        else
        {
            insert = newLine + indent + (opens ? unit : "");
            caret = from + insert.Length;
        }
        return new CodeEdit(from, to - from, insert, caret, caret);
    }

    /// <summary>
    /// Backspace in the indentation (only spaces before the caret on its line): back to the previous tab stop;
    /// null when a plain backspace should run.
    /// </summary>
    public static CodeEdit? Backspace(string text, int caret, int tabSize)
    {
        if (caret <= 0 || caret > text.Length)
            return null;
        var lineStart = text.LastIndexOf('\n', caret - 1) + 1;
        var column = caret - lineStart;
        if (column == 0 || text.AsSpan(lineStart, column).ContainsAnyExcept(' '))
            return null;
        var count = (column - 1) % tabSize + 1;
        return count <= 1 ? null : new CodeEdit(caret - count, count, "", caret - count, caret - count);
    }

    /// <summary>
    /// Ctrl+/: comments the lines out with <paramref name="prefix"/> (at the smallest indentation), or removes the comment
    /// when every non empty line has it. Null when there is nothing to change.
    /// </summary>
    public static CodeEdit? ToggleLineComment(string text, int selectionStart, int selectionEnd, string prefix)
    {
        var (from, to) = Ordered(text, selectionStart, selectionEnd);
        var (start, end) = LineSpan(text, from, to);
        var lines = text[start..end].Split('\n');
        var code = lines.Select((l, i) => (Line: l.TrimEnd('\r'), Index: i)).Where(l => l.Line.Trim().Length > 0).ToList();
        if (code.Count == 0)
            return null;
        var remove = code.All(l => l.Line.TrimStart().StartsWith(prefix, StringComparison.Ordinal));
        var indent = code.Min(l => l.Line.Length - l.Line.TrimStart().Length);
        var deltas = new int[lines.Length];
        foreach (var (line, i) in code)
        {
            if (remove)
            {
                var at = line.Length - line.TrimStart().Length;
                var count = prefix.Length + (at + prefix.Length < line.Length && line[at + prefix.Length] == ' ' ? 1 : 0);
                lines[i] = lines[i].Remove(at, count);
                deltas[i] = -count;
            }
            else
            {
                lines[i] = lines[i].Insert(indent, prefix + " ");
                deltas[i] = prefix.Length + 1;
            }
        }
        var newFrom = Math.Max(start, from + (from - start > indent ? deltas[0] : 0));
        return new CodeEdit(start, end - start, string.Join('\n', lines), newFrom, Math.Max(newFrom, to + deltas.Sum()));
    }

    private static (int From, int To) Ordered(string text, int a, int b)
    {
        a = Math.Clamp(a, 0, text.Length);
        b = Math.Clamp(b, 0, text.Length);
        return a <= b ? (a, b) : (b, a);
    }

    // The column of a position, with tabs counted to their tab stop.
    private static int Column(string text, int position, int tabSize)
    {
        var lineStart = position == 0 ? 0 : text.LastIndexOf('\n', position - 1) + 1;
        var column = 0;
        for (var i = lineStart; i < position; i++)
            column = text[i] == '\t' ? (column / tabSize + 1) * tabSize : column + 1;
        return column;
    }

    private static string LeadingWhitespace(string text, int lineStart, int limit)
    {
        var end = lineStart;
        while (end < limit && text[end] is ' ' or '\t')
            end++;
        return text[lineStart..end];
    }

    private static char LastNonSpace(string text, int lineStart, int position)
    {
        for (var i = position - 1; i >= lineStart; i--)
        {
            if (!char.IsWhiteSpace(text[i]))
                return text[i];
        }
        return '\0';
    }

    private static char Closer(char opener) => opener switch
    {
        '{' => '}',
        '[' => ']',
        '(' => ')',
        _ => '\0',
    };
}
