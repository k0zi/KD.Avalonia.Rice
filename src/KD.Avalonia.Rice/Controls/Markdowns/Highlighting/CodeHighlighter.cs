using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Markdowns;

public enum CodeTokenKind
{
    Keyword,
    Type,
    String,
    Number,
    Comment,
    Operator,
    Tag,
    Attribute,
}

/// <summary>A colored part of a code block; the text between tokens is plain.</summary>
public readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind);

/// <summary>
/// Small, regular expression based syntax highlighter for the common languages of code blocks. It does not parse
/// the code, it only finds comments, strings, numbers, keywords, types and tags, which is enough for coloring.
/// </summary>
public static class CodeHighlighter
{
    private sealed record Language(Regex Pattern, CodeTokenKind[] Kinds);

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cs"] = "csharp", ["c#"] = "csharp", ["csharp"] = "csharp",
        ["xml"] = "xml", ["xaml"] = "xml", ["axaml"] = "xml", ["html"] = "xml", ["htm"] = "xml", ["svg"] = "xml",
        ["csproj"] = "xml", ["props"] = "xml", ["targets"] = "xml", ["xhtml"] = "xml",
        ["json"] = "json", ["jsonc"] = "json", ["json5"] = "json", ["chart"] = "json",
        ["js"] = "javascript", ["javascript"] = "javascript", ["jsx"] = "javascript", ["mjs"] = "javascript",
        ["ts"] = "javascript", ["typescript"] = "javascript", ["tsx"] = "javascript",
        ["py"] = "python", ["python"] = "python",
        ["sh"] = "bash", ["bash"] = "bash", ["shell"] = "bash", ["zsh"] = "bash", ["console"] = "bash", ["shellsession"] = "bash",
        ["sql"] = "sql", ["mysql"] = "sql", ["pgsql"] = "sql", ["postgresql"] = "sql", ["sqlite"] = "sql", ["tsql"] = "sql",
        ["yaml"] = "yaml", ["yml"] = "yaml",
        ["plantuml"] = "plantuml", ["puml"] = "plantuml", ["uml"] = "plantuml",
        ["mermaid"] = "mermaid", ["mmd"] = "mermaid",
    };

    private static readonly Dictionary<string, Language> Languages = new()
    {
        ["csharp"] = Build(
            (CodeTokenKind.Comment, @"//[^\n]*|/\*[\s\S]*?\*/"),
            (CodeTokenKind.String, "\"\"\"[\\s\\S]*?\"\"\"|\\$?@\"(?:\"\"|[^\"])*\"|@?\\$?\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\\\n])+'"),
            (CodeTokenKind.Keyword, @"^[ \t]*#[a-z]+[^\n]*"),
            (CodeTokenKind.Number, @"\b(?:0[xX][0-9a-fA-F_]+|0[bB][01_]+|\d[\d_]*(?:\.\d[\d_]*)?(?:[eE][+-]?\d+)?)[fFdDmMuUlL]*\b"),
            (CodeTokenKind.Keyword, Words("abstract as async await base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach get goto if implicit in init int interface internal is lock long namespace new null object operator out override params partial private protected public readonly record ref required return sbyte sealed set short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using var virtual void volatile when where while with yield nint nuint dynamic file scoped global value and or not")),
            (CodeTokenKind.Type, @"\b[A-Z][A-Za-z0-9_]*\b")),
        ["javascript"] = Build(
            (CodeTokenKind.Comment, @"//[^\n]*|/\*[\s\S]*?\*/"),
            (CodeTokenKind.String, "`(?:\\\\[\\s\\S]|[^`\\\\])*`|\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\\\n])*'"),
            (CodeTokenKind.Number, @"\b(?:0[xX][0-9a-fA-F_]+|\d[\d_]*(?:\.\d+)?(?:[eE][+-]?\d+)?n?)\b"),
            (CodeTokenKind.Keyword, Words("abstract any as async await boolean break case catch class const constructor continue debugger declare default delete do else enum export extends false finally for from function get if implements import in instanceof interface keyof let module namespace new null number of private protected public readonly return set static string super switch this throw true try type typeof undefined var void while with yield")),
            (CodeTokenKind.Type, @"\b[A-Z][A-Za-z0-9_$]*\b")),
        ["python"] = Build(
            (CodeTokenKind.Comment, @"#[^\n]*"),
            (CodeTokenKind.String, "(?:[rRbBuUfF]{1,2})?(?:\"\"\"[\\s\\S]*?\"\"\"|'''[\\s\\S]*?'''|\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:\\\\.|[^'\\\\\\n])*')"),
            (CodeTokenKind.Attribute, @"^[ \t]*@[\w.]+"),
            (CodeTokenKind.Number, @"\b(?:0[xX][0-9a-fA-F_]+|\d[\d_]*(?:\.\d+)?(?:[eE][+-]?\d+)?j?)\b"),
            (CodeTokenKind.Keyword, Words("and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return True try while with yield match case self")),
            (CodeTokenKind.Type, @"\b(?:[A-Z][A-Za-z0-9_]*|int|str|float|bool|list|dict|set|tuple|bytes|object|print|len|range)\b")),
        ["bash"] = Build(
            (CodeTokenKind.Comment, @"(?<![^\s])#[^\n]*"),
            (CodeTokenKind.String, "\"(?:\\\\.|[^\"\\\\])*\"|'[^']*'"),
            (CodeTokenKind.Attribute, @"\$(?:\{[^}\n]*\}|\w+|[@#?$!*0-9])"),
            (CodeTokenKind.Keyword, Words("if then else elif fi for in do done while until case esac function return local export readonly declare unset shift exit break continue source alias sudo echo cd")),
            (CodeTokenKind.Operator, @"(?<=\s)--?[A-Za-z][\w-]*|&&|\|\||[|><]"),
            (CodeTokenKind.Number, @"\b\d+\b")),
        ["sql"] = Build(
            (CodeTokenKind.Comment, @"--[^\n]*|/\*[\s\S]*?\*/"),
            (CodeTokenKind.String, "'(?:''|[^'])*'"),
            (CodeTokenKind.Number, @"\b\d+(?:\.\d+)?\b"),
            (CodeTokenKind.Keyword, "(?i)" + Words("select from where and or not insert into values update set delete create table view index drop alter add column primary key foreign references join inner left right outer full cross on as group by order having limit offset distinct union all exists in is null like between case when then else end begin commit rollback transaction default unique check constraint with returning asc desc count sum avg min max cascade if")),
            (CodeTokenKind.Type, "(?i)" + Words("int integer bigint smallint tinyint decimal numeric float real double varchar nvarchar char nchar text date time datetime timestamp boolean bool blob uuid serial json jsonb"))),
        ["json"] = Build(
            (CodeTokenKind.Comment, @"//[^\n]*|/\*[\s\S]*?\*/"),
            (CodeTokenKind.Attribute, "\"(?:\\\\.|[^\"\\\\\\n])*\"(?=\\s*:)"),
            (CodeTokenKind.String, "\"(?:\\\\.|[^\"\\\\\\n])*\""),
            (CodeTokenKind.Number, @"-?\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b"),
            (CodeTokenKind.Keyword, Words("true false null"))),
        ["yaml"] = Build(
            (CodeTokenKind.Comment, @"(?<![^\s])#[^\n]*"),
            (CodeTokenKind.Attribute, @"^[ \t]*(?:- )?[\w.$/-]+(?=[ \t]*:(?:\s|$))"),
            (CodeTokenKind.String, "\"(?:\\\\.|[^\"\\\\\\n])*\"|'(?:''|[^'\\n])*'"),
            (CodeTokenKind.Number, @"(?<=[:\-][ \t]*)-?\b\d+(?:\.\d+)?\b"),
            (CodeTokenKind.Keyword, Words("true false null yes no on off"))),
        ["xml"] = Build(
            (CodeTokenKind.Comment, @"<!--[\s\S]*?-->"),
            (CodeTokenKind.String, @"<!\[CDATA\[[\s\S]*?\]\]>"),
            (CodeTokenKind.Tag, @"</?[A-Za-z_][\w:.-]*|/?>|<\?[\w:-]+|\?>"),
            (CodeTokenKind.Attribute, @"\b[A-Za-z_][\w:.-]*(?=\s*=\s*[""'])"),
            (CodeTokenKind.String, "\"[^\"]*\"|'[^']*'"),
            (CodeTokenKind.Keyword, @"&[#\w]+;")),
        ["plantuml"] = Build(
            (CodeTokenKind.Comment, @"(?m)^[ \t]*'[^\n]*|/'[\s\S]*?'/"),
            (CodeTokenKind.String, "\"[^\"\\n]*\""),
            (CodeTokenKind.Keyword, @"@(?:start|end)\w+|\b(?:" + string.Join("|", "abstract actor agent alt annotation artifact as boundary break card class cloud collections component control critical database deactivate activate destroy end else entity enum exception folder frame group hexagon if then endif interface label loop map metaclass namespace node note object opt over package par participant port portin portout queue rectangle ref repeat return stack start state stop storage title usecase while endwhile fork split detach kill robust concise clock binary left right top bottom of".Split(' ')) + @")\b"),
            (CodeTokenKind.Attribute, @"<<[^>\n]*>>"),
            (CodeTokenKind.Operator, @"[-.=]+(?:\[[^\]\n]*\])?[-.=]*[>|*o]+|[<|*o]+[-.=]+[>|*o]*|-+>|<-+")),
        ["mermaid"] = Build(
            (CodeTokenKind.Comment, @"%%[^\n]*"),
            (CodeTokenKind.String, "\"[^\"\\n]*\""),
            (CodeTokenKind.Keyword, @"\b(?:" + string.Join("|", "flowchart graph sequenceDiagram classDiagram stateDiagram stateDiagram-v2 erDiagram gantt pie mindmap journey gitGraph timeline subgraph end direction participant actor activate deactivate note loop alt else opt par and critical break rect autonumber class state section title dateFormat axisFormat excludes showData root TD TB BT LR RL as of left right over".Split(' ')) + @")\b"),
            (CodeTokenKind.Attribute, @"<<[^>\n]*>>|:::\w+"),
            (CodeTokenKind.Operator, @"[<ox]?[-.=]{2,}[>ox]?|[|}][|o]--?[|o][|{]|-{1,2}>>|-[x)]|\|[^|\n]*\|")),
    };

    private static string Words(string words) => @"\b(?:" + string.Join("|", words.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Regex.Escape)) + @")\b";

    private static Language Build(params (CodeTokenKind Kind, string Pattern)[] rules)
    {
        // One alternation, so the earliest match wins and comments hide the keywords inside them.
        var pattern = string.Join("|", rules.Select((r, i) => $"(?<g{i}>{r.Pattern})"));
        return new Language(new Regex(pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant), rules.Select(r => r.Kind).ToArray());
    }

    /// <summary>Whether <paramref name="language"/> (as in the info string: <c>cs</c>, <c>xaml</c>, <c>bash</c>, …) is colored.</summary>
    public static bool IsSupported(string? language) => language is not null && Aliases.ContainsKey(language);

    /// <summary>The language an alias stands for (<c>cs</c> → <c>csharp</c>); null when it is not supported.</summary>
    internal static string? LanguageName(string? language) =>
        language is not null && Aliases.TryGetValue(language, out var name) ? name : null;

    /// <summary>The colored parts of <paramref name="code"/>, in order and not overlapping.</summary>
    public static IReadOnlyList<CodeToken> Highlight(string code, string? language)
    {
        if (string.IsNullOrEmpty(code) || language is null || !Aliases.TryGetValue(language, out var name))
            return [];
        var lang = Languages[name];
        var tokens = new List<CodeToken>();
        foreach (Match m in lang.Pattern.Matches(code))
        {
            if (m.Length == 0)
                continue;
            for (var i = 0; i < lang.Kinds.Length; i++)
            {
                if (!m.Groups[$"g{i}"].Success)
                    continue;
                tokens.Add(new CodeToken(m.Index, m.Length, lang.Kinds[i]));
                break;
            }
        }
        return tokens;
    }
}
