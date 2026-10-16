using System.Text;

namespace KD.Avalonia.Rice.Controls.Uml;

public enum UmlDiagnosticSeverity
{
    Info,
    Warning,
    Error,
}

/// <summary>A problem found while reading PlantUML text; <see cref="Line"/> is 1-based.</summary>
public sealed record UmlDiagnostic(int Line, UmlDiagnosticSeverity Severity, string Message)
{
    public override string ToString() => $"{Line}: {Severity}: {Message}";
}

/// <summary>
/// A PlantUML file (<c>.puml</c>): one or more <c>@startuml</c> … <c>@enduml</c> blocks, each one a diagram.
/// Reading never throws on bad input, the problems are listed in <see cref="Diagnostics"/>.
/// </summary>
public sealed class UmlDocument
{
    public List<UmlDiagram> Diagrams { get; } = [];

    public List<UmlDiagnostic> Diagnostics { get; } = [];

    public bool HasErrors => Diagnostics.Any(d => d.Severity == UmlDiagnosticSeverity.Error);

    public static UmlDocument Parse(string text) => PlantUmlParser.Parse(text);

    public static UmlDocument Load(string path) => Parse(File.ReadAllText(path));

    public static async Task<UmlDocument> LoadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        return Parse(await reader.ReadToEndAsync(cancellationToken));
    }

    public string ToPlantUml() => string.Join("\n", Diagrams.Select(PlantUmlWriter.Write));

    public void Save(string path) => File.WriteAllText(path, ToPlantUml(), new UTF8Encoding(false));

    public async Task SaveAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteAsync(ToPlantUml().AsMemory(), cancellationToken);
    }
}
