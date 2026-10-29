using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// The band behind the caret's line in a <see cref="RiceCodeEditor"/>; lies under the text presenter in the scrolled
/// content, so it spans the whole width. Hidden while text is selected.
/// </summary>
public class RiceCodeLineHighlight : Control
{
    public static readonly StyledProperty<IBrush?> FillProperty =
        AvaloniaProperty.Register<RiceCodeLineHighlight, IBrush?>(nameof(Fill));

    private TextPresenter? _presenter;

    static RiceCodeLineHighlight()
    {
        AffectsRender<RiceCodeLineHighlight>(FillProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<RiceCodeLineHighlight>(false);
    }

    public IBrush? Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    internal TextPresenter? Presenter
    {
        get => _presenter;
        set
        {
            _presenter?.LayoutUpdated -= OnPresenterChanged;
            _presenter?.PropertyChanged -= OnPresenterPropertyChanged;
            _presenter = value;
            _presenter?.LayoutUpdated += OnPresenterChanged;
            _presenter?.PropertyChanged += OnPresenterPropertyChanged;
            InvalidateVisual();
        }
    }

    private void OnPresenterChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnPresenterPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextPresenter.CaretIndexProperty || e.Property == TextPresenter.SelectionStartProperty
            || e.Property == TextPresenter.SelectionEndProperty)
            InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (Fill is not { } fill || _presenter is not { } presenter || presenter.SelectionStart != presenter.SelectionEnd)
            return;
        var caret = Math.Clamp(presenter.CaretIndex, 0, presenter.Text?.Length ?? 0);
        var layout = presenter.TextLayout;
        var line = layout.TextLines[layout.GetLineIndexFromCharacterIndex(caret, false)];
        var caretRect = layout.HitTestTextPosition(caret);
        var top = presenter.TranslatePoint(new Point(0, caretRect.Top), this)?.Y ?? caretRect.Top;
        context.FillRectangle(fill, new Rect(0, top, Bounds.Width, Math.Max(line.Height, caretRect.Height)));
    }
}
