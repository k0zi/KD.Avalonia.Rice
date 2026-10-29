using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Tests;

public class CodeEditingTests
{
    private static string Apply(string text, CodeEdit edit) =>
        text[..edit.Start] + edit.Replacement + text[(edit.Start + edit.Length)..];

    [Fact]
    public void Tab_inserts_spaces_up_to_the_next_tab_stop()
    {
        var edit = CodeEditing.InsertTab("ab", 2, 2, 4, useSpaces: true);

        Assert.Equal("ab  ", Apply("ab", edit));
        Assert.Equal(4, edit.SelectionEnd);
        Assert.Equal("\t", CodeEditing.InsertTab("ab", 2, 2, 4, useSpaces: false).Replacement);
    }

    [Fact]
    public void Tab_on_several_lines_indents_every_non_empty_line()
    {
        const string text = "a\n\nb\nc";
        var edit = CodeEditing.Indent(text, 0, 5, "    ");

        // The selection ends at the start of "c": that line is not taken.
        Assert.Equal("    a\n\n    b\nc", Apply(text, edit));
        Assert.Equal((0, 13), (edit.SelectionStart, edit.SelectionEnd));
    }

    [Fact]
    public void Shift_tab_removes_one_level_and_keeps_the_rest()
    {
        const string text = "      a\n\tb\nc";
        var edit = CodeEditing.Outdent(text, 0, text.Length, 4)!.Value;

        Assert.Equal("  a\nb\nc", Apply(text, edit));
        Assert.Null(CodeEditing.Outdent("a\nb", 0, 3, 4));
    }

    [Fact]
    public void Enter_keeps_the_indentation_and_opens_brackets()
    {
        const string text = "    if (x)\n    {}";
        var plain = CodeEditing.NewLine(text, 10, 10, "\n", "    ");
        Assert.Equal("    if (x)\n    \n    {}", Apply(text, plain));

        var braces = CodeEditing.NewLine(text, text.Length - 1, text.Length - 1, "\n", "    ");
        Assert.Equal("    if (x)\n    {\n        \n    }", Apply(text, braces));
        Assert.Equal(text.Length - 1 + 1 + 8, braces.SelectionEnd);
    }

    [Theory]
    [InlineData("        x", 8, 4)]
    [InlineData("      x", 6, 2)]
    public void Backspace_in_the_indentation_goes_back_to_the_tab_stop(string text, int caret, int removed)
    {
        var edit = CodeEditing.Backspace(text, caret, 4)!.Value;

        Assert.Equal(removed, edit.Length);
        Assert.Equal(caret - removed, edit.SelectionEnd);
    }

    [Theory]
    [InlineData("    x", 5)]
    [InlineData("     ", 5)]
    [InlineData("ab", 1)]
    public void Backspace_elsewhere_is_left_to_the_text_box(string text, int caret)
    {
        Assert.Null(CodeEditing.Backspace(text, caret, 4));
    }

    [Fact]
    public void Ctrl_slash_comments_at_the_smallest_indentation_and_back()
    {
        const string text = "  a\n    b\n\n  c";
        var comment = CodeEditing.ToggleLineComment(text, 0, text.Length, "//")!.Value;
        var commented = Apply(text, comment);
        Assert.Equal("  // a\n  //   b\n\n  // c", commented);

        var uncomment = CodeEditing.ToggleLineComment(commented, 0, commented.Length, "//")!.Value;
        Assert.Equal(text, Apply(commented, uncomment));
    }

    [Fact]
    public void Mixed_lines_are_commented_out_rather_than_uncommented()
    {
        const string text = "// a\nb";
        var edit = CodeEditing.ToggleLineComment(text, 0, text.Length, "//")!.Value;

        Assert.Equal("// // a\n// b", Apply(text, edit));
    }

    [Theory]
    [InlineData("cs", "//")]
    [InlineData("ts", "//")]
    [InlineData("py", "#")]
    [InlineData("yml", "#")]
    [InlineData("sql", "--")]
    [InlineData("xaml", null)]
    [InlineData(null, null)]
    public void Languages_have_their_line_comment(string? language, string? prefix)
    {
        Assert.Equal(prefix, CodeEditing.LineCommentPrefix(language));
    }

    [Fact]
    public void Line_span_covers_whole_lines_without_the_line_break()
    {
        const string text = "ab\r\ncd\nef";

        Assert.Equal((0, 2), CodeEditing.LineSpan(text, 1, 1));
        Assert.Equal((4, 9), CodeEditing.LineSpan(text, 5, 8));
        Assert.True(CodeEditing.IsMultiLine(text, 1, 5));
        Assert.False(CodeEditing.IsMultiLine(text, 4, 6));
    }

    [Fact]
    public void Gutter_counts_lines_and_finds_the_caret_line()
    {
        Assert.Equal(1, RiceCodeGutter.LineCount(""));
        Assert.Equal(3, RiceCodeGutter.LineCount("a\nb\n"));
        Assert.Equal(2, RiceCodeGutter.CaretLine("a\nb", 2));
    }
}
