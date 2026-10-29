using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Layout of resizable, collapsible side panels around a content area, like the tool windows of an IDE: the
/// <see cref="RiceDockPane"/> children take their edge in order (as in a <see cref="DockPanel"/>), every other child
/// fills the rest. The content keeps at least <see cref="ContentMinSize"/>; the panes give way when the window shrinks.
/// <see cref="SaveLayout"/> / <see cref="RestoreLayout"/> keep the sizes and collapsed states; with
/// <see cref="LayoutKey"/> that happens by itself, in <c>~/.config/&lt;AppId&gt;/layouts/&lt;key&gt;.json</c>.
/// </summary>
public class RiceDockLayout : Panel
{
    public static readonly StyledProperty<double> ContentMinSizeProperty =
        AvaloniaProperty.Register<RiceDockLayout, double>(nameof(ContentMinSize), 120);

    /// <summary>Saves the layout under this key when it changes and restores it on load; null: not saved.</summary>
    public static readonly StyledProperty<string?> LayoutKeyProperty =
        AvaloniaProperty.Register<RiceDockLayout, string?>(nameof(LayoutKey));

    public static readonly RoutedEvent<RoutedEventArgs> LayoutChangedEvent =
        RoutedEvent.Register<RiceDockLayout, RoutedEventArgs>(nameof(LayoutChanged), RoutingStrategies.Bubble);

    // Saving waits until the splitter has rested a little.
    private static readonly TimeSpan SaveDelay = TimeSpan.FromMilliseconds(600);

    private DispatcherTimer? _saveTimer;
    private bool _restoring;

    static RiceDockLayout()
    {
        AffectsMeasure<RiceDockLayout>(ContentMinSizeProperty);
    }

    /// <summary>Width and height the content area keeps when the panes are resized. Default 120.</summary>
    public double ContentMinSize
    {
        get => GetValue(ContentMinSizeProperty);
        set => SetValue(ContentMinSizeProperty, value);
    }

    public string? LayoutKey
    {
        get => GetValue(LayoutKeyProperty);
        set => SetValue(LayoutKeyProperty, value);
    }

    /// <summary>Raised (debounced) after a pane was resized, collapsed or opened.</summary>
    public event EventHandler<RoutedEventArgs>? LayoutChanged
    {
        add => AddHandler(LayoutChangedEvent, value);
        remove => RemoveHandler(LayoutChangedEvent, value);
    }

    public IEnumerable<RiceDockPane> Panes => Children.OfType<RiceDockPane>();

    /// <summary>The sizes and collapsed states of the panes with a <see cref="RiceDockPane.LayoutId"/>, as JSON.</summary>
    public string SaveLayout() => JsonSerializer.Serialize(CaptureState(), DockLayoutContext.Default.RiceDockLayoutState);

    /// <summary>Applies a layout from <see cref="SaveLayout"/>; unknown panes are skipped. False when the text cannot be read.</summary>
    public bool RestoreLayout(string json)
    {
        RiceDockLayoutState? state;
        try
        {
            state = JsonSerializer.Deserialize(json, DockLayoutContext.Default.RiceDockLayoutState);
        }
        catch (JsonException)
        {
            return false;
        }
        if (state is null)
            return false;
        ApplyState(state);
        return true;
    }

    /// <summary>Opens every pane at its starting size.</summary>
    public void ResetLayout()
    {
        foreach (var pane in Panes)
        {
            pane.IsCollapsed = false;
            pane.Size = pane.DefaultSize;
        }
    }

    internal RiceDockLayoutState CaptureState() => new()
    {
        Panes = Panes.Where(p => p.LayoutId is not null)
            .ToDictionary(p => p.LayoutId!, p => new RiceDockPaneState(Math.Round(p.Size, 1), p.IsCollapsed)),
    };

    internal void ApplyState(RiceDockLayoutState state)
    {
        _restoring = true;
        try
        {
            foreach (var pane in Panes)
            {
                if (pane.LayoutId is not { } id || !state.Panes.TryGetValue(id, out var saved))
                    continue;
                if (double.IsFinite(saved.Size) && saved.Size > 0)
                    pane.Size = ClampSize(saved.Size, pane.MinSize, pane.MaxSize);
                pane.IsCollapsed = saved.IsCollapsed && pane.CanCollapse;
            }
        }
        finally
        {
            _restoring = false;
        }
    }

    internal static double ClampSize(double size, double min, double max) =>
        Math.Max(0, Math.Min(Math.Max(size, min), Math.Max(max, min)));

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        if (Store() is { } store && store.Load() is { } state)
            ApplyState(state);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_saveTimer?.IsEnabled == true)
            SaveNow();
    }

    internal void OnPaneChanged()
    {
        if (_restoring)
            return;
        _saveTimer ??= new DispatcherTimer(SaveDelay, DispatcherPriority.Background, (_, _) => SaveNow());
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        _saveTimer?.Stop();
        Store()?.Save(CaptureState());
        RaiseEvent(new RoutedEventArgs(LayoutChangedEvent));
    }

    private DockLayoutStore? Store() =>
        string.IsNullOrWhiteSpace(LayoutKey) ? null : new DockLayoutStore(ThemeManager.Instance.Options.AppInfo.AppId, LayoutKey);

    /// <summary>The largest size <paramref name="pane"/> can take now without squeezing the content below its minimum.</summary>
    internal double MaxExtentFor(RiceDockPane pane)
    {
        var size = Bounds.Size;
        double used = 0;
        var horizontal = pane.Dock is Dock.Left or Dock.Right;
        foreach (var other in Panes.Where(p => p != pane && p.IsVisible && (p.Dock is Dock.Left or Dock.Right) == horizontal))
            used += horizontal ? other.Bounds.Width : other.Bounds.Height;
        return Math.Max(0, (horizontal ? size.Width : size.Height) - used - ContentMinSize);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rest = new Rect(availableSize);
        double usedWidth = 0, usedHeight = 0, needWidth = 0, needHeight = 0;
        foreach (var child in Children)
        {
            if (child is not RiceDockPane pane || !pane.IsVisible)
                continue;
            var horizontal = pane.Dock is Dock.Left or Dock.Right;
            var extent = PaneExtent(pane, horizontal ? rest.Width : rest.Height, measure: true, rest);
            if (horizontal)
            {
                needHeight = Math.Max(needHeight, usedHeight + pane.DesiredSize.Height);
                usedWidth += extent;
                rest = new Rect(0, 0, Math.Max(0, rest.Width - extent), rest.Height);
            }
            else
            {
                needWidth = Math.Max(needWidth, usedWidth + pane.DesiredSize.Width);
                usedHeight += extent;
                rest = new Rect(0, 0, rest.Width, Math.Max(0, rest.Height - extent));
            }
        }
        var content = new Size();
        foreach (var child in Children)
        {
            if (child is RiceDockPane)
                continue;
            child.Measure(rest.Size);
            content = new Size(Math.Max(content.Width, child.DesiredSize.Width), Math.Max(content.Height, child.DesiredSize.Height));
        }
        return new Size(
            Math.Max(needWidth, usedWidth + content.Width),
            Math.Max(needHeight, usedHeight + content.Height));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var rest = new Rect(finalSize);
        foreach (var child in Children)
        {
            if (child is not RiceDockPane pane || !pane.IsVisible)
                continue;
            var horizontal = pane.Dock is Dock.Left or Dock.Right;
            var extent = PaneExtent(pane, horizontal ? rest.Width : rest.Height, measure: false, rest);
            switch (pane.Dock)
            {
                case Dock.Left:
                    pane.Arrange(new Rect(rest.X, rest.Y, extent, rest.Height));
                    rest = new Rect(rest.X + extent, rest.Y, Math.Max(0, rest.Width - extent), rest.Height);
                    break;
                case Dock.Right:
                    pane.Arrange(new Rect(rest.Right - extent, rest.Y, extent, rest.Height));
                    rest = new Rect(rest.X, rest.Y, Math.Max(0, rest.Width - extent), rest.Height);
                    break;
                case Dock.Top:
                    pane.Arrange(new Rect(rest.X, rest.Y, rest.Width, extent));
                    rest = new Rect(rest.X, rest.Y + extent, rest.Width, Math.Max(0, rest.Height - extent));
                    break;
                default:
                    pane.Arrange(new Rect(rest.X, rest.Bottom - extent, rest.Width, extent));
                    rest = new Rect(rest.X, rest.Y, rest.Width, Math.Max(0, rest.Height - extent));
                    break;
            }
        }
        foreach (var child in Children)
        {
            if (child is not RiceDockPane)
                child.Arrange(rest);
        }
        return finalSize;
    }

    // An open pane takes its size, but leaves the content its minimum; a collapsed one is as wide as its strip.
    private double PaneExtent(RiceDockPane pane, double available, bool measure, Rect rest)
    {
        var horizontal = pane.Dock is Dock.Left or Dock.Right;
        if (pane.IsCollapsed)
        {
            if (measure)
                pane.Measure(horizontal ? new Size(double.PositiveInfinity, rest.Height) : new Size(rest.Width, double.PositiveInfinity));
            return Math.Min(horizontal ? pane.DesiredSize.Width : pane.DesiredSize.Height, available);
        }
        var extent = PaneExtent(pane.Size, pane.MinSize, pane.MaxSize, available, ContentMinSize);
        if (measure)
            pane.Measure(horizontal ? new Size(extent, rest.Height) : new Size(rest.Width, extent));
        return extent;
    }

    internal static double PaneExtent(double size, double min, double max, double available, double contentMin)
    {
        var wanted = ClampSize(size, min, max);
        if (double.IsInfinity(available))
            return wanted;
        return Math.Max(0, Math.Min(wanted, available - contentMin));
    }
}

public sealed class RiceDockLayoutState
{
    public Dictionary<string, RiceDockPaneState> Panes { get; init; } = [];
}

public sealed record RiceDockPaneState(double Size, bool IsCollapsed);

/// <summary>Persists a dock layout to <c>$XDG_CONFIG_HOME/&lt;appId&gt;/layouts/&lt;key&gt;.json</c>.</summary>
internal sealed class DockLayoutStore(string appId, string key)
{
    private readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appId, "layouts", SafeFileName(key) + ".json");

    private static string SafeFileName(string key) =>
        string.Concat(key.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '/' or '\\' ? '_' : c));

    public RiceDockLayoutState? Load()
    {
        try
        {
            if (!File.Exists(_path))
                return null;
            using var stream = File.OpenRead(_path);
            return JsonSerializer.Deserialize(stream, DockLayoutContext.Default.RiceDockLayoutState);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void Save(RiceDockLayoutState state)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var stream = File.Create(_path);
            JsonSerializer.Serialize(stream, state, DockLayoutContext.Default.RiceDockLayoutState);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the layout is not worth crashing the app.
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(RiceDockLayoutState))]
internal sealed partial class DockLayoutContext : JsonSerializerContext;
