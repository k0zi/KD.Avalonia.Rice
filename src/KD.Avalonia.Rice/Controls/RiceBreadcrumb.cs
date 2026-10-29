using System.Collections;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Path of the current location (e.g. Settings › Theme › Colors): one <see cref="RiceBreadcrumbItem"/> per item,
/// separated by chevrons. The last item is the current location; the others are clickable and raise
/// <see cref="ItemClicked"/> (and run <see cref="Command"/> with the item). With <see cref="TrimOnClick"/> a click
/// also removes the items after the clicked one, i.e. navigates back to it.
/// </summary>
public class RiceBreadcrumb : ItemsControl
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<RiceBreadcrumb, ICommand?>(nameof(Command));

    public static readonly StyledProperty<bool> TrimOnClickProperty =
        AvaloniaProperty.Register<RiceBreadcrumb, bool>(nameof(TrimOnClick));

    public static readonly RoutedEvent<RiceBreadcrumbItemClickedEventArgs> ItemClickedEvent =
        RoutedEvent.Register<RiceBreadcrumb, RiceBreadcrumbItemClickedEventArgs>(nameof(ItemClicked), RoutingStrategies.Bubble);

    public RiceBreadcrumb()
    {
        ItemsView.CollectionChanged += OnItemsChanged;
    }

    /// <summary>Run with the clicked item as parameter.</summary>
    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Removes the items after the clicked one (from <c>Items</c>, or from an <c>ItemsSource</c> that is a
    /// modifiable <see cref="IList"/>). Default false.
    /// </summary>
    public bool TrimOnClick
    {
        get => GetValue(TrimOnClickProperty);
        set => SetValue(TrimOnClickProperty, value);
    }

    public event EventHandler<RiceBreadcrumbItemClickedEventArgs>? ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    protected override Type StyleKeyOverride => typeof(RiceBreadcrumb);

    protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey) =>
        new RiceBreadcrumbItem();

    protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey) =>
        NeedsContainer<RiceBreadcrumbItem>(item, out recycleKey);

    protected override void ContainerForItemPreparedOverride(Control container, object? item, int index)
    {
        base.ContainerForItemPreparedOverride(container, item, index);
        if (container is RiceBreadcrumbItem crumb)
            crumb.SetPosition(index == 0, index == ItemCount - 1);
    }

    protected override void ContainerIndexChangedOverride(Control container, int oldIndex, int newIndex)
    {
        base.ContainerIndexChangedOverride(container, oldIndex, newIndex);
        if (container is RiceBreadcrumbItem crumb)
            crumb.SetPosition(newIndex == 0, newIndex == ItemCount - 1);
    }

    internal void OnItemClicked(RiceBreadcrumbItem container)
    {
        var index = IndexFromContainer(container);
        if (index < 0)
            return;
        var item = ItemFromContainer(container);
        var args = new RiceBreadcrumbItemClickedEventArgs(ItemClickedEvent, item, index);
        RaiseEvent(args);
        if (Command is { } command && command.CanExecute(item))
            command.Execute(item);
        if (TrimOnClick && !args.Handled)
            TrimAfter(index);
    }

    /// <summary>Removes the items after <paramref name="index"/>.</summary>
    public void TrimAfter(int index)
    {
        if (ItemsSource is IList { IsReadOnly: false, IsFixedSize: false } source)
        {
            for (var i = source.Count - 1; i > index; i--)
                source.RemoveAt(i);
        }
        else if (ItemsSource is null)
        {
            for (var i = Items.Count - 1; i > index; i--)
                Items.RemoveAt(i);
        }
    }

    // Adding or removing items moves the "last" mark; the containers are updated after the change is applied.
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            foreach (var container in GetRealizedContainers().OfType<RiceBreadcrumbItem>())
            {
                var index = IndexFromContainer(container);
                container.SetPosition(index == 0, index == ItemCount - 1);
            }
        });
}

public class RiceBreadcrumbItemClickedEventArgs(RoutedEvent routedEvent, object? item, int index) : RoutedEventArgs(routedEvent)
{
    /// <summary>The clicked item (the data item, or the <see cref="RiceBreadcrumbItem"/> declared in XAML).</summary>
    public object? Item { get; } = item;

    public int Index { get; } = index;
}
