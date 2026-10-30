using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using KD.Avalonia.Rice;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Demo;
using KD.Avalonia.Rice.Dialogs;
using KD.Avalonia.Rice.Notifications;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Screenshots;

/// <summary>
/// Opens the demo window headless, selects every page of the side menu and saves it as a PNG.
/// Usage: dotnet run --project tools/KD.Avalonia.Rice.Screenshots [-- output folder] (default: docs/images).
/// </summary>
internal static class Program
{
    // Side menu item name -> file name.
    private static readonly (string Item, string File)[] Pages =
    [
        ("HomeItem", "home"),
        ("CardItem", "card"),
        ("GroupBoxItem", "group-box"),
        ("ChatItem", "chat"),
        ("ProgressItem", "progress"),
        ("UmlItem", "uml"),
        ("MarkdownItem", "markdown"),
        ("ChartsItem", "charts"),
        ("DialogsItem", "dialogs"),
        ("ToastsItem", "toasts"),
        ("FeedbackItem", "feedback"),
        ("NavigationItem", "navigation"),
        ("RibbonItem", "ribbon"),
        ("PropertyGridItem", "property-grid"),
        ("CommandPaletteItem", "command-palette"),
        ("TimelineItem", "timeline"),
        ("KanbanItem", "kanban"),
        ("CodeEditorItem", "code-editor"),
        ("DockLayoutItem", "dock-layout"),
        ("SchedulerItem", "scheduler"),
        ("DiffItem", "diff"),
    ];

    // Themes shown side by side on the home page, light and dark.
    private static readonly RiceTheme[] GalleryThemes =
        [BuiltInThemes.Ubuntu, BuiltInThemes.Fedora, BuiltInThemes.Nord, BuiltInThemes.Dracula,
         BuiltInThemes.Gruvbox, BuiltInThemes.Catppuccin, BuiltInThemes.Windows11];

    public static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(FindRepoRoot(), "docs", "images"));
        Directory.CreateDirectory(output);

        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .UseRice(options =>
            {
                // Own app id, so the theme saved by the demo is not used (and not overwritten).
                options.AppInfo = new RiceAppInfo { AppId = "kd-rice-screenshots", Name = "Rice Demo", Author = "k0zi" };
                options.SupportedThemes = BuiltInThemes.All;
                options.DefaultThemeId = BuiltInThemes.Ubuntu.Id;
            })
            .SetupWithoutStarting();

        var app = Application.Current!;
        ThemeManager.Instance.SetTheme(BuiltInThemes.Ubuntu);

        var window = Open("HomeItem");
        var menu = window.FindControl<RiceSideMenu>("SideMenu")!;
        foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
        {
            app.RequestedThemeVariant = variant;
            var suffix = variant == ThemeVariant.Dark ? "-dark" : "";
            foreach (var (item, file) in Pages)
            {
                menu.SelectedItem = window.FindControl<RiceSideMenuItem>(item);
                Settle();
                Save(window, Path.Combine(output, file + suffix + ".png"));
            }
            CaptureOverlays(output, suffix);
        }

        menu.SelectedItem = window.FindControl<RiceSideMenuItem>("HomeItem");
        foreach (var theme in GalleryThemes)
        {
            ThemeManager.Instance.SetTheme(theme);
            foreach (var variant in new[] { ThemeVariant.Light, ThemeVariant.Dark })
            {
                app.RequestedThemeVariant = variant;
                Settle();
                Save(window, Path.Combine(output, $"theme-{theme.Id}-{(variant == ThemeVariant.Dark ? "dark" : "light")}.png"));
            }
        }

        window.Close();
        return 0;
    }

    // Things that only show up on a click: toasts, the command palette and a dialog. Each gets a fresh window.
    private static void CaptureOverlays(string output, string suffix)
    {
        var window = Open("ToastsItem");
        RiceToast.ShowInfo("3 files were moved to the trash.", "Deleted", new ToastAction("Undo", () => { }), window);
        RiceToast.ShowSuccess("report.md was saved.", owner: window);
        RiceToast.ShowWarning("Less than 1 GB is free on /home.", "Low disk space", owner: window);
        RiceToast.ShowError("The server did not respond.", "Upload failed", owner: window);
        Settle();
        Save(window, Path.Combine(output, "toasts-shown" + suffix + ".png"));
        window.Close();

        window = Open("CommandPaletteItem");
        window.ShowCommandPalette();
        Settle();
        Save(window, Path.Combine(output, "command-palette-open" + suffix + ".png"));
        window.Close();

        window = Open("DialogsItem");
        _ = RiceDialog.ShowAsync("Delete the selected files? They are moved to the trash.", DialogIcon.Question,
            DialogButtons.YesNoCancel, "Delete files", window);
        Settle();
        var dialog = window.OwnedWindows.FirstOrDefault() ?? throw new InvalidOperationException("The dialog did not open.");
        Save(dialog, Path.Combine(output, "dialog" + suffix + ".png"));
        dialog.Close();
        window.Close();
    }

    private static MainWindow Open(string item)
    {
        var window = new MainWindow { Width = 1200, Height = 760 };
        window.Show();
        window.FindControl<RiceSideMenu>("SideMenu")!.SelectedItem = window.FindControl<RiceSideMenuItem>(item);
        Settle();
        return window;
    }

    // Lets layout, bindings and load animations finish (the render timer drives the animations).
    private static void Settle()
    {
        var end = DateTime.UtcNow.AddSeconds(1.5);
        while (DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(16);
        }
        Dispatcher.UIThread.RunJobs();
    }

    private static void Save(Window window, string path)
    {
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame was rendered.");
        frame.Save(path, PngBitmapEncoderOptions.Default);
        Console.WriteLine(path);
    }

    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "KD.Avalonia.Rice.slnx")))
                return dir.FullName;
        return Directory.GetCurrentDirectory();
    }
}
