using Avalonia;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

internal static class Program
{
    /// <summary>
    /// The only supported theme is the one being edited. MainWindow swaps it in this list and then calls
    /// <c>SetTheme</c>, so the whole editor previews the edits. With a single theme nothing is persisted.
    /// </summary>
    public static List<RiceTheme> LiveThemes { get; } = [BuiltInThemes.All[0]];

    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseRice(options =>
            {
                options.AppInfo = new RiceAppInfo
                {
                    AppId = ThemeStore.AppId,
                    Name = "Rice Theme Editor",
                    Author = "k0zi",
                    Description = "Edit, preview and save KD.Avalonia.Rice themes.",
                };
                options.SupportedThemes = LiveThemes;
            });
}
