using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Reads PlantUML text into the diagram model. Never throws on bad input: unknown or invalid lines are reported in
/// <see cref="UmlDocument.Diagnostics"/> and skipped.
/// </summary>
public static partial class PlantUmlParser
{
    public static UmlDocument Parse(string text)
    {
        var document = new UmlDocument();
        foreach (var block in SplitBlocks(text ?? "", document.Diagnostics))
        {
            var kind = block.KindHint ?? PlantUmlKindDetector.Detect(block.Lines);
            UmlDiagram diagram;
            try
            {
                diagram = kind switch
                {
                    UmlDiagramKind.Sequence => new SequenceDiagramParser(document.Diagnostics).Parse(block),
                    UmlDiagramKind.Activity or UmlDiagramKind.InteractionOverview =>
                        new ActivityDiagramParser(document.Diagnostics, kind).Parse(block),
                    UmlDiagramKind.Timing => new TimingDiagramParser(document.Diagnostics).Parse(block),
                    _ => new GraphDiagramParser(document.Diagnostics, kind).Parse(block),
                };
            }
            catch (Exception ex)
            {
                // A parser bug must not take the application down; the block is reported and skipped.
                document.Diagnostics.Add(new UmlDiagnostic(block.StartLine, UmlDiagnosticSeverity.Error,
                    $"The diagram could not be read: {ex.Message}"));
                continue;
            }
            diagram.Name = block.Name;
            document.Diagrams.Add(diagram);
        }
        return document;
    }

    /// <summary>Splits the text into blocks, removes comments and blank lines and trims the rest.</summary>
    internal static List<PlantUmlBlock> SplitBlocks(string text, List<UmlDiagnostic> diagnostics)
    {
        var blocks = new List<PlantUmlBlock>();
        var rawLines = text.Replace("\r\n", "\n").Split('\n');
        var hasStart = rawLines.Any(l => StartRegex().IsMatch(l.Trim()));

        PlantUmlBlock? current = hasStart ? null : new PlantUmlBlock(1, null);
        var skipping = false;
        var inBlockComment = false;
        for (var i = 0; i < rawLines.Length; i++)
        {
            var number = i + 1;
            var line = rawLines[i].Trim();

            if (inBlockComment || line.Contains("/'"))
            {
                line = StripBlockComment(line, ref inBlockComment);
                if (line.Length == 0)
                    continue;
            }

            if (line.Length == 0)
                continue;

            var start = StartRegex().Match(line);
            if (start.Success)
            {
                if (current is not null && hasStart)
                    diagnostics.Add(new UmlDiagnostic(number, UmlDiagnosticSeverity.Warning, "@enduml is missing before @startuml."));
                current = new PlantUmlBlock(number, start.Groups[1].Success ? start.Groups[1].Value.Trim() : null);
                blocks.Add(current);
                continue;
            }

            var other = OtherStartRegex().Match(line);
            if (other.Success)
            {
                diagnostics.Add(new UmlDiagnostic(number, UmlDiagnosticSeverity.Error,
                    $"@start{other.Groups[1].Value} is not a UML diagram and is not supported."));
                skipping = true;
                continue;
            }

            if (EndRegex().IsMatch(line))
            {
                if (skipping)
                    skipping = false;
                else if (current is null)
                    diagnostics.Add(new UmlDiagnostic(number, UmlDiagnosticSeverity.Warning, "@enduml without @startuml."));
                current = hasStart ? null : current;
                continue;
            }

            if (skipping)
                continue;

            if (line.StartsWith('\''))
            {
                var hint = KindHintRegex().Match(line);
                if (hint.Success && current is not null)
                {
                    if (Enum.TryParse<UmlDiagramKind>(hint.Groups[1].Value, true, out var kind))
                        current.KindHint = kind;
                    else
                        diagnostics.Add(new UmlDiagnostic(number, UmlDiagnosticSeverity.Warning,
                            $"Unknown diagram kind '{hint.Groups[1].Value}'."));
                }
                continue;
            }

            if (current is null)
            {
                diagnostics.Add(new UmlDiagnostic(number, UmlDiagnosticSeverity.Warning, "Text outside @startuml … @enduml was ignored."));
                continue;
            }
            current.Lines.Add(new PlantUmlLine(number, line));
        }

        if (current is not null && hasStart && blocks.Count > 0 && blocks[^1] == current)
            diagnostics.Add(new UmlDiagnostic(rawLines.Length, UmlDiagnosticSeverity.Warning, "@enduml is missing."));
        if (!hasStart && current is { Lines.Count: > 0 })
            blocks.Add(current);
        return blocks;
    }

    private static string StripBlockComment(string line, ref bool inComment)
    {
        var result = new System.Text.StringBuilder();
        var i = 0;
        while (i < line.Length)
        {
            if (inComment)
            {
                var end = line.IndexOf("'/", i, StringComparison.Ordinal);
                if (end < 0)
                    return result.ToString().Trim();
                inComment = false;
                i = end + 2;
            }
            else
            {
                var start = line.IndexOf("/'", i, StringComparison.Ordinal);
                if (start < 0)
                {
                    result.Append(line, i, line.Length - i);
                    break;
                }
                result.Append(line, i, start - i);
                inComment = true;
                i = start + 2;
            }
        }
        return result.ToString().Trim();
    }

    [GeneratedRegex(@"^@startuml(?:\s+(.*))?$", RegexOptions.IgnoreCase)]
    private static partial Regex StartRegex();

    [GeneratedRegex(@"^@start(?!uml)(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex OtherStartRegex();

    [GeneratedRegex(@"^@end\w+$", RegexOptions.IgnoreCase)]
    private static partial Regex EndRegex();

    [GeneratedRegex(@"^'\s*rice:kind\s*=\s*(\w+)", RegexOptions.IgnoreCase)]
    private static partial Regex KindHintRegex();
}
