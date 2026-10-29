using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Message bar that stays in the layout of a page (unlike a toast): a strip tinted with the color of its
/// <see cref="Kind"/>, with an icon, <see cref="Title"/>, <see cref="Message"/>, optional custom content, an
/// <see cref="Action"/> (e.g. a button) and a close button. Closing sets <see cref="IsOpen"/> to false, which hides it.
/// </summary>
public class RiceInfoBar : ContentControl
{
    public static readonly StyledProperty<InfoBarKind> KindProperty =
        AvaloniaProperty.Register<RiceInfoBar, InfoBarKind>(nameof(Kind));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RiceInfoBar, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<RiceInfoBar, string?>(nameof(Message));

    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<RiceInfoBar, bool>(nameof(IsOpen), true,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsClosableProperty =
        AvaloniaProperty.Register<RiceInfoBar, bool>(nameof(IsClosable), true);

    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<RiceInfoBar, object?>(nameof(Action));

    public static readonly StyledProperty<object?> IconProperty =
        RiceCard.IconProperty.AddOwner<RiceInfoBar>();

    public static readonly StyledProperty<bool> ShowIconProperty =
        AvaloniaProperty.Register<RiceInfoBar, bool>(nameof(ShowIcon), true);

    /// <summary>Color of the stripe and the icon; set by the theme for each kind.</summary>
    public static readonly StyledProperty<IBrush?> KindBrushProperty =
        AvaloniaProperty.Register<RiceInfoBar, IBrush?>(nameof(KindBrush));

    public static readonly RoutedEvent<RoutedEventArgs> ClosedEvent =
        RoutedEvent.Register<RiceInfoBar, RoutedEventArgs>(nameof(Closed), RoutingStrategies.Bubble);

    private TextBlock? _text;

    public RiceInfoBar()
    {
        UpdatePseudoClasses();
        // Screen readers announce the bar when it appears; errors interrupt.
        AutomationProperties.SetLiveSetting(this, AutomationLiveSetting.Polite);
    }

    public InfoBarKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    /// <summary>Bold text in front of the message.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    /// <summary>Shows the bar; the close button sets it to false.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Shows the close button. Default true.</summary>
    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    /// <summary>Content on the right, before the close button, e.g. a button or a hyperlink.</summary>
    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }

    /// <summary>Replaces the icon of the kind: an <c>IImage</c>, a <c>MaterialIconKind</c>, a string or any control.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public bool ShowIcon
    {
        get => GetValue(ShowIconProperty);
        set => SetValue(ShowIconProperty, value);
    }

    public IBrush? KindBrush
    {
        get => GetValue(KindBrushProperty);
        set => SetValue(KindBrushProperty, value);
    }

    /// <summary>Raised when the bar is closed by its close button or by setting <see cref="IsOpen"/> to false.</summary>
    public event EventHandler<RoutedEventArgs>? Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceInfoBar);

    public void Close() => IsOpen = false;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _text = e.NameScope.Find<TextBlock>("PART_Text");
        UpdateText();
        if (e.NameScope.Find<Button>("PART_CloseButton") is { } close)
        {
            ToolTip.SetTip(close, RiceStrings.InfoBarClose);
            AutomationProperties.SetName(close, RiceStrings.InfoBarClose);
            close.Click += (_, args) =>
            {
                args.Handled = true;
                Close();
            };
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
        {
            UpdatePseudoClasses();
            AutomationProperties.SetLiveSetting(this,
                Kind == InfoBarKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        }
        else if (change.Property == IconProperty)
        {
            UpdatePseudoClasses();
        }
        else if (change.Property == TitleProperty || change.Property == MessageProperty)
        {
            UpdateText();
        }
        else if (change.Property == IsOpenProperty)
        {
            UpdatePseudoClasses();
            if (!IsOpen)
                RaiseEvent(new RoutedEventArgs(ClosedEvent));
        }
    }

    private void UpdateText()
    {
        if (_text is null)
            return;
        var inlines = new InlineCollection();
        if (!string.IsNullOrEmpty(Title))
            inlines.Add(new Run(Title) { FontWeight = FontWeight.SemiBold });
        if (!string.IsNullOrEmpty(Message))
            inlines.Add(new Run(inlines.Count > 0 ? "  " + Message : Message));
        _text.Inlines = inlines;
        _text.IsVisible = inlines.Count > 0;
        AutomationProperties.SetName(this, string.Join(": ", new[] { Title, Message }.Where(t => !string.IsNullOrEmpty(t))));
    }

    private void UpdatePseudoClasses()
    {
        PseudoClasses.Set(":info", Kind == InfoBarKind.Info);
        PseudoClasses.Set(":success", Kind == InfoBarKind.Success);
        PseudoClasses.Set(":warning", Kind == InfoBarKind.Warning);
        PseudoClasses.Set(":error", Kind == InfoBarKind.Error);
        PseudoClasses.Set(":custom-icon", Icon is not null);
        PseudoClasses.Set(":open", IsOpen);
    }
}
