using System.Text.RegularExpressions;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>
/// Guesses the diagram type from the content, as PlantUML does (it has no header for the type). The kinds without a
/// syntax of their own (communication, composite structure, interaction overview) are only recognized by the
/// <c>' rice:kind=…</c> comment.
/// </summary>
internal static partial class PlantUmlKindDetector
{
    public static UmlDiagramKind Detect(IReadOnlyList<PlantUmlLine> lines)
    {
        bool Any(Regex regex) => lines.Any(l => regex.IsMatch(l.Text));

        if (Any(TimingRegex()))
            return UmlDiagramKind.Timing;
        if (Any(ActivityRegex()))
            return UmlDiagramKind.Activity;
        if (Any(StateRegex()))
            return UmlDiagramKind.State;
        if (Any(ProfileRegex()))
            return UmlDiagramKind.Profile;
        if (Any(ClassRegex()))
            return UmlDiagramKind.Class;
        if (Any(ObjectRegex()))
            return UmlDiagramKind.Object;
        if (Any(ClassArrowRegex()))
            return UmlDiagramKind.Class;
        if (Any(UseCaseRegex()))
            return UmlDiagramKind.UseCase;
        if (Any(DeploymentRegex()))
            return UmlDiagramKind.Deployment;
        if (Any(ComponentRegex()))
            return UmlDiagramKind.Component;
        // "interface" alone: in component diagrams it is the lollipop, handled above.
        if (Any(InterfaceRegex()))
            return UmlDiagramKind.Class;

        if (Any(SequenceRegex()))
            return UmlDiagramKind.Sequence;
        if (Any(PackageRegex()))
            return UmlDiagramKind.Package;
        // PlantUML's default: "A -> B" is a sequence diagram.
        return UmlDiagramKind.Sequence;
    }

    [GeneratedRegex(@"^(robust|concise|clock|binary)\s|^@-?\d|^@[\w""]+$", RegexOptions.IgnoreCase)]
    private static partial Regex TimingRegex();

    // :action; (but not the :actor: shorthand), start/stop, if (…) then, while, repeat, fork, |swimlane|
    [GeneratedRegex(@"^:(?![^:;]+:(\s|$|-|\.))|^(start|stop|endif|endwhile|end\s*fork|fork\s+again|repeat|detach|kill)$|^(else)?if\s*\(.*\)\s*then|^while\s*\(|^repeat\s+while\b|^\|[^|]+\|", RegexOptions.IgnoreCase)]
    private static partial Regex ActivityRegex();

    [GeneratedRegex(@"\[\*\]|^state\s+[^=\s]|\[H\*?\]", RegexOptions.IgnoreCase)]
    private static partial Regex StateRegex();

    [GeneratedRegex(@"^(metaclass|stereotype)\s|<<\s*metaclass\s*>>", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileRegex();

    [GeneratedRegex(@"^(abstract\s+class|abstract|class|enum|annotation|struct|exception|protocol|dataclass|record)\s", RegexOptions.IgnoreCase)]
    private static partial Regex ClassRegex();

    // The arrows only class (and object) diagrams use: <|--, --|>, *--, o--.
    [GeneratedRegex(@"<\|[-.]|[-.]\|>|(^|\s)[*o][-.]{2}|[-.]{2}[*o](\s|$)")]
    private static partial Regex ClassArrowRegex();

    [GeneratedRegex(@"^interface\s", RegexOptions.IgnoreCase)]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"^(participant|activate|deactivate|destroy|autonumber|alt|opt|loop|par|break|critical|ref\s+over|return)\b|^==.*==$|^\.\.\.|^\|\|\d*\|\|$|-+>>|^\[[-o]|[-.]\]$|^(note\s+over)\b|^(collections|queue)\s", RegexOptions.IgnoreCase)]
    private static partial Regex SequenceRegex();

    [GeneratedRegex(@"^usecase\s|^\(.+\)|[-.>]\s*\(.+\)\s*(:.*)?$|^:[^:]+:(\s|$|-|\.)", RegexOptions.IgnoreCase)]
    private static partial Regex UseCaseRegex();

    [GeneratedRegex(@"^(node|artifact|device|storage|stack|card|agent|file)\s", RegexOptions.IgnoreCase)]
    private static partial Regex DeploymentRegex();

    [GeneratedRegex(@"^(component|port|portin|portout)\s|^\[[^\]*]+\]|^\(\)\s", RegexOptions.IgnoreCase)]
    private static partial Regex ComponentRegex();

    [GeneratedRegex(@"^(object|map)\s", RegexOptions.IgnoreCase)]
    private static partial Regex ObjectRegex();

    [GeneratedRegex(@"^(package|namespace|folder|frame|cloud|rectangle)\s", RegexOptions.IgnoreCase)]
    private static partial Regex PackageRegex();
}
