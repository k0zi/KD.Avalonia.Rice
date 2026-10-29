using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KD.Avalonia.Rice.Dialogs;

namespace KD.Avalonia.Rice.Controls;

/// <summary>One result of the <see cref="RiceCommandPalette"/>: the command and the title characters the query matched.</summary>
public sealed record RiceCommandMatch(RiceCommandItem Command, int Score, IReadOnlyList<int> Highlights)
{
    public bool IsEnabled => Command.CanInvoke();
}

/// <summary>
/// Search box over a list of <see cref="RiceCommandItem"/>s, like the command palette of code editors: typing filters
/// and ranks the commands (fuzzy, matched characters highlighted), the arrow keys move the selection, Enter runs the
/// selected command and Escape closes. Recently used commands come first.
/// Usually shown over a window with <see cref="Show"/> (a <see cref="RiceWindow"/> opens it with Ctrl+Shift+P),
/// but it can also be placed in a layout.
/// </summary>
public class RiceCommandPalette : TemplatedControl
{
    public static readonly StyledProperty<IEnumerable<RiceCommandItem>?> CommandsProperty =
        AvaloniaProperty.Register<RiceCommandPalette, IEnumerable<RiceCommandItem>?>(nameof(Commands));

    public static readonly StyledProperty<string?> QueryProperty =
        AvaloniaProperty.Register<RiceCommandPalette, string?>(nameof(Query),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<RiceCommandPalette, string?>(nameof(Placeholder));

    public static readonly StyledProperty<int> MaxResultsProperty =
        AvaloniaProperty.Register<RiceCommandPalette, int>(nameof(MaxResults), 50);

    public static readonly DirectProperty<RiceCommandPalette, IReadOnlyList<RiceCommandMatch>> MatchesProperty =
        AvaloniaProperty.RegisterDirect<RiceCommandPalette, IReadOnlyList<RiceCommandMatch>>(nameof(Matches), p => p.Matches);

    public static readonly DirectProperty<RiceCommandPalette, RiceCommandMatch?> SelectedMatchProperty =
        AvaloniaProperty.RegisterDirect<RiceCommandPalette, RiceCommandMatch?>(nameof(SelectedMatch),
            p => p.SelectedMatch, (p, v) => p.SelectedMatch = v);

    private const int MaxRecent = 8;
    private static readonly ConditionalWeakTable<TopLevel, Overlay> Overlays = new();

    private readonly List<RiceCommandItem> _recent = [];
    private IReadOnlyList<RiceCommandMatch> _matches = [];
    private RiceCommandMatch? _selectedMatch;
    private TextBox? _searchBox;
    private ListBox? _list;

    public RiceCommandPalette()
    {
        // Tunnel: the search box would use the arrow keys and Enter itself.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public IEnumerable<RiceCommandItem>? Commands
    {
        get => GetValue(CommandsProperty);
        set => SetValue(CommandsProperty, value);
    }

    /// <summary>Text of the search box.</summary>
    public string? Query
    {
        get => GetValue(QueryProperty);
        set => SetValue(QueryProperty, value);
    }

    /// <summary>Shown in the empty search box; defaults to <see cref="RiceStrings.CommandPalettePlaceholder"/>.</summary>
    public string? Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public int MaxResults
    {
        get => GetValue(MaxResultsProperty);
        set => SetValue(MaxResultsProperty, value);
    }

    /// <summary>The commands matching <see cref="Query"/>, best first.</summary>
    public IReadOnlyList<RiceCommandMatch> Matches
    {
        get => _matches;
        private set => SetAndRaise(MatchesProperty, ref _matches, value);
    }

    public RiceCommandMatch? SelectedMatch
    {
        get => _selectedMatch;
        set => SetAndRaise(SelectedMatchProperty, ref _selectedMatch, value);
    }

    /// <summary>The commands run from this palette, most recent first.</summary>
    public IReadOnlyList<RiceCommandItem> RecentCommands => _recent;

    /// <summary>Raised when a command is chosen, before it runs.</summary>
    public event EventHandler<RiceCommandItem>? CommandInvoked;

    /// <summary>Raised when the palette should close: a command was chosen or Escape was pressed.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Shows the palette over the window (default: the active one) and focuses its search box. Each window keeps its
    /// palette, so the recently used commands are remembered while the application runs.
    /// </summary>
    public static void Show(IEnumerable<RiceCommandItem> commands, TopLevel? owner = null, string? query = null)
    {
        if ((owner ?? RiceDialog.ActiveWindow()) is not { } topLevel)
            return;
        if (!Overlays.TryGetValue(topLevel, out var overlay))
        {
            overlay = new Overlay(topLevel);
            Overlays.Add(topLevel, overlay);
        }
        overlay.Show(commands, query);
    }

    /// <summary>Closes the palette of the window (default: the active one) and gives the focus back.</summary>
    public static void Hide(TopLevel? owner = null)
    {
        if ((owner ?? RiceDialog.ActiveWindow()) is { } topLevel && Overlays.TryGetValue(topLevel, out var overlay))
            overlay.Hide();
    }

    public static bool IsShown(TopLevel owner) => Overlays.TryGetValue(owner, out var overlay) && overlay.IsShown;

    /// <summary>Focuses the search box and selects its text.</summary>
    public void FocusSearchBox()
    {
        _searchBox?.Focus();
        _searchBox?.SelectAll();
    }

    /// <summary>Runs the command of <paramref name="match"/> (default: the selected one) unless it is disabled.</summary>
    public void Invoke(RiceCommandMatch? match = null)
    {
        if ((match ?? SelectedMatch) is not { IsEnabled: true, Command: var command })
            return;
        _recent.Remove(command);
        _recent.Insert(0, command);
        if (_recent.Count > MaxRecent)
            _recent.RemoveAt(_recent.Count - 1);

        CommandInvoked?.Invoke(this, command);
        CloseRequested?.Invoke(this, EventArgs.Empty);
        // After closing, so the command finds the focus back where it was (e.g. a command that pastes into it).
        Dispatcher.UIThread.Post(command.Invoke);
    }

    protected override Type StyleKeyOverride => typeof(RiceCommandPalette);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _list?.Tapped -= OnListTapped;
        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        _list = e.NameScope.Find<ListBox>("PART_List");
        _list?.Tapped += OnListTapped;
        if (e.NameScope.Find<TextBlock>("PART_Empty") is { } empty)
            empty.Text = RiceStrings.CommandPaletteNoResults;
        UpdatePlaceholder();
        Refresh();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommandsProperty || change.Property == QueryProperty || change.Property == MaxResultsProperty)
            Refresh();
        else if (change.Property == SelectedMatchProperty && SelectedMatch is not null)
            _list?.ScrollIntoView(SelectedMatch);
        else if (change.Property == PlaceholderProperty)
            UpdatePlaceholder();
    }

    private void UpdatePlaceholder() => _searchBox?.PlaceholderText = Placeholder ?? RiceStrings.CommandPalettePlaceholder;

    private void Refresh()
    {
        Matches = CommandSearch.Search(Commands ?? [], Query, _recent, Math.Max(MaxResults, 1));
        SelectedMatch = Matches.FirstOrDefault(m => m.IsEnabled) ?? Matches.FirstOrDefault();
        PseudoClasses.Set(":empty", Matches.Count == 0);
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: RiceCommandMatch match })
            Invoke(match);
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                Move(1);
                break;
            case Key.Up:
                Move(-1);
                break;
            case Key.PageDown:
                Move(PageSize());
                break;
            case Key.PageUp:
                Move(-PageSize());
                break;
            case Key.Enter:
                Invoke();
                break;
            case Key.Escape:
                CloseRequested?.Invoke(this, EventArgs.Empty);
                break;
            default:
                return;
        }
        e.Handled = true;
    }

    // Up from the first entry wraps to the last one and back; page steps stop at the ends.
    private void Move(int delta)
    {
        if (Matches.Count == 0)
            return;
        var index = SelectedMatch is null ? -1 : Matches.ToList().IndexOf(SelectedMatch);
        index = Math.Abs(delta) == 1
            ? (index + delta + Matches.Count) % Matches.Count
            : Math.Clamp(index + delta, 0, Matches.Count - 1);
        SelectedMatch = Matches[index];
    }

    private int PageSize()
    {
        var row = _list?.ContainerFromIndex(0)?.Bounds.Height ?? 0;
        return row > 0 && _list is not null ? Math.Max((int)(_list.Bounds.Height / row) - 1, 1) : 8;
    }

    /// <summary>The palette over a window: in its overlay layer, on a dimmed backdrop that closes it when clicked.</summary>
    private sealed class Overlay
    {
        private readonly TopLevel _topLevel;
        private readonly RiceCommandPalette _palette = new()
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(16, 56, 16, 16),
        };
        private readonly Panel _container = new();
        private IInputElement? _previousFocus;

        public Overlay(TopLevel topLevel)
        {
            _topLevel = topLevel;
            var backdrop = new Border();
            backdrop.Bind(Border.BackgroundProperty, backdrop.GetResourceObservable("RiceBackdropBrush"));
            backdrop.PointerPressed += (_, e) =>
            {
                e.Handled = true;
                Hide();
            };
            _container.Children.Add(backdrop);
            _container.Children.Add(_palette);
            _palette.CloseRequested += (_, _) => Hide();
        }

        public bool IsShown => _container.Parent is not null;

        public void Show(IEnumerable<RiceCommandItem> commands, string? query)
        {
            if (OverlayLayer.GetOverlayLayer(_topLevel) is not { } layer)
                return;
            if (!IsShown)
            {
                _previousFocus = _topLevel.FocusManager?.GetFocusedElement();
                Fit(layer);
                layer.PropertyChanged += OnLayerPropertyChanged;
                layer.Children.Add(_container);
            }
            _palette.Commands = commands.ToList();
            _palette.Query = query ?? "";
            // The template is applied on the next layout pass.
            Dispatcher.UIThread.Post(_palette.FocusSearchBox, DispatcherPriority.Loaded);
        }

        public void Hide()
        {
            if (_container.Parent is not OverlayLayer layer)
                return;
            layer.PropertyChanged -= OnLayerPropertyChanged;
            layer.Children.Remove(_container);
            if (_previousFocus is InputElement previous && previous.IsAttachedToVisualTree())
                previous.Focus();
            _previousFocus = null;
        }

        // The overlay layer is a canvas: the container is sized to it, so the backdrop covers the window.
        private void OnLayerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.BoundsProperty && sender is OverlayLayer layer)
                Fit(layer);
        }

        private void Fit(OverlayLayer layer) => (_container.Width, _container.Height) = (layer.Bounds.Width, layer.Bounds.Height);
    }
}
