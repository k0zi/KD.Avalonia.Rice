using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One card of a <see cref="RiceKanbanColumn"/>: a small raised surface showing its <c>Content</c> (with the column's
/// <c>ItemTemplate</c> or the board's <see cref="RiceKanbanBoard.CardTemplate"/>), with an optional colored
/// <see cref="Accent"/> stripe on the left.
/// Pseudo classes: <c>:selected</c>, <c>:dragging</c> (the place holder of a card being dragged), <c>:has-accent</c>.
/// </summary>
public class RiceKanbanCard : ContentControl
{
    public static readonly StyledProperty<IBrush?> AccentProperty =
        AvaloniaProperty.Register<RiceKanbanCard, IBrush?>(nameof(Accent));

    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<RiceKanbanCard, bool>(nameof(IsSelected));

    static RiceKanbanCard()
    {
        FocusableProperty.OverrideDefaultValue<RiceKanbanCard>(true);
    }

    /// <summary>Color of the stripe on the left edge (e.g. the priority or the type of the task); none when null.</summary>
    public IBrush? Accent
    {
        get => GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>Set by the board for the card of <see cref="RiceKanbanBoard.SelectedCard"/>.</summary>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceKanbanCard);

    internal void SetDragging(bool dragging) => PseudoClasses.Set(":dragging", dragging);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSelectedProperty)
            PseudoClasses.Set(":selected", IsSelected);
        else if (change.Property == AccentProperty)
            PseudoClasses.Set(":has-accent", Accent is not null);
    }
}
