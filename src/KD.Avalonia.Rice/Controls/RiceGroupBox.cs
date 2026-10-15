using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Frame around related controls with a title on the top line, like the WinForms GroupBox.
/// The title is <c>Header</c> plus an optional <see cref="Icon"/>
/// (an <c>IImage</c>, a <c>MaterialIconKind</c>, a string such as an emoji, or any control).
/// </summary>
public class RiceGroupBox : HeaderedContentControl
{
    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceGroupBox, object?>(nameof(Icon));

    // Space between the line ends and the title.
    private const double GapPadding = 4;

    private RiceGroupBoxFrame? _frame;
    private Control? _header;

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceGroupBox);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _header?.PropertyChanged -= OnHeaderPropertyChanged;
        _frame = e.NameScope.Find<RiceGroupBoxFrame>("PART_Frame");
        _header = e.NameScope.Find<Control>("PART_Header");
        _header?.PropertyChanged += OnHeaderPropertyChanged;
        UpdateHeaderVisibility();
        UpdateGap();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeaderProperty || change.Property == IconProperty)
            UpdateHeaderVisibility();
    }

    private void OnHeaderPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == IsVisibleProperty)
            UpdateGap();
    }

    private void UpdateHeaderVisibility() => _header?.IsVisible = Header is not null || Icon is not null;

    // The line runs through the middle of the title and is cut where the title is.
    private void UpdateGap()
    {
        if (_frame is null || _header is null)
            return;
        var bounds = _header.IsVisible ? _header.Bounds : default;
        _frame.Margin = new Thickness(0, bounds.Height / 2, 0, 0);
        _frame.GapStart = bounds.X - GapPadding;
        _frame.GapWidth = bounds.Width > 0 ? bounds.Width + 2 * GapPadding : 0;
    }
}
