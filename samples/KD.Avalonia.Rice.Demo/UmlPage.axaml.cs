using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using KD.Avalonia.Rice.Controls.Uml;

namespace KD.Avalonia.Rice.Demo;

public partial class UmlPage : UserControl
{
    private static readonly FilePickerFileType PlantUmlFiles = new("PlantUML")
    {
        Patterns = ["*.puml", "*.plantuml", "*.pu", "*.iuml", "*.txt"],
        MimeTypes = ["text/plain"],
    };

    // The diagram follows the editor after a short pause in typing, not on every key.
    private readonly DispatcherTimer _debounce = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public UmlPage()
    {
        InitializeComponent();
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            DiagramView.Source = SourceBox.Text;
            UpdateStatus();
        };
        SourceBox.TextChanged += (_, _) =>
        {
            _debounce.Stop();
            _debounce.Start();
        };
        DiagramView.SelectionChanged += (_, _) => UpdateStatus();

        SampleBox.ItemsSource = UmlSamples.All;
        SampleBox.SelectionChanged += (_, _) =>
        {
            if (SampleBox.SelectedItem is UmlSamples.Sample sample)
                ShowSource(sample.Source);
        };
        SampleBox.SelectedIndex = 0;
    }

    private void ShowSource(string source)
    {
        _debounce.Stop();
        SourceBox.Text = source;
        _debounce.Stop();
        DiagramView.Source = source;
        DiagramView.SelectedElement = null;
        DispatcherTimer.RunOnce(DiagramView.ZoomToFit, TimeSpan.FromMilliseconds(1));
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        var kind = DiagramView.Diagram?.Kind.ToString() ?? "–";
        var problems = DiagramView.Diagnostics.Count(d => d.Severity != UmlDiagnosticSeverity.Info);
        var selection = DiagramView.SelectedElement switch
        {
            null => "nothing selected",
            UmlElement e => $"{e.Kind} {e.Name}",
            UmlRelation r => $"{r.Kind} {r.From} → {r.To}" + (r.Label is null ? "" : $" ({r.Label})"),
            UmlMessage m => $"Message {m.From} → {m.To}: {m.Text}",
            UmlParticipant p => $"{p.Kind} {p.Name}",
            UmlActivityAction a => $"Action {a.Text}",
            UmlActivityReference r => $"Reference {r.Name}",
            UmlTimingLifeline l => $"{l.Kind} lifeline {l.Name}",
            var other => other.GetType().Name.Replace("Uml", ""),
        };
        StatusText.Text = $"{kind} diagram · {problems} problem(s) · {selection}";
    }

    private async void OnOpenClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open PlantUML file",
            FileTypeFilter = [PlantUmlFiles, FilePickerFileTypes.All],
        });
        if (files.Count == 0)
            return;
        await using var stream = await files[0].OpenReadAsync();
        using var reader = new StreamReader(stream);
        SampleBox.SelectedItem = null;
        ShowSource(await reader.ReadToEndAsync());
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
            return;
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save PlantUML file",
            SuggestedFileName = (DiagramView.Diagram?.Kind.ToString().ToLowerInvariant() ?? "diagram") + ".puml",
            DefaultExtension = "puml",
            FileTypeChoices = [PlantUmlFiles],
        });
        if (file is null)
            return;
        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(SourceBox.Text ?? "");
    }
}
