using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

/// <summary>A theme in the editor's list: a built-in one to start from, or one saved by the editor.</summary>
public sealed record ThemeEntry(RiceTheme Theme, bool IsBuiltIn)
{
    public string DisplayName => Theme.DisplayName;

    public string Kind => IsBuiltIn ? "Built-in" : "Saved";

    public RicePalette Light => Theme.Light;

    public RicePalette Dark => Theme.Dark;
}
