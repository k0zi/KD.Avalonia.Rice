using Avalonia;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice;

public static class AppBuilderExtensions
{
    /// <summary>Configures app info and supported themes, and restores the saved theme.</summary>
    public static AppBuilder UseRice(this AppBuilder builder, Action<RiceOptions>? configure = null)
    {
        var options = new RiceOptions();
        configure?.Invoke(options);
        ThemeManager.Instance.Configure(options);
        return builder;
    }
}
