using System.Reflection;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace KD.Avalonia.Rice.Options;

/// <summary>Application metadata shown in the title bar and the about dialog.</summary>
public sealed class RiceAppInfo
{
    private Bitmap? _icon;
    private bool _iconLoaded;

    /// <summary>Identifier used as the settings folder name (e.g. <c>~/.config/&lt;AppId&gt;</c>).</summary>
    public required string AppId { get; init; }

    public required string Name { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    /// <summary>Defaults to the entry assembly's informational version.</summary>
    public string Version { get; init; } = GetEntryVersion();

    /// <summary>Icon asset, e.g. <c>avares://MyApp/Assets/icon.png</c>.</summary>
    public string? IconUri { get; init; }

    public Bitmap? Icon
    {
        get
        {
            if (!_iconLoaded && IconUri is not null)
            {
                _iconLoaded = true;
                using var stream = AssetLoader.Open(new Uri(IconUri));
                _icon = new Bitmap(stream);
            }
            return _icon;
        }
    }

    internal static RiceAppInfo Default { get; } = new()
    {
        AppId = Assembly.GetEntryAssembly()?.GetName().Name ?? "avalonia-app",
        Name = Assembly.GetEntryAssembly()?.GetName().Name ?? "Avalonia App",
    };

    private static string GetEntryVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly?.GetName().Version?.ToString()
                      ?? "1.0.0";
        var metadataStart = version.IndexOf('+');
        return metadataStart >= 0 ? version[..metadataStart] : version;
    }
}
