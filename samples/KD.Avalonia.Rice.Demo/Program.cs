using Avalonia;
using KD.Avalonia.Rice;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Demo;

internal static class Program
{
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
                    AppId = "kd-rice-demo",
                    Name = "Rice Demo",
                    Author = "k0zi",
                    Description = "Frameless Avalonia window with distro themes.",
                    IconUri = "avares://KD.Avalonia.Rice.Demo/Assets/icon.png",
                };
                options.SupportedThemes = BuiltInThemes.All;
                options.DefaultThemeId = BuiltInThemes.Ubuntu.Id;
            });
}
