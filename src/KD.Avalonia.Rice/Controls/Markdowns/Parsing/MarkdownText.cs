using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>Patterns and text helpers of the CommonMark spec.</summary>
internal static partial class MarkdownText
{
    private const string TagName = "[A-Za-z][A-Za-z0-9-]*";
    private const string AttributeName = "[a-zA-Z_:][a-zA-Z0-9:._-]*";
    private const string AttributeValue = "(?:[^\"'=<>`\\x00-\\x20]+|'[^']*'|\"[^\"]*\")";
    private const string Attribute = "(?:\\s+" + AttributeName + "(?:\\s*=\\s*" + AttributeValue + ")?)";
    public const string OpenTag = "<" + TagName + Attribute + "*\\s*/?>";
    public const string CloseTag = "</" + TagName + "\\s*[>]";
    private const string HtmlComment = "<!-->|<!--->|<!--[\\s\\S]*?-->";
    private const string ProcessingInstruction = "[<][?][\\s\\S]*?[?][>]";
    private const string Declaration = "<![A-Za-z]+[^>]*>";
    private const string CData = "<!\\[CDATA\\[[\\s\\S]*?\\]\\]>";
    private const string HtmlTagPattern = "(?:" + OpenTag + "|" + CloseTag + "|" + HtmlComment + "|" + ProcessingInstruction + "|" + Declaration + "|" + CData + ")";

    /// <summary>Openers of the 7 kinds of HTML blocks (index 0 unused).</summary>
    public static readonly Regex[] HtmlBlockOpen =
    [
        new("."),
        new(@"^<(?:script|pre|textarea|style)(?:\s|>|$)", RegexOptions.IgnoreCase),
        new("^<!--"),
        new("^<[?]"),
        new("^<![A-Za-z]"),
        new(@"^<!\[CDATA\["),
        new(@"^<[/]?(?:address|article|aside|base|basefont|blockquote|body|caption|center|col|colgroup|dd|details|dialog|dir|div|dl|dt|fieldset|figcaption|figure|footer|form|frame|frameset|h[123456]|head|header|hr|html|iframe|legend|li|link|main|menu|menuitem|nav|noframes|ol|optgroup|option|p|param|search|section|summary|table|tbody|td|tfoot|th|thead|title|tr|track|ul)(?:\s|[/]?[>]|$)", RegexOptions.IgnoreCase),
        new("^(?:" + OpenTag + "|" + CloseTag + ")\\s*$", RegexOptions.IgnoreCase),
    ];

    /// <summary>Closers of the HTML block kinds 1–5.</summary>
    public static readonly Regex[] HtmlBlockClose =
    [
        new("."),
        new("</(?:script|pre|textarea|style)>", RegexOptions.IgnoreCase),
        new("-->"),
        new(@"\?>"),
        new(">"),
        new(@"\]\]>"),
    ];

    public static readonly Regex HtmlTag = new(@"\G" + HtmlTagPattern, RegexOptions.IgnoreCase);

    [GeneratedRegex(@"\\[!""#$%&'()*+,./:;<=>?@[\\\]^_`{|}~-]|&(?:#[xX][a-fA-F0-9]{1,6}|#[0-9]{1,7}|[a-zA-Z][a-zA-Z0-9]{1,31});")]
    private static partial Regex EntityOrEscapedCharRegex();

    [GeneratedRegex(@"\G&(?:#[xX][a-fA-F0-9]{1,6}|#[0-9]{1,7}|[a-zA-Z][a-zA-Z0-9]{1,31});")]
    public static partial Regex EntityRegex();

    [GeneratedRegex(@"[ \t\r\n]+")]
    private static partial Regex WhitespaceRunRegex();

    public static bool IsEscapable(char c) => c is >= '!' and <= '/' or >= ':' and <= '@' or >= '[' and <= '`' or >= '{' and <= '~';

    public static bool IsSpaceOrTab(char c) => c is ' ' or '\t';

    /// <summary>Unicode whitespace as CommonMark defines it (Zs, tab, line feed, form feed, carriage return).</summary>
    public static bool IsWhitespace(char c) => c is '\t' or '\n' or '\f' or '\r' || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.SpaceSeparator;

    /// <summary>Unicode punctuation and symbols (CommonMark 0.31).</summary>
    public static bool IsPunctuation(char c) => char.IsPunctuation(c) || char.IsSymbol(c);

    /// <summary>Resolves backslash escapes and entities.</summary>
    public static string Unescape(string text) =>
        text.IndexOfAny(['\\', '&']) < 0 ? text : EntityOrEscapedCharRegex().Replace(text, m => m.Value[0] == '\\' ? m.Value[1..] : DecodeEntity(m.Value));

    /// <summary>Decodes one entity; an unknown name stays as written.</summary>
    public static string DecodeEntity(string entity)
    {
        if (entity is ['&', '#', ..])
        {
            var hex = entity.Length > 2 && entity[2] is 'x' or 'X';
            var digits = entity[(hex ? 3 : 2)..^1];
            if (!int.TryParse(digits, hex ? System.Globalization.NumberStyles.HexNumber : System.Globalization.NumberStyles.None, null, out var code)
                || code == 0 || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
                return "\uFFFD";
            return char.ConvertFromUtf32(code);
        }
        var decoded = WebUtility.HtmlDecode(entity);
        return decoded;
    }

    /// <summary>Link label → key of the reference map: trimmed, inner whitespace collapsed, case folded.</summary>
    public static string NormalizeReference(string label)
    {
        var inner = label.Length >= 2 && label[0] == '[' && label[^1] == ']' ? label[1..^1] : label;
        return WhitespaceRunRegex().Replace(inner.Trim(), " ").ToLowerInvariant().ToUpperInvariant();
    }

    /// <summary>GitHub's heading anchors: lower case, spaces to dashes, punctuation removed.</summary>
    public static string Slug(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_')
                builder.Append(c);
            else if (c == ' ')
                builder.Append('-');
        }
        return builder.ToString();
    }
}
