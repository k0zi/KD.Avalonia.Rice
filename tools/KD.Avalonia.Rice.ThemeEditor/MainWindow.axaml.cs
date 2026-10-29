using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Controls.Charts;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

public partial class MainWindow : RiceWindow
{
    private const string PreviewText = """
        ### Markdown

        Text with a [link](https://avaloniaui.net), `inline code` and a quote:

        > Quotes get an accent tinted bar.

        ```csharp
        // Syntax colors follow the theme
        var theme = ThemeManager.Instance.CurrentTheme;
        Console.WriteLine($"Theme: {theme.DisplayName}, {42}");
        ```

        | Table | Header |
        |-------|--------|
        | one   | 1      |
        | two   | 2      |
        """;

    private readonly PaletteEditor _light = new();
    private readonly PaletteEditor _dark = new();
    private readonly ObservableCollection<ThemeEntry> _entries = [];
    private ThemeEntry? _loaded;
    private bool _applyPending;

    public MainWindow()
    {
        InitializeComponent();

        LightEditor.Content = _light;
        DarkEditor.Content = _dark;
        _light.Changed += (_, _) => OnEdited();
        _dark.Changed += (_, _) => OnEdited();
        NameBox.TextChanged += (_, _) =>
        {
            IdText.Text = $"Id: {CurrentId()}";
            if (_loaded is not null && NameBox.Text != DefaultName(_loaded))
                SetStatus("Unsaved changes.");
        };

        ThemeList.ItemsSource = _entries;
        ThemeList.SelectionChanged += (_, _) =>
        {
            if (ThemeList.SelectedItem is ThemeEntry entry)
                Load(entry);
            DeleteButton.IsEnabled = ThemeList.SelectedItem is ThemeEntry { IsBuiltIn: false };
        };
        SaveButton.Click += (_, _) => Save();
        CopyCodeButton.Click += async (_, _) => await CopyCode();
        RevertButton.Click += (_, _) =>
        {
            if (_loaded is not null)
                Load(_loaded);
        };
        DeleteButton.Click += (_, _) => Delete();
        FolderText.Text = $"Saved themes: {ThemeStore.Folder}";

        // The tab and the title bar toggle both switch the light / dark variant.
        VariantTabs.SelectionChanged += (_, _) =>
        {
            if (VariantTabs.SelectedIndex == 1 != ThemeManager.Instance.IsDark)
                ThemeManager.Instance.ToggleVariant();
        };
        ThemeManager.Instance.PropertyChanged += OnThemeManagerChanged;
        VariantTabs.SelectedIndex = ThemeManager.Instance.IsDark ? 1 : 0;

        PreviewChart.Data = new ChartCategoryData(["Q1", "Q2", "Q3", "Q4"],
            new ChartSeries("Accent", [42, 51, 48, 63]),
            new ChartSeries("Series 1", [35, 39, 46, 52]),
            new ChartSeries("Series 2", [22, 30, 37, 41]),
            new ChartSeries("Series 3", [28, 24, 33, 30]));
        PreviewMarkdown.Markdown = PreviewText;

        ReloadList(selectId: null);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        ThemeManager.Instance.PropertyChanged -= OnThemeManagerChanged;
    }

    private void OnThemeManagerChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ThemeManager.IsDark) or null)
            VariantTabs.SelectedIndex = ThemeManager.Instance.IsDark ? 1 : 0;
    }

    // A built-in theme is a starting point: its copy gets a new name, so saving does not clash with it.
    private static string DefaultName(ThemeEntry entry) =>
        entry.IsBuiltIn ? $"My {entry.DisplayName}" : entry.DisplayName;

    private string CurrentName() => NameBox.Text?.Trim() ?? "";

    private string CurrentId() => ThemeStore.ToId(CurrentName());

    private void Load(ThemeEntry entry)
    {
        _loaded = entry;
        _light.Load(entry.Light);
        _dark.Load(entry.Dark);
        NameBox.Text = DefaultName(entry);
        Apply();
        SetStatus("");
    }

    private void OnEdited()
    {
        SetStatus("Unsaved changes.");
        // Color pickers report every drag step; rebuilding the theme once per dispatcher pass is enough.
        if (_applyPending)
            return;
        _applyPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _applyPending = false;
            Apply();
        }, DispatcherPriority.Background);
    }

    private RiceTheme BuildTheme()
    {
        var id = CurrentId();
        return new RiceTheme(id.Length > 0 ? id : "custom", CurrentName() is { Length: > 0 } name ? name : "Custom",
            _light.Build(), _dark.Build());
    }

    private void Apply()
    {
        var theme = BuildTheme();
        Program.LiveThemes[0] = theme;
        ThemeManager.Instance.SetTheme(theme);
    }

    private void Save()
    {
        var id = CurrentId();
        if (id.Length == 0)
        {
            SetStatus("Enter a name with at least one letter or digit.", isError: true);
            return;
        }
        if (BuiltInThemes.All.Any(t => t.Id == id))
        {
            SetStatus($"The built-in theme '{id}' already uses this name, choose another one.", isError: true);
            return;
        }

        try
        {
            var replaced = _entries.Any(e => !e.IsBuiltIn && e.Theme.Id == id);
            var path = ThemeStore.Save(BuildTheme());
            ReloadList(selectId: id);
            SetStatus(replaced ? $"Saved, replacing the earlier version: {path}" : $"Saved: {path}");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not save: {e.Message}", isError: true);
        }
    }

    private void Delete()
    {
        if (ThemeList.SelectedItem is not ThemeEntry { IsBuiltIn: false } entry)
            return;
        try
        {
            ThemeStore.Delete(entry.Theme.Id);
            ReloadList(selectId: null);
            SetStatus($"Deleted '{entry.DisplayName}'.");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SetStatus($"Could not delete: {e.Message}", isError: true);
        }
    }

    private async Task CopyCode()
    {
        if (Clipboard is null)
            return;
        await Clipboard.SetTextAsync(ThemeCodeWriter.Write(BuildTheme()));
        SetStatus("C# code copied to the clipboard.");
    }

    private void ReloadList(string? selectId)
    {
        _entries.Clear();
        foreach (var theme in ThemeStore.LoadAll())
            _entries.Add(new ThemeEntry(theme, IsBuiltIn: false));
        foreach (var theme in BuiltInThemes.All)
            _entries.Add(new ThemeEntry(theme, IsBuiltIn: true));

        ThemeList.SelectedItem = _entries.FirstOrDefault(e => !e.IsBuiltIn && e.Theme.Id == selectId) ?? _entries[0];
    }

    private void SetStatus(string text, bool isError = false)
    {
        StatusText.Text = text;
        StatusText.Classes.Set("error", isError);
    }
}
