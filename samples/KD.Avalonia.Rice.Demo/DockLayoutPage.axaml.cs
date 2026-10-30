using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Notifications;
using Material.Icons;
using Material.Icons.Avalonia;

namespace KD.Avalonia.Rice.Demo;

public partial class DockLayoutPage : UserControl
{
    // The files of the explorer: name, language and text.
    private static readonly Dictionary<string, (string Language, string Text)> Documents = new()
    {
        ["Counter.cs"] = ("cs", CodeEditorPage.Samples["cs"]),
        ["MainWindow.axaml"] = ("xaml", CodeEditorPage.Samples["xaml"]),
        ["appsettings.json"] = ("json", CodeEditorPage.Samples["json"]),
        ["fib.py"] = ("py", CodeEditorPage.Samples["py"]),
        ["report.sql"] = ("sql", CodeEditorPage.Samples["sql"]),
    };

    private readonly CodeEditorOptions _options = new();
    private int _untitled;

    public DockLayoutPage()
    {
        InitializeComponent();
        Files.ItemsSource = new[]
        {
            Folder("src", File("Counter.cs"), File("MainWindow.axaml"), File("fib.py")),
            Folder("config", File("appsettings.json")),
            File("report.sql"),
        };
        Files.DoubleTapped += (_, _) =>
        {
            if (Files.SelectedItem is TreeViewItem { Tag: string name })
                OpenDocument(name);
        };
        CollapseAllButton.Click += (_, _) =>
        {
            foreach (var item in Files.Items.OfType<TreeViewItem>())
                item.IsExpanded = false;
        };
        _options.PropertyChanged += (_, _) => ApplyOptions();
        PropertyGrid.SelectedObject = _options;
        OutputText.Text = "Build started…\nKD.Avalonia.Rice -> bin/Debug/net10.0/KD.Avalonia.Rice.dll\nBuild succeeded. 0 warnings, 0 errors.";

        OpenDocument("Counter.cs");
        OpenDocument("MainWindow.axaml");

        ViewButton.Click += (_, _) => ShowViewMenu();
        NewDocumentButton.Click += (_, _) => AddDocument($"Untitled {++_untitled}", "cs", "");
        ResetButton.Click += (_, _) => Layout.ResetLayout();
        Layout.LayoutChanged += (_, _) => ShowLayout();
        Layout.Loaded += (_, _) => ShowLayout();
        Layout.PaneClosing += OnPaneClosing;
    }

    // A changed document asks once: closing it again discards the changes.
    private void OnPaneClosing(object? sender, RiceDockPaneClosingEventArgs e)
    {
        if (e.Pane is not { Content: RiceCodeEditor { Tag: true } editor })
            return;
        editor.Tag = false;
        e.Cancel = true;
        RiceToast.ShowWarning("Close it again to discard the changes.", $"{e.Pane.Header} has unsaved changes");
    }

    private void OpenDocument(string name)
    {
        // An open document comes to the front, a closed one opens again.
        if (Layout.AllPanes.FirstOrDefault(p => p.Id == "doc:" + name) is { } existing)
        {
            Layout.ShowPane(existing);
            return;
        }
        var (language, text) = Documents[name];
        AddDocument(name, language, text);
    }

    private void AddDocument(string name, string language, string text)
    {
        var editor = new RiceCodeEditor
        {
            Language = language,
            Text = text,
            CornerRadius = new CornerRadius(0),
            BorderThickness = new Thickness(0),
        };
        editor.TextChanged += (_, _) => editor.Tag = true;
        ApplyOptions(editor);
        Layout.AddPane(new RiceDockPane
        {
            Id = "doc:" + name,
            Header = name,
            Dock = DockSide.Document,
            Icon = MaterialIconKind.FileCodeOutline,
            Content = editor,
        });
    }

    private void ApplyOptions()
    {
        foreach (var editor in Layout.AllPanes.Select(p => p.Content).OfType<RiceCodeEditor>())
            ApplyOptions(editor);
    }

    private void ApplyOptions(RiceCodeEditor editor)
    {
        editor.TabSize = _options.TabSize;
        editor.ConvertTabsToSpaces = _options.ConvertTabsToSpaces;
        editor.ShowLineNumbers = _options.ShowLineNumbers;
        editor.HighlightCurrentLine = _options.HighlightCurrentLine;
        editor.FontSize = _options.FontSize;
    }

    // Every tool window with a check mark when it is open; clicking shows a closed one or closes an open one.
    private void ShowViewMenu()
    {
        var menu = new MenuFlyout();
        foreach (var pane in Layout.AllPanes.Where(p => p.Dock != DockSide.Document))
        {
            var item = new MenuItem
            {
                Header = pane.Header,
                Icon = pane.IsOpen ? new MaterialIcon { Kind = MaterialIconKind.Check } : null,
            };
            item.Click += (_, _) =>
            {
                if (pane.IsOpen)
                    pane.Close();
                else
                    pane.Show();
            };
            menu.Items.Add(item);
        }
        menu.ShowAt(ViewButton);
    }

    private void ShowLayout()
    {
        var closed = Layout.ClosedPanes.Select(p => p.Header).ToList();
        LayoutInfo.Text = $"{Layout.Panes.Count()} open" + (closed.Count > 0 ? $", closed: {string.Join(", ", closed)}" : "");
    }

    private static TreeViewItem Folder(string name, params TreeViewItem[] children) =>
        new() { Header = "📁 " + name, ItemsSource = children, IsExpanded = true };

    private static TreeViewItem File(string name) => new() { Header = "📄 " + name, Tag = name };
}

/// <summary>A few settings of the dock layout demo's editors, for the property grid.</summary>
public sealed class CodeEditorOptions : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("Code"), DisplayName("Tab size"), Range(1, 8)]
    public int TabSize { get => field; set => Set(ref field, value); } = 4;

    [Category("Code"), DisplayName("Tabs to spaces")]
    public bool ConvertTabsToSpaces { get => field; set => Set(ref field, value); } = true;

    [Category("View"), DisplayName("Line numbers")]
    public bool ShowLineNumbers { get => field; set => Set(ref field, value); } = true;

    [Category("View"), DisplayName("Current line")]
    public bool HighlightCurrentLine { get => field; set => Set(ref field, value); } = true;

    [Category("View"), DisplayName("Font size"), Range(8, 32)]
    public double FontSize { get => field; set => Set(ref field, value); } = 13;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
