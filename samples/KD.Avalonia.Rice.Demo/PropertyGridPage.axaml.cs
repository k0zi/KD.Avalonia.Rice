using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Demo;

public partial class PropertyGridPage : UserControl
{
    private readonly EditorSettings _settings = new();

    public PropertyGridPage()
    {
        InitializeComponent();
        PropertyGrid.SelectedObject = _settings;
        PropertyGrid.PropertyValueChanged += (_, e) => ChangeLog.Text = $"{e.Entry.DisplayName}: {e.OldValue} → {e.NewValue}";
        _settings.PropertyChanged += (_, _) => UpdatePreview();
        // The grid listens to PropertyChanged too, so changes from code show up in it.
        ResetButton.Click += (_, _) => _settings.Reset();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        Preview.Header = _settings.Title;
        PreviewText.Text = _settings.SampleText;
        PreviewText.FontSize = _settings.FontSize;
        PreviewText.FontWeight = _settings.Bold ? FontWeight.Bold : FontWeight.Normal;
        PreviewText.Foreground = new SolidColorBrush(_settings.TextColor);
        PreviewText.TextAlignment = _settings.Alignment;
        Preview.Elevation = _settings.Elevation;
        Preview.IsEnabled = _settings.IsEnabled;
    }
}

/// <summary>Sample object for the property grid: every editor type and attribute once.</summary>
public sealed class EditorSettings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("Card"), Description("Text in the header of the preview card.")]
    public string Title { get => field; set => Set(ref field, value); } = "Preview";

    [Category("Card"), Description("Shadow depth of the card, 0 (flat) to 3."), Range(0, 3)]
    public int Elevation { get => field; set => Set(ref field, value); } = 1;

    [Category("Card"), DisplayName("Enabled"), Description("A disabled card is dimmed.")]
    public bool IsEnabled { get => field; set => Set(ref field, value); } = true;

    [Category("Text"), DisplayName("Sample text"), Description("The paragraph shown in the card.")]
    public string SampleText { get => field; set => Set(ref field, value); } = "The quick brown fox jumps over the lazy dog.";

    [Category("Text"), Description("Height of the letters in pixels."), Range(8, 40)]
    public double FontSize { get => field; set => Set(ref field, value); } = 14;

    [Category("Text"), Description("Semi-bold letters.")]
    public bool Bold { get => field; set => Set(ref field, value); }

    [Category("Text"), Description("Horizontal alignment of the paragraph.")]
    public TextAlignment Alignment { get => field; set => Set(ref field, value); } = TextAlignment.Left;

    [Category("Text"), DisplayName("Color"), Description("Hex color, e.g. #E95420 (Ubuntu orange) or #88C0D0 (Nord frost).")]
    public Color TextColor { get => field; set => Set(ref field, value); } = Color.Parse("#E95420");

    [Category("Meta"), Description("Shown read-only: the property has no public setter.")]
    public Guid Id { get; } = Guid.NewGuid();

    [Category("Meta"), Description("Edited as text through its TypeConverter.")]
    public TimeSpan Timeout { get => field; set => Set(ref field, value); } = TimeSpan.FromSeconds(30);

    [Category("Meta"), Description("A date without the time part.")]
    public DateTime Created { get => field; set => Set(ref field, value); } = DateTime.Today;

    [Browsable(false)]
    public string Hidden { get; set; } = "not shown";

    public void Reset()
    {
        Title = "Preview";
        Elevation = 1;
        IsEnabled = true;
        FontSize = 14;
        Bold = false;
        Alignment = TextAlignment.Left;
        TextColor = Color.Parse("#E95420");
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
