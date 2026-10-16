using System.Text;
using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Tests;

public class MarkdownParserTests
{
    /// <summary>The document as HTML in the format of the CommonMark spec examples, so they can be compared.</summary>
    private static string Html(string markdown)
    {
        var document = MarkdownDocument.Parse(markdown);
        var builder = new StringBuilder();
        Blocks(builder, document.Blocks, tight: false);
        return builder.ToString();
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static void Blocks(StringBuilder b, IEnumerable<MdBlock> blocks, bool tight)
    {
        foreach (var block in blocks)
        {
            switch (block)
            {
                case MdParagraph p when tight:
                    Inlines(b, p.Inlines);
                    b.Append('\n');
                    break;
                case MdParagraph p:
                    b.Append("<p>");
                    Inlines(b, p.Inlines);
                    b.Append("</p>\n");
                    break;
                case MdHeading h:
                    b.Append($"<h{h.Level}>");
                    Inlines(b, h.Inlines);
                    b.Append($"</h{h.Level}>\n");
                    break;
                case MdThematicBreak:
                    b.Append("<hr />\n");
                    break;
                case MdCodeBlock c:
                    b.Append(c.Language.Length > 0 ? $"<pre><code class=\"language-{Escape(c.Language)}\">" : "<pre><code>");
                    b.Append(Escape(c.Code)).Append(c.Code.Length > 0 ? "\n" : "").Append("</code></pre>\n");
                    break;
                case MdHtmlBlock h:
                    b.Append(h.Html).Append('\n');
                    break;
                case MdQuote q:
                    b.Append("<blockquote>\n");
                    Blocks(b, q.Children, false);
                    b.Append("</blockquote>\n");
                    break;
                case MdList l:
                    b.Append(l.IsOrdered ? l.Start == 1 ? "<ol>\n" : $"<ol start=\"{l.Start}\">\n" : "<ul>\n");
                    foreach (var item in l.Items)
                    {
                        b.Append("<li>");
                        if (item.IsTask)
                            b.Append(item.IsChecked ? "[x] " : "[ ] ");
                        if (!l.IsTight && item.Children.Count > 0)
                            b.Append('\n');
                        var inner = new StringBuilder();
                        Blocks(inner, item.Children, l.IsTight);
                        var text = inner.ToString();
                        // Tight items end without the new line of their last paragraph.
                        if (l.IsTight && item.Children.LastOrDefault() is MdParagraph && text.EndsWith('\n'))
                            text = text[..^1];
                        if (l.IsTight && item.Children.FirstOrDefault() is not MdParagraph and not null)
                            text = "\n" + text;
                        b.Append(text).Append("</li>\n");
                    }
                    b.Append(l.IsOrdered ? "</ol>\n" : "</ul>\n");
                    break;
                case MdTable t:
                    b.Append("<table>\n");
                    foreach (var row in t.Rows)
                    {
                        b.Append("<tr>");
                        foreach (var cell in row.Cells)
                        {
                            b.Append(row.IsHeader ? "<th>" : "<td>");
                            Inlines(b, cell);
                            b.Append(row.IsHeader ? "</th>" : "</td>");
                        }
                        b.Append("</tr>\n");
                    }
                    b.Append("</table>\n");
                    break;
            }
        }
    }

    private static void Inlines(StringBuilder b, IEnumerable<MdInline> inlines)
    {
        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case MdText t:
                    b.Append(Escape(t.Text));
                    break;
                case MdSoftBreak:
                    b.Append('\n');
                    break;
                case MdLineBreak:
                    b.Append("<br />\n");
                    break;
                case MdCode c:
                    b.Append("<code>").Append(Escape(c.Text)).Append("</code>");
                    break;
                case MdEmphasis e:
                    b.Append("<em>");
                    Inlines(b, e.Children);
                    b.Append("</em>");
                    break;
                case MdStrong s:
                    b.Append("<strong>");
                    Inlines(b, s.Children);
                    b.Append("</strong>");
                    break;
                case MdStrikethrough s:
                    b.Append("<del>");
                    Inlines(b, s.Children);
                    b.Append("</del>");
                    break;
                case MdLink l:
                    b.Append($"<a href=\"{Escape(l.Url)}\"").Append(l.Title is null ? "" : $" title=\"{Escape(l.Title)}\"").Append('>');
                    Inlines(b, l.Children);
                    b.Append("</a>");
                    break;
                case MdImage i:
                    b.Append($"<img src=\"{Escape(i.Url)}\" alt=\"{Escape(i.AltText)}\"").Append(i.Title is null ? "" : $" title=\"{Escape(i.Title)}\"").Append(" />");
                    break;
                case MdHtmlInline h:
                    b.Append(h.Html);
                    break;
                case MdFootnoteReference f:
                    b.Append($"<sup>{f.Definition.Number}</sup>");
                    break;
            }
        }
    }

    public static TheoryData<string, string> CommonMarkExamples => LoadExamples("commonmark-examples.json");

    public static TheoryData<string, string> GfmExamples => LoadExamples("gfm-examples.json");

    private static TheoryData<string, string> LoadExamples(string fileName)
    {
        using var stream = typeof(MarkdownParserTests).Assembly.GetManifestResourceStream(
            $"KD.Avalonia.Rice.Tests.TestData.{fileName}")
            ?? throw new InvalidOperationException($"Missing Markdown test data: {fileName}");
        var examples = System.Text.Json.JsonSerializer.Deserialize<string[][]>(stream)
            ?? throw new InvalidOperationException($"Invalid Markdown test data: {fileName}");
        var result = new TheoryData<string, string>();
        foreach (var example in examples)
        {
            if (example.Length != 2)
                throw new InvalidOperationException($"Expected input/output pair in {fileName}.");
            result.Add(example[0], example[1]);
        }
        return result;
    }

    [Theory]
    [MemberData(nameof(CommonMarkExamples))]
    public void CommonMark_examples(string markdown, string html) => Assert.Equal(html, Html(markdown));

    [Theory]
    [MemberData(nameof(GfmExamples))]
    public void Gfm_extensions(string markdown, string html) => Assert.Equal(html, Html(markdown));

    [Fact]
    public void Table_alignments_are_read()
    {
        var table = Assert.IsType<MdTable>(MarkdownDocument.Parse("| a | b | c | d |\n|:--|:-:|--:|---|\n").Blocks[0]);

        Assert.Equal([MdTableAlignment.Left, MdTableAlignment.Center, MdTableAlignment.Right, MdTableAlignment.None], table.Alignments);
    }

    [Fact]
    public void Footnotes_are_numbered_by_first_reference_and_keep_their_content()
    {
        var document = MarkdownDocument.Parse("B[^b] A[^a] B again[^b]\n\n[^a]: Note *a*.\n[^b]: Note b.\n\n    more of b\n");

        Assert.Equal(["b", "a"], document.Footnotes.Select(f => f.Label));
        Assert.Equal([1, 2], document.Footnotes.Select(f => f.Number));
        Assert.Equal(2, document.Footnotes[0].Children.Count);
        Assert.IsType<MdEmphasis>(Assert.IsType<MdParagraph>(document.Footnotes[1].Children[0]).Inlines[1]);
        // The definitions are not part of the body.
        Assert.Single(document.Blocks);
    }

    [Fact]
    public void Headings_get_unique_github_slugs()
    {
        var document = MarkdownDocument.Parse("# Getting started!\n## Getting started\n### C# & .NET 10\n");

        Assert.Equal(["getting-started", "getting-started-1", "c--net-10"], document.Headings.Select(h => h.Slug));
    }

    [Fact]
    public void Code_block_language_is_the_first_word_of_the_info_string()
    {
        var code = Assert.IsType<MdCodeBlock>(MarkdownDocument.Parse("```CSharp title=\"x.cs\"\nvar x = 1;\n```").Blocks[0]);

        Assert.Equal("csharp", code.Language);
        Assert.Equal("CSharp title=\"x.cs\"", code.Info);
        Assert.Equal("var x = 1;", code.Code);
        Assert.Equal(1, code.Line);
    }

    [Fact]
    public void Task_items_are_marked()
    {
        var list = Assert.IsType<MdList>(MarkdownDocument.Parse("- [x] done\n- [ ] todo\n- [] not a task\n").Blocks[0]);

        Assert.Equal([true, true, false], list.Items.Select(i => i.IsTask));
        Assert.Equal([true, false, false], list.Items.Select(i => i.IsChecked));
        Assert.Equal("done", Assert.IsType<MdParagraph>(list.Items[0].Children[0]).PlainText);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n\n\n")]
    [InlineData("*[`<![CDATA[")]
    [InlineData("> - > - ```\n\t\t\t~~~")]
    [InlineData("[a]: <\n[b](\n![c](d \"e\n|x|\n|-|\n|")]
    [InlineData("***___***___~~~~**_*_")]
    [InlineData("\r\n\r# a\r\n- b\r")]
    public void Odd_input_never_throws(string markdown)
    {
        var document = MarkdownDocument.Parse(markdown);

        Assert.NotNull(document.Blocks);
    }

    [Fact]
    public void Repository_documents_are_parsed()
    {
        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "README.md")))
            root = Path.GetDirectoryName(root);
        Assert.NotNull(root);
        foreach (var file in Directory.GetFiles(Path.Combine(root, "docs"), "*.md").Append(Path.Combine(root, "README.md")))
        {
            var document = MarkdownDocument.Load(file);
            Assert.NotEmpty(document.Blocks);
            Assert.NotEmpty(document.Headings);
        }
    }
}
