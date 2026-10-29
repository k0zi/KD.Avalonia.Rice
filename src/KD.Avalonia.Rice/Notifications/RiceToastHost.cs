using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Notifications;

/// <summary>
/// Stacks <see cref="RiceToastItem"/>s in a corner of its parent, the newest nearest to the edge. While the pointer
/// is over the stack every countdown pauses. <see cref="RiceWindow"/> has one built in; other windows get one in
/// their overlay layer on first use (<see cref="For"/>).
/// </summary>
public class RiceToastHost : StackPanel
{
    public static readonly StyledProperty<ToastPosition> PositionProperty =
        AvaloniaProperty.Register<RiceToastHost, ToastPosition>(nameof(Position), ToastPosition.BottomRight);

    /// <summary>When more toasts are shown, the oldest ones close.</summary>
    public static readonly StyledProperty<int> MaxItemsProperty =
        AvaloniaProperty.Register<RiceToastHost, int>(nameof(MaxItems), 5);

    public static readonly StyledProperty<double> ToastWidthProperty =
        AvaloniaProperty.Register<RiceToastHost, double>(nameof(ToastWidth), 360);

    private static readonly ConditionalWeakTable<TopLevel, RiceToastHost> OverlayHosts = new();

    public RiceToastHost()
    {
        Margin = new Thickness(16, 12);
        Width = ToastWidth;
        UpdateAlignment();
    }

    public ToastPosition Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public int MaxItems
    {
        get => GetValue(MaxItemsProperty);
        set => SetValue(MaxItemsProperty, value);
    }

    public double ToastWidth
    {
        get => GetValue(ToastWidthProperty);
        set => SetValue(ToastWidthProperty, value);
    }

    /// <summary>Toasts shown and not yet closing, oldest first.</summary>
    public IEnumerable<RiceToastItem> Toasts
    {
        get
        {
            var open = Children.OfType<RiceToastItem>().Where(t => !t.IsClosing);
            return IsTop ? open.Reverse() : open;
        }
    }

    private bool IsTop => IsTopPosition(Position);

    /// <summary>
    /// The host of a window: the built-in one of a <see cref="RiceWindow"/>, else one added to its overlay layer.
    /// Null when the window has neither (e.g. it is not shown yet).
    /// </summary>
    public static RiceToastHost? For(TopLevel topLevel)
    {
        if (topLevel is RiceWindow { ToastHost: { } builtIn })
            return builtIn;
        if (OverlayHosts.TryGetValue(topLevel, out var existing))
            return existing;
        if (OverlayLayer.GetOverlayLayer(topLevel) is not { } layer)
            return null;

        // The overlay layer is a canvas: the container is sized to it, so the host can align to a corner.
        var host = new RiceToastHost();
        var container = new Panel { Children = { host } };
        void Fit() => (container.Width, container.Height) = (layer.Bounds.Width, layer.Bounds.Height);
        layer.PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
                Fit();
        };
        Fit();
        layer.Children.Add(container);
        OverlayHosts.Add(topLevel, host);
        return host;
    }

    public void Show(RiceToastItem toast)
    {
        toast.SetSlideDirection(Position);
        toast.SetPaused(IsPointerOver);
        if (IsTop)
            Children.Insert(0, toast);
        else
            Children.Add(toast);

        foreach (var oldest in Toasts.SkipLast(Math.Max(MaxItems, 1)).ToList())
            oldest.Close();
    }

    public void CloseAll()
    {
        foreach (var toast in Toasts.ToList())
            toast.Close();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PositionProperty)
        {
            UpdateAlignment();
            // The newest toast is nearest to the edge, so moving between top and bottom flips the order.
            if (IsTopPosition(change.GetOldValue<ToastPosition>()) != IsTop)
            {
                var toasts = Children.Reverse().ToList();
                Children.Clear();
                Children.AddRange(toasts);
            }
            foreach (var toast in Children.OfType<RiceToastItem>())
                toast.SetSlideDirection(Position);
        }
        else if (change.Property == ToastWidthProperty)
        {
            Width = ToastWidth;
        }
        else if (change.Property == IsPointerOverProperty)
        {
            foreach (var toast in Children.OfType<RiceToastItem>())
                toast.SetPaused(IsPointerOver);
        }
    }

    private static bool IsTopPosition(ToastPosition position) =>
        position is ToastPosition.TopLeft or ToastPosition.TopCenter or ToastPosition.TopRight;

    private void UpdateAlignment()
    {
        VerticalAlignment = IsTop ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        HorizontalAlignment = Position switch
        {
            ToastPosition.TopLeft or ToastPosition.BottomLeft => HorizontalAlignment.Left,
            ToastPosition.TopCenter or ToastPosition.BottomCenter => HorizontalAlignment.Center,
            _ => HorizontalAlignment.Right,
        };
    }
}
