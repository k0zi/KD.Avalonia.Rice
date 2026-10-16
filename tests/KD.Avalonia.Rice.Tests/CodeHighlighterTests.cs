using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Tests;

public class CodeHighlighterTests
{
    private static List<(string Text, CodeTokenKind Kind)> Tokens(string code, string language) =>
        CodeHighlighter.Highlight(code, language).Select(t => (code.Substring(t.Start, t.Length), t.Kind)).ToList();

    [Fact]
    public void Csharp_keywords_strings_comments_numbers_and_types()
    {
        var tokens = Tokens("// note \"x\"\nvar list = new List<int> { 42, 0x1F }; string s = $\"a{b}\";", "cs");

        Assert.Contains(("// note \"x\"", CodeTokenKind.Comment), tokens);
        Assert.Contains(("var", CodeTokenKind.Keyword), tokens);
        Assert.Contains(("List", CodeTokenKind.Type), tokens);
        Assert.Contains(("42", CodeTokenKind.Number), tokens);
        Assert.Contains(("0x1F", CodeTokenKind.Number), tokens);
        Assert.Contains(("$\"a{b}\"", CodeTokenKind.String), tokens);
        // Nothing is found inside the comment.
        Assert.DoesNotContain(tokens, t => t.Text == "\"x\"");
    }

    [Fact]
    public void Xml_tags_attributes_and_values()
    {
        var tokens = Tokens("<Button Content=\"OK\" /><!-- c -->", "xaml");

        Assert.Equal([("<Button", CodeTokenKind.Tag), ("Content", CodeTokenKind.Attribute), ("\"OK\"", CodeTokenKind.String), ("/>", CodeTokenKind.Tag), ("<!-- c -->", CodeTokenKind.Comment)], tokens);
    }

    [Fact]
    public void Json_property_names_differ_from_values()
    {
        var tokens = Tokens("{\"a\": \"b\", \"n\": -1.5, \"t\": true}", "json");

        Assert.Contains(("\"a\"", CodeTokenKind.Attribute), tokens);
        Assert.Contains(("\"b\"", CodeTokenKind.String), tokens);
        Assert.Contains(("-1.5", CodeTokenKind.Number), tokens);
        Assert.Contains(("true", CodeTokenKind.Keyword), tokens);
    }

    [Theory]
    [InlineData("python", "def f(): # c", "def")]
    [InlineData("bash", "if [ -f x ]; then echo $HOME; fi", "then")]
    [InlineData("sql", "SELECT id FROM t", "SELECT")]
    [InlineData("ts", "const x: number = 1", "const")]
    [InlineData("yaml", "key: true", "true")]
    [InlineData("plantuml", "@startuml\nclass A", "class")]
    [InlineData("mermaid", "flowchart LR", "flowchart")]
    public void Languages_have_keywords(string language, string code, string keyword)
    {
        Assert.True(CodeHighlighter.IsSupported(language));
        Assert.Contains((keyword, CodeTokenKind.Keyword), Tokens(code, language));
    }

    [Fact]
    public void Unknown_language_is_plain_and_tokens_do_not_overlap()
    {
        Assert.Empty(CodeHighlighter.Highlight("anything", "brainfuck"));
        Assert.Empty(CodeHighlighter.Highlight("anything", null));
        var tokens = CodeHighlighter.Highlight(MarkdownSamplesCode, "csharp");
        for (var i = 1; i < tokens.Count; i++)
            Assert.True(tokens[i].Start >= tokens[i - 1].Start + tokens[i - 1].Length);
    }

    private const string MarkdownSamplesCode = """
        /* block */ public sealed class A<T> : B where T : new() { private readonly string _s = @"x""y"; }
        """;
}
