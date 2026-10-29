using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One tab of a <see cref="RiceTabView"/>: optional <c>Icon</c> (an <c>IImage</c>, a <c>MaterialIconKind</c>, a string or
/// any control), the <c>Header</c> and a close button.
/// Pseudo classes: <c>:closable</c>, <c>:dragging</c> (while being reordered), <c>:tearing</c> (dragged out of the strip).
/// </summary>
public class RiceTabViewItem : TabItem
{
    /// <summary>Shows the close button; defaults to the <see cref="RiceTabView.CanCloseTabs"/> of the view.</summary>
    public static readonly StyledProperty<bool> IsClosableProperty =
        RiceTabView.CanCloseTabsProperty.AddOwner<RiceTabViewItem>();

    public RiceTabViewItem() => PseudoClasses.Set(":closable", IsClosable);

    public bool IsClosable
    {
        get => GetValue(IsClosableProperty);
        set => SetValue(IsClosableProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceTabViewItem);

    /// <summary>Closes the tab through its <see cref="RiceTabView"/> (which may cancel it).</summary>
    public void Close() => (ItemsControl.ItemsControlFromItemContainer(this) as RiceTabView)?.OnCloseRequested(this);

    internal void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    internal void SetTearing(bool tearing) => PseudoClasses.Set(":tearing", tearing);

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_CloseButton") is { } close)
        {
            ToolTip.SetTip(close, RiceStrings.TabClose);
            AutomationProperties.SetName(close, RiceStrings.TabClose);
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
        if (change.Property == IsClosableProperty)
            PseudoClasses.Set(":closable", IsClosable);
    }
}
