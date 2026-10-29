using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Letter badge of a ribbon command while the key tips are shown (Alt).</summary>
public class RiceKeyTip : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RiceKeyTip, string?>(nameof(Text));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceKeyTip);
}

/// <summary>
/// Keyboard access of a <see cref="RiceRibbon"/>, like in Office: Alt (or F10) shows key tips on the tabs, a tab's key
/// selects it and shows the key tips of its commands, a command's key runs it. A collapsed group opens and shows the
/// key tips of its items. Escape goes back a level; a click, Alt again or another key closes them.
/// </summary>
internal sealed class RiceRibbonKeyTips
{
    private enum Level { None, Tabs, Items, Group }

    private sealed record Target(Control Control, string? KeyTip, string? Label, bool Centered, Action Activate);

    private sealed record Shown(string Tip, RiceKeyTip Badge, AdornerLayer Layer, Action Activate);

    private readonly RiceRibbon _ribbon;
    private readonly List<Shown> _shown = [];
    private readonly List<InputElement> _extraRoots = [];
    private TopLevel? _topLevel;
    private Level _level;
    private RiceRibbonGroup? _group;
    private string _prefix = "";
    private bool _altPending;
    private bool _suppressTextInput;

    public RiceRibbonKeyTips(RiceRibbon ribbon) => _ribbon = ribbon;

    public bool IsActive => _level != Level.None;

    public void Attach(TopLevel topLevel)
    {
        _topLevel = topLevel;
        Hook(topLevel);
        if (topLevel is WindowBase window)
            window.Deactivated += OnDeactivated;
    }

    public void Detach()
    {
        Hide();
        if (_topLevel is null)
            return;
        Unhook(_topLevel);
        if (_topLevel is WindowBase window)
            window.Deactivated -= OnDeactivated;
        _topLevel = null;
    }

    public void Show()
    {
        if (_ribbon.IsKeyTipsEnabled && _ribbon.IsEffectivelyVisible && _ribbon.IsEffectivelyEnabled)
            ShowLevel(Level.Tabs);
    }

    public void Hide()
    {
        ClearBadges();
        foreach (var root in _extraRoots)
            Unhook(root);
        _extraRoots.Clear();
        _level = Level.None;
        _group = null;
    }

    /// <summary>
    /// Key tips for the items: the explicit ones as given, the others from the letters of the label (word initials
    /// first), else "1".."9", "Y1".. – so that no key tip is the start of another one.
    /// </summary>
    internal static string[] Assign(IReadOnlyList<(string? KeyTip, string? Label)> items)
    {
        var result = new string?[items.Count];
        var used = new List<string>();
        bool IsFree(string tip) => !used.Any(u => u.StartsWith(tip, StringComparison.Ordinal) || tip.StartsWith(u, StringComparison.Ordinal));

        for (var i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(items[i].KeyTip))
                used.Add(result[i] = items[i].KeyTip!.Trim().ToUpperInvariant());
        }
        for (var i = 0; i < items.Count; i++)
        {
            if (result[i] is not null)
                continue;
            var tip = Candidates(items[i].Label).Concat(Fallbacks()).First(IsFree);
            used.Add(result[i] = tip);
        }
        return result!;
    }

    private static IEnumerable<string> Candidates(string? label)
    {
        if (string.IsNullOrEmpty(label))
            yield break;
        // Word initials: the letters and digits that follow anything else.
        for (var i = 0; i < label.Length; i++)
        {
            if (char.IsLetterOrDigit(label[i]) && (i == 0 || !char.IsLetterOrDigit(label[i - 1])))
                yield return char.ToUpperInvariant(label[i]).ToString();
        }
        foreach (var c in label.Where(char.IsLetterOrDigit))
            yield return char.ToUpperInvariant(c).ToString();
    }

    private static IEnumerable<string> Fallbacks()
    {
        for (var d = 1; d <= 9; d++)
            yield return d.ToString();
        foreach (var letter in "YZXQ")
        {
            for (var d = 1; d <= 9; d++)
                yield return $"{letter}{d}";
        }
    }

    private void ShowLevel(Level level, RiceRibbonGroup? group = null)
    {
        Hide();
        _level = level;
        _group = group;
        var targets = level switch
        {
            Level.Tabs => TabTargets(),
            Level.Items => ItemTargets(),
            Level.Group when group is not null => GroupItemTargets(group, []),
            _ => [],
        };
        targets = targets.Where(t => t.Control.IsEffectivelyVisible && t.Control.IsEffectivelyEnabled).ToList();
        var tips = Assign(targets.Select(t => (t.KeyTip, t.Label)).ToList());
        for (var i = 0; i < targets.Count; i++)
            AddBadge(targets[i], tips[i]);
    }

    // Shows the next level once the layout caught up (a tab selected, a popup or flyout opened).
    private void ShowLevelLater(Level level, RiceRibbonGroup? group = null)
    {
        ClearBadges();
        _level = level;
        Dispatcher.UIThread.Post(() =>
        {
            if (_level == level)
                ShowLevel(level, group);
        }, DispatcherPriority.Background);
    }

    private List<Target> TabTargets()
    {
        var targets = new List<Target>();
        if (_ribbon.ApplicationButton is { } appButton && _ribbon.ApplicationButtonContent is not null)
            targets.Add(new Target(appButton, _ribbon.ApplicationButtonKeyTip, _ribbon.ApplicationButtonContent as string, true,
                () => { Hide(); Invoke(appButton); }));
        for (var i = 0; i < _ribbon.ItemCount; i++)
        {
            if (_ribbon.ContainerFromIndex(i) is not RiceRibbonTab tab)
                continue;
            var index = i;
            targets.Add(new Target(tab, RiceRibbon.GetKeyTip(tab), tab.Header as string, true, () =>
            {
                _ribbon.SelectedIndex = index;
                if (_ribbon.IsMinimized)
                    _ribbon.OpenPopup();
                ShowLevelLater(Level.Items);
            }));
        }
        return targets;
    }

    private List<Target> ItemTargets()
    {
        var targets = new List<Target>();
        if (_ribbon.SelectedIndex < 0 || _ribbon.ContainerFromIndex(_ribbon.SelectedIndex) is not RiceRibbonTab tab)
            return targets;
        foreach (var child in tab.Groups)
        {
            if (child is not RiceRibbonGroup group)
                AddItemTargets(child, targets);
            else if (group.IsCollapsed && group.CollapsedButton is { } button)
                targets.Add(new Target(button, RiceRibbon.GetKeyTip(group), group.Header as string, true, () =>
                {
                    Invoke(button);
                    ShowLevelLater(Level.Group, group);
                }));
            else
                GroupItemTargets(group, targets);
        }
        return targets;
    }

    private List<Target> GroupItemTargets(RiceRibbonGroup group, List<Target> targets)
    {
        foreach (var item in group.Items.OfType<Control>())
            AddItemTargets(item, targets);
        if (group.ShowLauncher && group.LauncherButton is { } launcher)
            targets.Add(new Target(launcher, group.LauncherKeyTip, group.Header as string, false, () => { Hide(); Invoke(launcher); }));
        return targets;
    }

    // Panels (e.g. a row of small buttons) are looked into; anything that takes input gets a key tip.
    private void AddItemTargets(Control control, List<Target> targets)
    {
        if (control is Panel panel)
        {
            foreach (var child in panel.Children)
                AddItemTargets(child, targets);
            return;
        }
        if (control is RiceRibbonSeparator || !control.Focusable && control is not Button)
            return;
        var label = control switch
        {
            ContentControl { Content: string content } => content,
            HeaderedSelectingItemsControl { Header: string header } => header,
            _ => ToolTip.GetTip(control) as string,
        };
        var centered = RiceRibbon.GetSize(control) == RiceRibbonItemSize.Large;
        targets.Add(new Target(control, RiceRibbon.GetKeyTip(control), label, centered, () => { Hide(); Invoke(control); }));
    }

    // Runs a button (its command, its flyout), toggles a check box, opens a combo box, focuses anything else.
    private static void Invoke(Control control)
    {
        var peer = ControlAutomationPeer.CreatePeerForElement(control);
        if (peer.GetProvider<IInvokeProvider>() is { } invoke)
        {
            invoke.Invoke();
        }
        else if (peer.GetProvider<IToggleProvider>() is { } toggle)
        {
            toggle.Toggle();
        }
        else if (peer.GetProvider<IExpandCollapseProvider>() is { } expand)
        {
            control.Focus(NavigationMethod.Tab);
            expand.Expand();
        }
        else
        {
            control.Focus(NavigationMethod.Tab);
        }
    }

    private void AddBadge(Target target, string tip)
    {
        if (AdornerLayer.GetAdornerLayer(target.Control) is not { } layer)
            return;
        var badge = new RiceKeyTip
        {
            Text = tip,
            HorizontalAlignment = target.Centered ? HorizontalAlignment.Center : HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = target.Centered ? new Thickness(0, 0, 0, -8) : new Thickness(2, 0, 0, -5),
        };
        AdornerLayer.SetAdornedElement(badge, target.Control);
        AdornerLayer.SetIsClipEnabled(badge, false);
        layer.Children.Add(badge);
        _shown.Add(new Shown(tip, badge, layer, target.Activate));

        // Keys typed into a popup or flyout (a window of its own, or an overlay with its own input root) must reach us too.
        HookExtra(TopLevel.GetTopLevel(target.Control));
        HookExtra(target.Control.FindAncestorOfType<OverlayPopupHost>());
    }

    private void ClearBadges()
    {
        foreach (var shown in _shown)
            shown.Layer.Children.Remove(shown.Badge);
        _shown.Clear();
        _prefix = "";
    }

    private void Back()
    {
        switch (_level)
        {
            case Level.Group:
                _group?.HideFlyout();
                ShowLevelLater(Level.Items);
                break;
            case Level.Items:
                _ribbon.ClosePopup();
                ShowLevel(Level.Tabs);
                break;
            default:
                Hide();
                break;
        }
    }

    private void Type(string key)
    {
        var typed = _prefix + key;
        var matches = _shown.Where(s => s.Tip.StartsWith(typed, StringComparison.Ordinal)).ToList();
        if (matches.Count == 0)
            return;
        if (matches.Count == 1 && matches[0].Tip == typed)
        {
            matches[0].Activate();
            return;
        }
        _prefix = typed;
        foreach (var shown in _shown)
            shown.Badge.IsVisible = shown.Tip.StartsWith(typed, StringComparison.Ordinal);
    }

    private static string? KeyText(KeyEventArgs e)
    {
        if (e.KeySymbol is { Length: 1 } symbol && char.IsLetterOrDigit(symbol[0]))
            return symbol.ToUpperInvariant();
        return e.Key switch
        {
            >= Key.A and <= Key.Z => e.Key.ToString(),
            >= Key.D0 and <= Key.D9 => ((char)('0' + (e.Key - Key.D0))).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((char)('0' + (e.Key - Key.NumPad0))).ToString(),
            _ => null,
        };
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var isAlt = e.Key == Key.LeftAlt;
        if (!IsActive)
        {
            _altPending = isAlt && e.KeyModifiers == KeyModifiers.Alt;
            if (e is { Key: Key.F10, KeyModifiers: KeyModifiers.None } && _ribbon.IsKeyTipsEnabled)
            {
                Show();
                e.Handled = IsActive;
            }
            return;
        }
        _altPending = isAlt;
        if (isAlt || e.Key is Key.LeftShift or Key.RightShift)
        {
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case Key.Escape:
                Back();
                break;
            case Key.F10:
                Hide();
                break;
            default:
                if (KeyText(e) is not { } text || e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift))
                {
                    // Arrows, Tab, shortcuts, …: the key tips go away and the key does its usual job.
                    Hide();
                    return;
                }
                _suppressTextInput = true;
                Type(text);
                break;
        }
        e.Handled = true;
    }

    private void OnKeyUp(object? sender, KeyEventArgs e)
    {
        _suppressTextInput = false;
        if (e.Key != Key.LeftAlt || !_altPending)
            return;
        _altPending = false;
        if (IsActive)
            Hide();
        else
            Show();
        e.Handled = true;
    }

    // The letter of a key tip must not be typed into the focused text box as well.
    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (!IsActive && !_suppressTextInput)
            return;
        _suppressTextInput = false;
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _altPending = false;
        Hide();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        _altPending = false;
        Hide();
    }

    private void HookExtra(InputElement? root)
    {
        if (root is null || root == _topLevel || _extraRoots.Contains(root))
            return;
        _extraRoots.Add(root);
        Hook(root);
    }

    private void Hook(InputElement topLevel)
    {
        topLevel.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        topLevel.AddHandler(InputElement.KeyUpEvent, OnKeyUp, RoutingStrategies.Tunnel);
        topLevel.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        topLevel.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void Unhook(InputElement topLevel)
    {
        topLevel.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
        topLevel.RemoveHandler(InputElement.KeyUpEvent, OnKeyUp);
        topLevel.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
        topLevel.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
    }
}
