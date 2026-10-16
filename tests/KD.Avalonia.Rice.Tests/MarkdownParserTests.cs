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

    [Theory]
    // Paragraphs and line breaks
    [InlineData("aaa\n\nbbb\n", "<p>aaa</p>\n<p>bbb</p>\n")]
    [InlineData("aaa\nbbb\n", "<p>aaa\nbbb</p>\n")]
    [InlineData("  aaa\n bbb\n", "<p>aaa\nbbb</p>\n")]
    [InlineData("foo  \nbaz\n", "<p>foo<br />\nbaz</p>\n")]
    [InlineData("foo\\\nbaz\n", "<p>foo<br />\nbaz</p>\n")]
    [InlineData("foo  \n", "<p>foo</p>\n")]
    // Headings
    [InlineData("# foo\n## foo\n###### foo\n", "<h1>foo</h1>\n<h2>foo</h2>\n<h6>foo</h6>\n")]
    [InlineData("####### foo\n", "<p>####### foo</p>\n")]
    [InlineData("#5 bolt\n", "<p>#5 bolt</p>\n")]
    [InlineData("# foo ##\n", "<h1>foo</h1>\n")]
    [InlineData("### foo ### b\n", "<h3>foo ### b</h3>\n")]
    [InlineData("Foo *bar*\n=========\n\nFoo *bar*\n---------\n", "<h1>Foo <em>bar</em></h1>\n<h2>Foo <em>bar</em></h2>\n")]
    [InlineData("Foo\nbar\n---\n", "<h2>Foo\nbar</h2>\n")]
    // Thematic breaks
    [InlineData("***\n---\n___\n", "<hr />\n<hr />\n<hr />\n")]
    [InlineData(" - - -\n", "<hr />\n")]
    [InlineData("--\n", "<p>--</p>\n")]
    // Code
    [InlineData("    a simple\n      indented code block\n", "<pre><code>a simple\n  indented code block\n</code></pre>\n")]
    [InlineData("```\n<\n >\n```\n", "<pre><code>&lt;\n &gt;\n</code></pre>\n")]
    [InlineData("~~~ruby startline=3\ndef foo(x)\nend\n~~~\n", "<pre><code class=\"language-ruby\">def foo(x)\nend\n</code></pre>\n")]
    [InlineData("```\naaa\n~~~\n```\n", "<pre><code>aaa\n~~~\n</code></pre>\n")]
    [InlineData(" ```\n aaa\naaa\n```\n", "<pre><code>aaa\naaa\n</code></pre>\n")]
    [InlineData("```\nabc\n", "<pre><code>abc\n</code></pre>\n")]
    [InlineData("Foo\n    bar\n", "<p>Foo\nbar</p>\n")]
    // Block quotes
    [InlineData("> # Foo\n> bar\n> baz\n", "<blockquote>\n<h1>Foo</h1>\n<p>bar\nbaz</p>\n</blockquote>\n")]
    [InlineData("> bar\nbaz\n> foo\n", "<blockquote>\n<p>bar\nbaz\nfoo</p>\n</blockquote>\n")]
    [InlineData("> foo\n---\n", "<blockquote>\n<p>foo</p>\n</blockquote>\n<hr />\n")]
    [InlineData("> a\n>\n> > b\n", "<blockquote>\n<p>a</p>\n<blockquote>\n<p>b</p>\n</blockquote>\n</blockquote>\n")]
    // Lists
    [InlineData("- foo\n- bar\n+ baz\n", "<ul>\n<li>foo</li>\n<li>bar</li>\n</ul>\n<ul>\n<li>baz</li>\n</ul>\n")]
    [InlineData("1. foo\n2. bar\n3) baz\n", "<ol>\n<li>foo</li>\n<li>bar</li>\n</ol>\n<ol start=\"3\">\n<li>baz</li>\n</ol>\n")]
    [InlineData("- foo\n\n- bar\n", "<ul>\n<li>\n<p>foo</p>\n</li>\n<li>\n<p>bar</p>\n</li>\n</ul>\n")]
    [InlineData("- a\n  - b\n    - c\n", "<ul>\n<li>a\n<ul>\n<li>b\n<ul>\n<li>c</li>\n</ul>\n</li>\n</ul>\n</li>\n</ul>\n")]
    [InlineData("The number of windows in my house is\n14.  The number of doors is 6.\n", "<p>The number of windows in my house is\n14.  The number of doors is 6.</p>\n")]
    [InlineData("1. a\n\n   b\n", "<ol>\n<li>\n<p>a</p>\n<p>b</p>\n</li>\n</ol>\n")]
    [InlineData("-one\n\n2.two\n", "<p>-one</p>\n<p>2.two</p>\n")]
    [InlineData("- a\n- b\n\n  c\n- d\n", "<ul>\n<li>\n<p>a</p>\n</li>\n<li>\n<p>b</p>\n<p>c</p>\n</li>\n<li>\n<p>d</p>\n</li>\n</ul>\n")]
    [InlineData("123456789. ok\n", "<ol start=\"123456789\">\n<li>ok</li>\n</ol>\n")]
    // Emphasis
    [InlineData("*foo bar*\n", "<p><em>foo bar</em></p>\n")]
    [InlineData("a * foo bar*\n", "<p>a * foo bar*</p>\n")]
    [InlineData("foo*bar*\n", "<p>foo<em>bar</em></p>\n")]
    [InlineData("foo_bar_\n", "<p>foo_bar_</p>\n")]
    [InlineData("_foo_bar_baz_\n", "<p><em>foo_bar_baz</em></p>\n")]
    [InlineData("**foo bar**\n", "<p><strong>foo bar</strong></p>\n")]
    [InlineData("***strong emph***\n", "<p><em><strong>strong emph</strong></em></p>\n")]
    [InlineData("*foo**bar**baz*\n", "<p><em>foo<strong>bar</strong>baz</em></p>\n")]
    [InlineData("*foo**bar*\n", "<p><em>foo**bar</em></p>\n")]
    [InlineData("**foo*bar*baz**\n", "<p><strong>foo<em>bar</em>baz</strong></p>\n")]
    [InlineData("foo***bar***baz\n", "<p>foo<em><strong>bar</strong></em>baz</p>\n")]
    [InlineData("*foo [bar](/url)*\n", "<p><em>foo <a href=\"/url\">bar</a></em></p>\n")]
    [InlineData("**foo*\n", "<p>*<em>foo</em></p>\n")]
    [InlineData("*foo**\n", "<p><em>foo</em>*</p>\n")]
    // Code spans, escapes, entities
    [InlineData("`foo`\n", "<p><code>foo</code></p>\n")]
    [InlineData("`` foo ` bar ``\n", "<p><code>foo ` bar</code></p>\n")]
    [InlineData("`foo\nbar  \nbaz`\n", "<p><code>foo bar   baz</code></p>\n")]
    [InlineData("`foo\\`bar`\n", "<p><code>foo\\</code>bar`</p>\n")]
    [InlineData("*foo`*`\n", "<p>*foo<code>*</code></p>\n")]
    [InlineData("\\*not emphasized*\n", "<p>*not emphasized*</p>\n")]
    [InlineData("&copy; &#35; &#x22; &nbsp;x\n", "<p>© # &quot;  x</p>\n")]
    // Links and images
    [InlineData("[link](/uri \"title\")\n", "<p><a href=\"/uri\" title=\"title\">link</a></p>\n")]
    [InlineData("[link]()\n", "<p><a href=\"\">link</a></p>\n")]
    [InlineData("[link](<foo bar>)\n", "<p><a href=\"foo bar\">link</a></p>\n")]
    [InlineData("[link](foo(and(bar)))\n", "<p><a href=\"foo(and(bar))\">link</a></p>\n")]
    [InlineData("[link [foo [bar]]](/uri)\n", "<p><a href=\"/uri\">link [foo [bar]]</a></p>\n")]
    [InlineData("[foo [bar](/uri)](/uri)\n", "<p>[foo <a href=\"/uri\">bar</a>](/uri)</p>\n")]
    [InlineData("[foo]: /url \"title\"\n\n[foo]\n", "<p><a href=\"/url\" title=\"title\">foo</a></p>\n")]
    [InlineData("[foo][bar]\n\n[BAR]: /url\n", "<p><a href=\"/url\">foo</a></p>\n")]
    [InlineData("[foo][]\n\n[foo]: /url\n", "<p><a href=\"/url\">foo</a></p>\n")]
    [InlineData("[Foo\n  bar]: /url\n\n[Baz][Foo bar]\n", "<p><a href=\"/url\">Baz</a></p>\n")]
    [InlineData("[foo]: /url\n=== \n[foo]\n", "<p>===\n<a href=\"/url\">foo</a></p>\n")]
    [InlineData("![foo *bar*](train.jpg \"train & tracks\")\n", "<p><img src=\"train.jpg\" alt=\"foo bar\" title=\"train &amp; tracks\" /></p>\n")]
    [InlineData("[![moon](moon.jpg)](/uri)\n", "<p><a href=\"/uri\"><img src=\"moon.jpg\" alt=\"moon\" /></a></p>\n")]
    [InlineData("<http://foo.bar.baz>\n", "<p><a href=\"http://foo.bar.baz\">http://foo.bar.baz</a></p>\n")]
    [InlineData("<foo@bar.example.com>\n", "<p><a href=\"mailto:foo@bar.example.com\">foo@bar.example.com</a></p>\n")]
    [InlineData("[foo]\n", "<p>[foo]</p>\n")]
    // HTML
    [InlineData("<div>\n*hello*\n</div>\n", "<div>\n*hello*\n</div>\n")]
    [InlineData("foo <b>bar</b>\n", "<p>foo <b>bar</b></p>\n")]
    [InlineData("<!-- comment -->\nafter\n", "<!-- comment -->\n<p>after</p>\n")]
    // Tabs
    [InlineData("\tfoo\tbaz\t\tbim\n", "<pre><code>foo\tbaz\t\tbim\n</code></pre>\n")]
    [InlineData("- foo\n\n\tbar\n", "<ul>\n<li>\n<p>foo</p>\n<p>bar</p>\n</li>\n</ul>\n")]
    public void CommonMark_examples(string markdown, string html) => Assert.Equal(html, Html(markdown));

    [Theory]
    [InlineData("~~Hi~~ Hello, ~there~ world!\n", "<p><del>Hi</del> Hello, <del>there</del> world!</p>\n")]
    [InlineData("x ~~~not struck~~~ y\n", "<p>x ~~~not struck~~~ y</p>\n")]
    [InlineData("- [ ] foo\n- [x] bar\n", "<ul>\n<li>[ ] foo</li>\n<li>[x] bar</li>\n</ul>\n")]
    [InlineData("| foo | bar |\n| --- | --- |\n| baz | bim |\n", "<table>\n<tr><th>foo</th><th>bar</th></tr>\n<tr><td>baz</td><td>bim</td></tr>\n</table>\n")]
    [InlineData("| f\\|oo  |\n| ------ |\n| b `\\|` az |\n| b **\\|** im |\n", "<table>\n<tr><th>f|oo</th></tr>\n<tr><td>b <code>|</code> az</td></tr>\n<tr><td>b <strong>|</strong> im</td></tr>\n</table>\n")]
    [InlineData("| abc | def |\n| --- | --- |\n| bar |\n| bar | baz | boo |\n", "<table>\n<tr><th>abc</th><th>def</th></tr>\n<tr><td>bar</td><td></td></tr>\n<tr><td>bar</td><td>baz</td></tr>\n</table>\n")]
    [InlineData("| abc | def |\n| --- |\n| bar |\n", "<p>| abc | def |\n| --- |\n| bar |</p>\n")]
    [InlineData("intro\n| a |\n| - |\n| b |\n", "<p>intro</p>\n<table>\n<tr><th>a</th></tr>\n<tr><td>b</td></tr>\n</table>\n")]
    [InlineData("www.commonmark.org/help for more.\n", "<p><a href=\"http://www.commonmark.org/help\">www.commonmark.org/help</a> for more.</p>\n")]
    [InlineData("Visit https://example.com/a_b_c.\n", "<p>Visit <a href=\"https://example.com/a_b_c\">https://example.com/a_b_c</a>.</p>\n")]
    [InlineData("(https://example.com/x)\n", "<p>(<a href=\"https://example.com/x\">https://example.com/x</a>)</p>\n")]
    [InlineData("mail foo@bar.baz now\n", "<p>mail <a href=\"mailto:foo@bar.baz\">foo@bar.baz</a> now</p>\n")]
    [InlineData("[x](https://a.b) https://c.d\n", "<p><a href=\"https://a.b\">x</a> <a href=\"https://c.d\">https://c.d</a></p>\n")]
    [InlineData("Text[^1] and [^n].\n\n[^1]: First.\n[^n]: Second.\n", "<p>Text<sup>1</sup> and <sup>2</sup>.</p>\n")]
    [InlineData("[^missing] x\n", "<p>[^missing] x</p>\n")]
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
