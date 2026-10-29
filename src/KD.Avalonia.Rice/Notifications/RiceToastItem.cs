using System.Diagnostics;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace KD.Avalonia.Rice.Notifications;

/// <summary>
/// One toast: a card tinted with the color of its <see cref="Kind"/>, with an icon, title, message, optional custom
/// content and action button, a close button and a bar counting down the time until it closes itself.
/// Usually created by <see cref="RiceToast"/> and stacked by a <see cref="RiceToastHost"/>.
/// </summary>
public class RiceToastItem : ContentControl
{
    public static readonly StyledProperty<ToastKind> KindProperty =
        AvaloniaProperty.Register<RiceToastItem, ToastKind>(nameof(Kind));

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<RiceToastItem, string?>(nameof(Title));

    public static readonly StyledProperty<string?> MessageProperty =
        AvaloniaProperty.Register<RiceToastItem, string?>(nameof(Message));

    public static readonly StyledProperty<bool> ShowCloseButtonProperty =
        AvaloniaProperty.Register<RiceToastItem, bool>(nameof(ShowCloseButton), true);

    /// <summary>Color of the stripe, the icon and the countdown bar; set by the theme for each kind.</summary>
    public static readonly StyledProperty<IBrush?> KindBrushProperty =
        AvaloniaProperty.Register<RiceToastItem, IBrush?>(nameof(KindBrush));

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(16);

    private readonly ToastOptions _options;
    private readonly Stopwatch _elapsed = new();
    private readonly ScaleTransform _progressScale = new(1, 1);
    private DispatcherTimer? _timer;
    private bool _paused;
    private bool _closing;

    // A toast placed in XAML (e.g. a preview) stays until closed.
    public RiceToastItem() : this(new ToastOptions { Duration = TimeSpan.Zero })
    {
    }

    public RiceToastItem(ToastOptions options)
    {
        _options = options;
        Kind = options.Kind;
        Title = options.Title;
        Message = options.Message;
        Content = options.Content;
        ContentTemplate = options.ContentTemplate;
        ShowCloseButton = options.ShowCloseButton;
        if (options.OnClick is not null)
            Cursor = new Cursor(StandardCursorType.Hand);
        // Screen readers announce toasts when they appear; errors interrupt.
        AutomationProperties.SetLiveSetting(this,
            options.Kind == ToastKind.Error ? AutomationLiveSetting.Assertive : AutomationLiveSetting.Polite);
        AutomationProperties.SetName(this, string.Join(": ", new[] { options.Title, options.Message }.Where(t => !string.IsNullOrEmpty(t))));
        UpdateKindPseudoClasses();
    }

    public ToastKind Kind
    {
        get => GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

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

    public bool ShowCloseButton
    {
        get => GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    public IBrush? KindBrush
    {
        get => GetValue(KindBrushProperty);
        set => SetValue(KindBrushProperty, value);
    }

    /// <summary>Time until the toast closes itself; <see cref="TimeSpan.Zero"/> when it stays.</summary>
    public TimeSpan Duration => _options.EffectiveDuration;

    public bool IsClosing => _closing;

    /// <summary>Raised once the toast is closed and removed from its host.</summary>
    public event EventHandler? Closed;

    /// <summary>Fades the toast out, collapses its place in the stack and removes it.</summary>
    public async void Close()
    {
        if (_closing)
            return;
        _closing = true;
        StopTimer();
        PseudoClasses.Set(":closing", true);
        IsHitTestVisible = false;

        if (TopLevel.GetTopLevel(this) is not null)
        {
            // :closing fades it out through the theme's opacity transition (an animation would fight the transition).
            await Task.Delay(200);
            // Collapsing the height and the margin lets the toasts below slide up instead of jumping.
            // The removal waits for a timer, not for the animation: it must not depend on the render clock.
            var collapse = TimeSpan.FromMilliseconds(160);
            _ = Animate(collapse, (MaxHeightProperty, Bounds.Height, 0d), (MarginProperty, Margin, new Thickness(0)));
            await Task.Delay(collapse);
        }

        (Parent as Panel)?.Children.Remove(this);
        _options.OnClosed?.Invoke();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops the countdown, e.g. while the pointer is over the toasts.</summary>
    internal void SetPaused(bool paused)
    {
        _paused = paused;
        if (paused)
            _elapsed.Stop();
        else if (_timer is not null)
            _elapsed.Start();
    }

    internal void SetSlideDirection(ToastPosition position)
    {
        PseudoClasses.Set(":from-left", position is ToastPosition.TopLeft or ToastPosition.BottomLeft);
        PseudoClasses.Set(":from-top", position is ToastPosition.TopCenter);
        PseudoClasses.Set(":from-bottom", position is ToastPosition.BottomCenter);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Border>("PART_Progress") is { } progress)
        {
            progress.RenderTransformOrigin = new RelativePoint(0, 0.5, RelativeUnit.Relative);
            progress.RenderTransform = _progressScale;
            progress.IsVisible = _options.ShowProgress && Duration > TimeSpan.Zero;
        }

        if (e.NameScope.Find<Button>("PART_CloseButton") is { } close)
        {
            ToolTip.SetTip(close, RiceStrings.ToastClose);
            AutomationProperties.SetName(close, RiceStrings.ToastClose);
            close.Click += (_, args) =>
            {
                args.Handled = true;
                Close();
            };
        }

        if (e.NameScope.Find<Button>("PART_ActionButton") is { } actionButton)
        {
            actionButton.IsVisible = _options.Action is not null;
            if (_options.Action is { } action)
            {
                actionButton.Content = action.Text;
                actionButton.Command = action.Command;
                actionButton.CommandParameter = action.CommandParameter;
                actionButton.Click += (_, args) =>
                {
                    args.Handled = true;
                    action.Execute?.Invoke();
                    Close();
                };
            }
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // After the first frame at the theme's start values, so the transitions slide and fade it in.
        Dispatcher.UIThread.Post(() =>
        {
            if (_closing)
                return;
            PseudoClasses.Set(":shown", true);
            StartTimer();
        }, DispatcherPriority.Background);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        StopTimer();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == KindProperty)
            UpdateKindPseudoClasses();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.Handled && _options.OnClick is { } onClick && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Handled = true;
            onClick();
            Close();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ControlAutomationPeer(this);

    private void UpdateKindPseudoClasses()
    {
        PseudoClasses.Set(":info", Kind == ToastKind.Info);
        PseudoClasses.Set(":success", Kind == ToastKind.Success);
        PseudoClasses.Set(":warning", Kind == ToastKind.Warning);
        PseudoClasses.Set(":error", Kind == ToastKind.Error);
    }

    private void StartTimer()
    {
        if (Duration <= TimeSpan.Zero || _timer is not null)
            return;
        _timer = new DispatcherTimer(TickInterval, DispatcherPriority.Render, (_, _) => Tick());
        _timer.Start();
        if (!_paused)
            _elapsed.Start();
    }

    private void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
        _elapsed.Stop();
    }

    private void Tick()
    {
        var remaining = 1 - _elapsed.Elapsed / Duration;
        if (remaining <= 0)
            Close();
        else
            _progressScale.ScaleX = remaining;
    }

    private Task Animate(TimeSpan duration, params (AvaloniaProperty Property, object From, object To)[] values)
    {
        var from = new KeyFrame { Cue = new Cue(0) };
        var to = new KeyFrame { Cue = new Cue(1) };
        foreach (var (property, start, end) in values)
        {
            from.Setters.Add(new Setter(property, start));
            to.Setters.Add(new Setter(property, end));
        }
        var animation = new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children = { from, to },
        };
        return animation.RunAsync(this);
    }
}
