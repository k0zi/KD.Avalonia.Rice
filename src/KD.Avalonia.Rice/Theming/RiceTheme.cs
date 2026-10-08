namespace KD.Avalonia.Rice.Theming;

/// <summary>A named color theme with a light and a dark variant.</summary>
public sealed record RiceTheme(string Id, string DisplayName, RicePalette Light, RicePalette Dark)
{
    public override string ToString() => DisplayName;
}
