using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>Raises <see cref="Changed"/> when it or anything below it changes; the charts redraw on it.</summary>
internal interface IChartChangeSource
{
    event EventHandler? Changed;
}

/// <summary>Base of the chart data classes: property changes, plus <see cref="Changed"/> for any change below.</summary>
public abstract class ChartModel : INotifyPropertyChanged, IChartChangeSource
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when a property of this object, or anything in its collections, changes.</summary>
    public event EventHandler? Changed;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        RaiseChanged();
        return true;
    }

    /// <summary>A collection whose changes are changes of this object.</summary>
    protected ChartCollection<T> Track<T>(ChartCollection<T> collection)
    {
        collection.Changed += (_, _) => RaiseChanged();
        return collection;
    }

    private protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

/// <summary>
/// Observable list of chart data. Raises <see cref="Changed"/> on its own changes and on changes of the
/// <see cref="ChartModel"/> items in it, so a chart follows edits anywhere in its data.
/// </summary>
public sealed class ChartCollection<T> : ObservableCollection<T>, IChartChangeSource
{
    public ChartCollection()
    {
    }

    public ChartCollection(IEnumerable<T> items)
    {
        foreach (var item in items)
            Add(item);
    }

    public event EventHandler? Changed;

    /// <summary>Replaces the whole content with one change notification.</summary>
    public void Reset(IEnumerable<T> items)
    {
        foreach (var item in Items)
            Unhook(item);
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
            Hook(item);
        }
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        base.OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    protected override void InsertItem(int index, T item)
    {
        base.InsertItem(index, item);
        Hook(item);
    }

    protected override void SetItem(int index, T item)
    {
        Unhook(this[index]);
        base.SetItem(index, item);
        Hook(item);
    }

    protected override void RemoveItem(int index)
    {
        Unhook(this[index]);
        base.RemoveItem(index);
    }

    protected override void ClearItems()
    {
        foreach (var item in Items)
            Unhook(item);
        base.ClearItems();
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void Hook(T item)
    {
        if (item is ChartModel model)
            model.Changed += OnItemChanged;
    }

    private void Unhook(T item)
    {
        if (item is ChartModel model)
            model.Changed -= OnItemChanged;
    }

    private void OnItemChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);
}
