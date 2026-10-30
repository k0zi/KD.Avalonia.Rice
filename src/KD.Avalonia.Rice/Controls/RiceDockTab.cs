using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// The tab of a <see cref="RiceDockPane"/> in a <see cref="RiceDockGroupView"/>: on top for documents (with a close
/// button), at the bottom for tool windows. Pseudo classes: <c>:selected</c>, <c>:document</c>, <c>:closable</c>.
/// </summary>
public class RiceDockTab : TemplatedControl
{
    public static readonly StyledProperty<object?> HeaderProperty =
        AvaloniaProperty.Register<RiceDockTab, object?>(nameof(Header));

    public static readonly StyledProperty<object?> IconProperty =
        AvaloniaProperty.Register<RiceDockTab, object?>(nameof(Icon));

    internal RiceDockTab(RiceDockPane pane, bool document, bool selected)
    {
        Pane = pane;
        Header = pane.Header;
        Icon = pane.Icon;
        PseudoClasses.Set(":document", document);
        PseudoClasses.Set(":selected", selected);
        PseudoClasses.Set(":closable", document && pane.CanClose);
    }

    public RiceDockPane Pane { get; }

    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceDockTab);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_CloseButton") is { } close)
        {
            ToolTip.SetTip(close, RiceStrings.DockClose);
            AutomationProperties.SetName(close, RiceStrings.DockClose);
            close.Click += (_, args) =>
            {
                args.Handled = true;
                Pane.Close();
            };
        }
    }
}
