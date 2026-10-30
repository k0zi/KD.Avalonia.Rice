using KD.Avalonia.Rice.Controls.Diffs;

namespace KD.Avalonia.Rice.Tests;

public class DiffTests
{
    private static string Script(IReadOnlyList<DiffSpan> spans) => string.Concat(spans.Select(s => s.Operation switch
    {
        DiffOperation.Equal => new string('=', s.OldLength),
        DiffOperation.Delete => new string('-', s.OldLength),
        _ => new string('+', s.NewLength),
    }));

    [Theory]
    [InlineData("ABCABBA", "CBABAC", 5)]
    [InlineData("abc", "abc", 0)]
    [InlineData("", "abc", 3)]
    [InlineData("abc", "", 3)]
    [InlineData("kitten", "sitting", 5)]
    public void Myers_finds_the_shortest_edit_script(string a, string b, int edits)
    {
        var spans = DiffAlgorithm.Diff(a.ToList(), b.ToList());
        var script = Script(spans);

        Assert.Equal(edits, script.Count(c => c != '='));
        // Replaying the script on the old sequence gives the new one.
        var result = new List<char>();
        foreach (var span in spans)
        {
            if (span.Operation == DiffOperation.Equal)
                result.AddRange(a.Skip(span.OldStart).Take(span.OldLength));
            else if (span.Operation == DiffOperation.Insert)
                result.AddRange(b.Skip(span.NewStart).Take(span.NewLength));
        }
        Assert.Equal(b, new string(result.ToArray()));
    }

    [Fact]
    public void Too_many_edits_become_one_replacement()
    {
        var spans = DiffAlgorithm.Diff("abcdef".ToList(), "uvwxyz".ToList(), maxEdits: 3);

        Assert.Equal("------++++++", Script(spans));
    }

    [Fact]
    public void Lines_are_split_without_a_last_empty_line_and_without_carriage_returns()
    {
        Assert.Equal(["a", "b"], DiffDocument.SplitLines("a\r\nb\n"));
        Assert.Equal(["a", ""], DiffDocument.SplitLines("a\n\n"));
        Assert.Empty(DiffDocument.SplitLines(""));
    }

    [Fact]
    public void A_replaced_line_is_one_change_with_its_words_marked()
    {
        var document = DiffDocument.Compute("a\nint x = 4;\nb", "a\nint x = 6;\nb");

        Assert.Equal(1, document.ChangeCount);
        Assert.Equal((1, 1), (document.AddedLines, document.RemovedLines));
        var row = document.BuildRows(DiffViewMode.SideBySide, -1).Single(r => r.Kind == DiffRowKind.Changed);
        Assert.Equal([new TextRange(8, 1)], row.OldChanges);
        Assert.Equal([new TextRange(8, 1)], row.NewChanges);
    }

    [Fact]
    public void Inline_rows_list_the_removed_lines_before_the_added_ones()
    {
        var document = DiffDocument.Compute("x\ny", "z");
        var rows = document.BuildRows(DiffViewMode.Inline, -1);

        Assert.Equal([DiffRowKind.Removed, DiffRowKind.Removed, DiffRowKind.Added], rows.Select(r => r.Kind));
        Assert.Equal([1, 2, null], rows.Select(r => r.OldNumber));
    }

    [Fact]
    public void Long_unchanged_runs_are_folded_around_the_context()
    {
        var old = string.Join('\n', Enumerable.Range(1, 20));
        var @new = old.Replace("10", "ten");
        var document = DiffDocument.Compute(old, @new);

        var rows = document.BuildRows(DiffViewMode.SideBySide, 2);
        // 1–7 folded, 8 9 context, the change, 11 12 context, 13–20 folded.
        Assert.Equal([DiffRowKind.Fold, DiffRowKind.Unchanged, DiffRowKind.Unchanged, DiffRowKind.Changed,
            DiffRowKind.Unchanged, DiffRowKind.Unchanged, DiffRowKind.Fold], rows.Select(r => r.Kind));
        Assert.Equal(7, rows[0].HiddenCount);

        var expanded = document.BuildRows(DiffViewMode.SideBySide, 2, new HashSet<int> { rows[0].Block });
        Assert.Equal(13, expanded.Count);
    }

    [Fact]
    public void Ignoring_whitespace_makes_reindented_lines_equal()
    {
        Assert.True(DiffDocument.Compute("if (a)\n  b();", "if (a)\n    b( );", ignoreWhitespace: false).ChangeCount > 0);
        Assert.True(DiffDocument.Compute("if (a)\n  b();", "if (a)\n\tb();", ignoreWhitespace: true).IsIdentical);
    }

    [Fact]
    public void Unified_diff_has_hunks_with_context()
    {
        var old = string.Join('\n', Enumerable.Range(1, 10));
        var @new = old.Replace("5", "five");
        var text = DiffDocument.Compute(old, @new).ToUnifiedDiff("a.txt", "b.txt", 2);

        Assert.Equal("--- a.txt\n+++ b.txt\n@@ -3,5 +3,5 @@\n 3\n 4\n-5\n+five\n 6\n 7\n", text);
        Assert.Equal("", DiffDocument.Compute("x", "x").ToUnifiedDiff());
    }

    [Fact]
    public void Dissimilar_lines_get_no_word_marks()
    {
        var (old, @new) = DiffDocument.WordChanges("return a;", "while (true) { Run(); }");

        Assert.Empty(old);
        Assert.Empty(@new);
    }
}
