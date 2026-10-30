using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Diffs;

public enum DiffViewMode
{
    /// <summary>The old text on the left, the new one on the right, changed lines side by side.</summary>
    SideBySide,

    /// <summary>One column: removed lines, then the added lines that replace them.</summary>
    Inline,
}

public enum DiffRowKind
{
    Unchanged,

    /// <summary>A line only in the old text.</summary>
    Removed,

    /// <summary>A line only in the new text.</summary>
    Added,

    /// <summary>Side by side: an old line and the new line that replaced it.</summary>
    Changed,

    /// <summary>Unchanged lines that are hidden; <see cref="DiffRow.HiddenCount"/> of them.</summary>
    Fold,
}

/// <summary>A part of a line, in characters.</summary>
public readonly record struct TextRange(int Start, int Length);

/// <summary>One row of a <see cref="RiceDiffView"/>.</summary>
public sealed class DiffRow
{
    public DiffRowKind Kind { get; init; }

    /// <summary>1 based line number in the old text; null when the row has no old line.</summary>
    public int? OldNumber { get; init; }

    public int? NewNumber { get; init; }

    public string? OldText { get; init; }

    public string? NewText { get; init; }

    /// <summary>The changed words of the old line (against the line that replaced it).</summary>
    public IReadOnlyList<TextRange> OldChanges { get; init; } = [];

    public IReadOnlyList<TextRange> NewChanges { get; init; } = [];

    /// <summary>The number of the change block the row belongs to (0, 1, …), -1 for unchanged rows.</summary>
    public int ChangeIndex { get; init; } = -1;

    /// <summary>The block a fold row hides lines of (expand it with <see cref="RiceDiffView.ExpandFold"/>).</summary>
    public int Block { get; init; } = -1;

    public int HiddenCount { get; init; }
}

/// <summary>A run of equal lines, or a change: old lines replaced by new lines (either may be none).</summary>
public sealed record DiffBlock(bool IsChange, int OldStart, int OldCount, int NewStart, int NewCount)
{
    /// <summary>The changed words of the paired lines (old line i with new line i).</summary>
    internal IReadOnlyList<(IReadOnlyList<TextRange> Old, IReadOnlyList<TextRange> New)> Pairs { get; init; } = [];
}

/// <summary>The line difference of two texts, with the changed words of the lines that replaced each other.</summary>
public sealed partial class DiffDocument
{
    // Below this share of equal characters two lines are not compared word by word: they are just different.
    private const double MinSimilarity = 0.35;

    private DiffDocument(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines, IReadOnlyList<DiffBlock> blocks)
    {
        OldLines = oldLines;
        NewLines = newLines;
        Blocks = blocks;
        AddedLines = blocks.Where(b => b.IsChange).Sum(b => b.NewCount);
        RemovedLines = blocks.Where(b => b.IsChange).Sum(b => b.OldCount);
        ChangeCount = blocks.Count(b => b.IsChange);
    }

    public IReadOnlyList<string> OldLines { get; }

    public IReadOnlyList<string> NewLines { get; }

    public IReadOnlyList<DiffBlock> Blocks { get; }

    public int AddedLines { get; }

    public int RemovedLines { get; }

    /// <summary>The number of change blocks.</summary>
    public int ChangeCount { get; }

    public bool IsIdentical => ChangeCount == 0;

    public static DiffDocument Compute(string? oldText, string? newText, bool ignoreWhitespace = false)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);
        IReadOnlyList<string> oldKeys = oldLines, newKeys = newLines;
        if (ignoreWhitespace)
        {
            oldKeys = oldLines.Select(WhitespaceKey).ToList();
            newKeys = newLines.Select(WhitespaceKey).ToList();
        }

        var spans = DiffAlgorithm.Diff(oldKeys, newKeys, StringComparer.Ordinal);
        var blocks = new List<DiffBlock>();
        for (var i = 0; i < spans.Count; i++)
        {
            var span = spans[i];
            if (span.Operation == DiffOperation.Equal)
            {
                blocks.Add(new DiffBlock(false, span.OldStart, span.OldLength, span.NewStart, span.NewLength));
                continue;
            }
            // A deletion and an insertion next to each other are one change: the old lines were replaced.
            var oldStart = span.OldStart;
            var newStart = span.NewStart;
            int oldCount = 0, newCount = 0;
            while (i < spans.Count && spans[i].Operation != DiffOperation.Equal)
            {
                oldCount += spans[i].OldLength;
                newCount += spans[i].NewLength;
                i++;
            }
            i--;
            var pairs = Enumerable.Range(0, Math.Min(oldCount, newCount))
                .Select(p => WordChanges(oldLines[oldStart + p], newLines[newStart + p], ignoreWhitespace))
                .ToList();
            blocks.Add(new DiffBlock(true, oldStart, oldCount, newStart, newCount) { Pairs = pairs });
        }
        return new DiffDocument(oldLines, newLines, blocks);
    }

    /// <summary>
    /// The rows to show: unchanged runs longer than twice <paramref name="contextLines"/> are folded except for
    /// <paramref name="contextLines"/> lines next to the changes (-1: nothing is folded), unless their block is in
    /// <paramref name="expandedBlocks"/>.
    /// </summary>
    public IReadOnlyList<DiffRow> BuildRows(DiffViewMode mode, int contextLines, IReadOnlySet<int>? expandedBlocks = null)
    {
        var rows = new List<DiffRow>();
        var change = 0;
        for (var b = 0; b < Blocks.Count; b++)
        {
            var block = Blocks[b];
            if (block.IsChange)
            {
                AddChange(rows, block, mode, change++);
                continue;
            }
            var count = block.OldCount;
            var head = b == 0 ? 0 : contextLines;
            var tail = b == Blocks.Count - 1 ? 0 : contextLines;
            var fold = contextLines >= 0 && !IsIdentical && count > head + tail + 1 && expandedBlocks?.Contains(b) != true;
            for (var i = 0; i < count; i++)
            {
                if (fold && i == head)
                {
                    rows.Add(new DiffRow { Kind = DiffRowKind.Fold, Block = b, HiddenCount = count - head - tail });
                    i = count - tail - 1;
                    continue;
                }
                rows.Add(new DiffRow
                {
                    Kind = DiffRowKind.Unchanged,
                    OldNumber = block.OldStart + i + 1,
                    NewNumber = block.NewStart + i + 1,
                    OldText = OldLines[block.OldStart + i],
                    NewText = NewLines[block.NewStart + i],
                });
            }
        }
        return rows;
    }

    private void AddChange(List<DiffRow> rows, DiffBlock block, DiffViewMode mode, int change)
    {
        if (mode == DiffViewMode.Inline)
        {
            for (var i = 0; i < block.OldCount; i++)
                rows.Add(Removed(block, i, change));
            for (var i = 0; i < block.NewCount; i++)
                rows.Add(Added(block, i, change));
            return;
        }
        for (var i = 0; i < Math.Max(block.OldCount, block.NewCount); i++)
        {
            if (i >= block.NewCount)
                rows.Add(Removed(block, i, change));
            else if (i >= block.OldCount)
                rows.Add(Added(block, i, change));
            else
                rows.Add(new DiffRow
                {
                    Kind = DiffRowKind.Changed,
                    OldNumber = block.OldStart + i + 1,
                    NewNumber = block.NewStart + i + 1,
                    OldText = OldLines[block.OldStart + i],
                    NewText = NewLines[block.NewStart + i],
                    OldChanges = block.Pairs[i].Old,
                    NewChanges = block.Pairs[i].New,
                    ChangeIndex = change,
                });
        }
    }

    private DiffRow Removed(DiffBlock block, int i, int change) => new()
    {
        Kind = DiffRowKind.Removed,
        OldNumber = block.OldStart + i + 1,
        OldText = OldLines[block.OldStart + i],
        OldChanges = i < block.Pairs.Count ? block.Pairs[i].Old : [],
        ChangeIndex = change,
    };

    private DiffRow Added(DiffBlock block, int i, int change) => new()
    {
        Kind = DiffRowKind.Added,
        NewNumber = block.NewStart + i + 1,
        NewText = NewLines[block.NewStart + i],
        NewChanges = i < block.Pairs.Count ? block.Pairs[i].New : [],
        ChangeIndex = change,
    };

    /// <summary>
    /// The difference as a unified diff (the format of <c>diff -u</c> and <c>git diff</c>): hunks of the changes with
    /// <paramref name="context"/> unchanged lines around them; changes closer than twice that share a hunk.
    /// Empty when the texts are identical.
    /// </summary>
    public string ToUnifiedDiff(string oldName = "a", string newName = "b", int context = 3)
    {
        if (IsIdentical)
            return "";
        context = Math.Max(0, context);
        var text = new StringBuilder();
        text.Append("--- ").Append(oldName).Append('\n').Append("+++ ").Append(newName).Append('\n');
        var changes = Enumerable.Range(0, Blocks.Count).Where(i => Blocks[i].IsChange).ToList();
        for (var c = 0; c < changes.Count; c++)
        {
            var first = changes[c];
            var last = first;
            // Between two changes there is always one block of equal lines.
            while (c + 1 < changes.Count && Blocks[last + 1].OldCount <= 2 * context)
                last = changes[++c];
            var before = first > 0 ? Math.Min(context, Blocks[first - 1].OldCount) : 0;
            var after = last < Blocks.Count - 1 ? Math.Min(context, Blocks[last + 1].OldCount) : 0;
            var oldStart = Blocks[first].OldStart - before;
            var newStart = Blocks[first].NewStart - before;

            var lines = new List<string>();
            for (var i = 0; i < before; i++)
                lines.Add(" " + OldLines[oldStart + i]);
            for (var b = first; b <= last; b++)
            {
                var block = Blocks[b];
                if (!block.IsChange)
                {
                    lines.AddRange(Enumerable.Range(block.OldStart, block.OldCount).Select(i => " " + OldLines[i]));
                    continue;
                }
                lines.AddRange(Enumerable.Range(block.OldStart, block.OldCount).Select(i => "-" + OldLines[i]));
                lines.AddRange(Enumerable.Range(block.NewStart, block.NewCount).Select(i => "+" + NewLines[i]));
            }
            var tail = Blocks[last].OldStart + Blocks[last].OldCount;
            for (var i = 0; i < after; i++)
                lines.Add(" " + OldLines[tail + i]);

            var oldCount = lines.Count(l => l[0] != '+');
            var newCount = lines.Count(l => l[0] != '-');
            // An empty range names the line before it (0 at the start of the file).
            text.Append(CultureInfo.InvariantCulture,
                $"@@ -{(oldCount == 0 ? oldStart : oldStart + 1)},{oldCount} +{(newCount == 0 ? newStart : newStart + 1)},{newCount} @@\n");
            foreach (var line in lines)
                text.Append(line).Append('\n');
        }
        return text.ToString();
    }

    /// <summary>The lines of a text; a line break at the end does not start another line.</summary>
    internal static IReadOnlyList<string> SplitLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return [];
        var lines = text.Split('\n');
        var count = lines.Length > 1 && lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        return lines.Take(count).Select(l => l.EndsWith('\r') ? l[..^1] : l).ToList();
    }

    private static string WhitespaceKey(string line) => WhitespaceRun().Replace(line.Trim(), " ");

    /// <summary>The changed words of two lines; none when they have too little in common to compare.</summary>
    internal static (IReadOnlyList<TextRange> Old, IReadOnlyList<TextRange> New) WordChanges(string oldLine, string newLine,
        bool ignoreWhitespace = false)
    {
        var oldTokens = Tokens(oldLine);
        var newTokens = Tokens(newLine);
        var spans = DiffAlgorithm.Diff(oldTokens.Select(t => t.Text).ToList(), newTokens.Select(t => t.Text).ToList(),
            ignoreWhitespace ? WhitespaceInsensitive.Instance : StringComparer.Ordinal, maxEdits: 200);
        var equal = spans.Where(s => s.Operation == DiffOperation.Equal)
            .Sum(s => oldTokens.Skip(s.OldStart).Take(s.OldLength).Sum(t => t.Text.Length));
        var longest = Math.Max(oldLine.Length, newLine.Length);
        if (longest == 0 || (double)equal / longest < MinSimilarity)
            return ([], []);

        var oldRanges = new List<TextRange>();
        var newRanges = new List<TextRange>();
        foreach (var span in spans)
        {
            if (span.Operation == DiffOperation.Equal)
                continue;
            AddRange(oldRanges, oldTokens, span.OldStart, span.OldLength, ignoreWhitespace);
            AddRange(newRanges, newTokens, span.NewStart, span.NewLength, ignoreWhitespace);
        }
        return (oldRanges, newRanges);
    }

    private static void AddRange(List<TextRange> ranges, List<(string Text, int Start)> tokens, int start, int count, bool ignoreWhitespace)
    {
        var changed = tokens.Skip(start).Take(count).Where(t => !ignoreWhitespace || t.Text.Trim().Length > 0).ToList();
        if (changed.Count == 0)
            return;
        var from = changed[0].Start;
        var to = changed[^1].Start + changed[^1].Text.Length;
        // Neighbouring ranges merge, so a changed phrase is one mark.
        if (ranges.Count > 0 && ranges[^1].Start + ranges[^1].Length == from)
            ranges[^1] = new TextRange(ranges[^1].Start, to - ranges[^1].Start);
        else
            ranges.Add(new TextRange(from, to - from));
    }

    private static List<(string Text, int Start)> Tokens(string line) =>
        TokenPattern().Matches(line).Select(m => (m.Value, m.Index)).ToList();

    [GeneratedRegex(@"\w+|\s+|[^\w\s]")]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    private sealed class WhitespaceInsensitive : IEqualityComparer<string>
    {
        public static readonly WhitespaceInsensitive Instance = new();

        public bool Equals(string? x, string? y) =>
            string.IsNullOrWhiteSpace(x) && string.IsNullOrWhiteSpace(y) || string.Equals(x, y, StringComparison.Ordinal);

        public int GetHashCode(string obj) => string.IsNullOrWhiteSpace(obj) ? 0 : obj.GetHashCode(StringComparison.Ordinal);
    }
}
