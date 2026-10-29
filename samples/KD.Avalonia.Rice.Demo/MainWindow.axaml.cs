using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Notifications;
using KD.Avalonia.Rice.Theming;
using Material.Icons;

namespace KD.Avalonia.Rice.Demo;

public partial class MainWindow : RiceWindow
{
    // Pages are created on first use and kept, so their state (e.g. the chat) survives navigation.
    private readonly Dictionary<object, Control> _pages = [];

    public MainWindow()
    {
        InitializeComponent();
        SideMenu.SelectionChanged += (_, _) =>
        {
            if (SideMenu.SelectedItem is RiceSideMenuItem item)
                ShowPage(item);
        };
        ShowPage(HomeItem);
        AddPaletteCommands();
    }

    // Ctrl+Shift+P lists these and the side menu items; the gestures work without opening the palette too.
    // "?" lists the modes: ">" commands, "@" pages (built in) and "=" the calculator below.
    private void AddPaletteCommands()
    {
        Commands.Add(new RiceCommandItem(RiceStrings.ToggleTheme, ThemeManager.Instance.ToggleVariant)
        {
            Icon = MaterialIconKind.LightbulbOutline,
            Category = "View",
            Keywords = "dark light mode variant",
            Gesture = new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Shift),
        });
        Commands.Add(new RiceCommandItem(RiceStrings.ThemeSettings, () => _ = ShowThemeDialog())
        {
            Icon = MaterialIconKind.Palette,
            Category = "View",
            Keywords = "color ubuntu fedora nord dracula",
            Gesture = new KeyGesture(Key.OemComma, KeyModifiers.Control),
        });
        Commands.Add(new RiceCommandItem("Collapse / expand the menu", SideMenu.ToggleCollapsed)
        {
            Icon = MaterialIconKind.MenuOpen,
            Category = "View",
            Gesture = new KeyGesture(Key.B, KeyModifiers.Control),
        });
        Commands.Add(new RiceCommandItem("Say hello", () => RiceToast.ShowSuccess("Hello from the command palette!"))
        {
            Icon = MaterialIconKind.HandWave,
            Description = "Shows a toast",
            Category = "Demo",
        });
        Commands.Add(new RiceCommandItem(RiceStrings.About, RequestAbout)
        {
            Icon = MaterialIconKind.InformationOutline,
            Category = "Help",
            Gesture = new KeyGesture(Key.F1),
        });

        // A dynamic mode: the entry is computed from the text after the prefix.
        CommandProviders.Add(new RiceCommandProvider
        {
            Prefix = "=",
            Title = "Calculator",
            Icon = MaterialIconKind.Calculator,
            FilterResults = false,
            GetCommands = Calculate,
        });
    }

    private static IEnumerable<RiceCommandItem> Calculate(string expression)
    {
        if (expression.Length == 0)
            yield break;
        string result;
        try
        {
            result = Convert.ToString(new System.Data.DataTable().Compute(expression, null), CultureInfo.InvariantCulture) ?? "";
        }
        catch (Exception e) when (e is System.Data.EvaluateException or System.Data.SyntaxErrorException or FormatException
                                      or OverflowException or InvalidCastException or IndexOutOfRangeException)
        {
            yield break;
        }
        yield return new RiceCommandItem($"{expression} = {result}", () => RiceToast.ShowInfo(result, "Result"))
        {
            Icon = MaterialIconKind.EqualBox,
            Description = "Enter shows the result as a toast",
        };
    }

    // Footer items are not selectable, so the menu selection is cleared while the settings page is shown.
    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        SideMenu.SelectedItem = null;
        ShowPage(nameof(SettingsPage));
    }

    private void OnAboutClicked(object? sender, RoutedEventArgs e) => RequestAbout();

    private void ShowPage(object key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                _ when key == CardItem => new CardPage(),
                _ when key == GroupBoxItem => new GroupBoxPage(),
                _ when key == ChatItem => new ChatPage(),
                _ when key == ProgressItem => new ProgressPage(),
                _ when key == UmlItem => new UmlPage(),
                _ when key == MarkdownItem => new MarkdownPage(),
                _ when key == ChartsItem => new ChartsPage(),
                _ when key == DialogsItem => new DialogsPage(),
                _ when key == ToastsItem => new ToastsPage(),
                _ when key == FeedbackItem => new FeedbackPage(),
                _ when key == NavigationItem => new NavigationPage(),
                _ when key == RibbonItem => new RibbonPage(),
                _ when key == PropertyGridItem => new PropertyGridPage(),
                _ when key == CommandPaletteItem => new CommandPalettePage(),
                _ when key == TimelineItem => new TimelinePage(),
                _ when key == KanbanItem => new KanbanPage(),
                _ when key == CodeEditorItem => new CodeEditorPage(),
                _ when key == DockLayoutItem => new DockLayoutPage(),
                nameof(SettingsPage) => new SettingsPage(),
                _ => new HomePage(),
            };
            _pages[key] = page;
        }
        PageHost.Content = page;
    }
}
