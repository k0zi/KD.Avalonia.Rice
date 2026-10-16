using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KD.Avalonia.Rice.Controls.Markdowns;

namespace KD.Avalonia.Rice.Demo;

public partial class MarkdownPage : UserControl
{
    private static readonly FilePickerFileType MarkdownFiles = new("Markdown")
    {
        Patterns = ["*.md", "*.markdown", "*.mmd", "*.txt"],
        MimeTypes = ["text/markdown", "text/plain"],
    };

    // The preview follows the editor after a short pause in typing, not on every key.
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public MarkdownPage()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            Preview.Markdown = SourceBox.Text;
            UpdateStatus();
        };
        SourceBox.TextChanged += (_, _) =>
        {
            _debounce.Stop();
            _debounce.Start();
        };
        Preview.LinkClicked += (_, e) => StatusText.Text = $"Link: {e.Url}";
        EditorToggle.IsCheckedChanged += (_, _) => ShowEditor(EditorToggle.IsChecked == true);

        SampleBox.ItemsSource = MarkdownSamples.All;
        SampleBox.SelectionChanged += (_, _) =>
        {
            if (SampleBox.SelectedItem is MarkdownSamples.Sample sample)
                ShowText(sample.Markdown, null);
        };
        SampleBox.SelectedIndex = 0;
    }

    private void ShowEditor(bool show)
    {
        SourceBox.IsVisible = show;
        Splitter.IsVisible = show;
        Columns.ColumnDefinitions[0].Width = show ? new GridLength(2, GridUnitType.Star) : new GridLength(0);
        Columns.ColumnDefinitions[1].Width = new GridLength(show ? 8 : 0);
    }

    private void ShowText(string text, Uri? baseUri)
    {
        _debounce.Stop();
        SourceBox.Text = text;
        _debounce.Stop();
        Preview.BaseUri = baseUri;
        Preview.Markdown = text;
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var document = Preview.Document;
        StatusText.Text = document is null
            ? "Empty document"
            : $"{document.Blocks.Count} blocks · {document.Headings.Count} headings · {document.Footnotes.Count} footnotes";
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown file",
            FileTypeFilter = [MarkdownFiles, FilePickerFileTypes.All],
        });
        if (files.Count == 0)
            return;
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync();
        // A Mermaid file is shown as a diagram block.
        if (files[0].Name.EndsWith(".mmd", StringComparison.OrdinalIgnoreCase))
            text = $"```mermaid\n{text.TrimEnd()}\n```\n";
        SampleBox.SelectedItem = null;
        // Relative images and links of the file are resolved from its folder.
        ShowText(text, files[0].Path);
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Markdown file",
            SuggestedFileName = "document.md",
            DefaultExtension = "md",
            FileTypeChoices = [MarkdownFiles],
        });
        if (file is null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(SourceBox.Text ?? "");
    }
}
