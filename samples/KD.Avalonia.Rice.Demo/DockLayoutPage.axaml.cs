using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class DockLayoutPage : UserControl
{
    public DockLayoutPage()
    {
        InitializeComponent();
        Editor.Text = CodeEditorPage.Samples["cs"];
        Files.ItemsSource = new[]
        {
            Folder("src", Folder("Controls", File("RiceDockLayout.cs"), File("RiceDockPane.cs")), File("RiceStrings.cs")),
            Folder("docs", File("rice-dock-layout.md")),
            File("README.md"),
        };
        // The properties pane edits the editor in the middle.
        var options = new CodeEditorOptions();
        options.PropertyChanged += (_, _) => options.ApplyTo(Editor);
        options.ApplyTo(Editor);
        PropertyGrid.SelectedObject = options;
        OutputText.Text = "Build started…\nKD.Avalonia.Rice -> bin/Debug/net10.0/KD.Avalonia.Rice.dll\nBuild succeeded. 0 warnings, 0 errors.";

        ResetButton.Click += (_, _) => Layout.ResetLayout();
        Layout.LayoutChanged += (_, _) => ShowLayout();
        Layout.Loaded += (_, _) => ShowLayout();
    }

    private void ShowLayout() =>
        LayoutInfo.Text = string.Join("   ", Layout.Panes.Select(p => $"{p.LayoutId}: {(p.IsCollapsed ? "collapsed" : $"{p.Size:0}")}"));

    private static TreeViewItem Folder(string name, params TreeViewItem[] children) =>
        new() { Header = "📁 " + name, ItemsSource = children, IsExpanded = true };

    private static TreeViewItem File(string name) => new() { Header = "📄 " + name };
}

/// <summary>A few settings of the dock layout demo's editor, for the property grid.</summary>
public sealed class CodeEditorOptions : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("Code"), Description("cs, xaml, json, js, py, bash, sql, yaml, plantuml or mermaid.")]
    public string Language { get => field; set => Set(ref field, value); } = "cs";

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

    public void ApplyTo(RiceCodeEditor editor)
    {
        editor.Language = Language;
        editor.TabSize = TabSize;
        editor.ConvertTabsToSpaces = ConvertTabsToSpaces;
        editor.ShowLineNumbers = ShowLineNumbers;
        editor.HighlightCurrentLine = HighlightCurrentLine;
        editor.FontSize = FontSize;
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
