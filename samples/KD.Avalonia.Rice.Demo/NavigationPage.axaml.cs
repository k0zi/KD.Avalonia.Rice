using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using KD.Avalonia.Rice.Controls;
using Material.Icons;

namespace KD.Avalonia.Rice.Demo;

public partial class NavigationPage : UserControl
{
    private static readonly string[] DeeperLevels = ["Fonts", "Monospace", "Size", "Fine tuning"];

    // Tabs from data: closing removes the item from the collection, dragging moves it.
    private readonly ObservableCollection<RiceTabViewItem> _tabs = [];
    private int _tabCounter;

    public NavigationPage()
    {
        InitializeComponent();

        Breadcrumb.ItemClicked += (_, e) => BreadcrumbResult.Text = $"Back to “{(e.Item as RiceBreadcrumbItem)?.Content}” (level {e.Index + 1}).";
        AddLevelButton.Click += (_, _) =>
        {
            var level = DeeperLevels[(Breadcrumb.ItemCount - 1) % DeeperLevels.Length];
            Breadcrumb.Items.Add(new RiceBreadcrumbItem { Content = level });
        };

        ViewSwitch.SelectionChanged += (_, _) =>
            SegmentResult.Text = $"Selected: {(ViewSwitch.SelectedItem as RiceSegmentedItem)?.Content}";
        SegmentResult.Text = "Selected: Day";

        _tabs.Add(CreateTab("Welcome", MaterialIconKind.HomeOutline, "This tab has IsClosable=\"False\".", closable: false));
        _tabs.Add(CreateTab("README.md", MaterialIconKind.LanguageMarkdownOutline, "# KD.Avalonia.Rice"));
        _tabs.Add(CreateTab("App.axaml", MaterialIconKind.Xml, "<Application …>"));
        Tabs.ItemsSource = _tabs;
        Tabs.SelectedIndex = 0;
        Tabs.AddTabRequested += (_, _) =>
        {
            _tabs.Add(CreateTab($"Untitled {++_tabCounter}", MaterialIconKind.FileOutline, "Empty document"));
            Tabs.SelectedIndex = _tabs.Count - 1;
        };
    }

    private static RiceTabViewItem CreateTab(string header, MaterialIconKind icon, string text, bool closable = true) => new()
    {
        Header = header,
        Icon = icon,
        IsClosable = closable,
        Content = new TextBlock
        {
            Text = text,
            Margin = new Thickness(16),
            FontFamily = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace"),
            VerticalAlignment = VerticalAlignment.Top,
        },
    };
}
