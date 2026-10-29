using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Material.Icons;
using Material.Icons.Avalonia;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Shows and edits the public properties of <see cref="SelectedObject"/>, like the property window of form designers:
/// grouped by <c>[Category]</c> (or sorted by name), with a search box and the <c>[Description]</c> of the selected row
/// below. Editors by type: check box (bool), drop-down (enum), numeric box (numbers, limited by <c>[Range]</c>),
/// date picker (DateTime), text box (string, Color as hex, anything a <see cref="TypeConverter"/> reads from text);
/// other values are shown read-only. Objects implementing <see cref="INotifyPropertyChanged"/> update the grid.
/// </summary>
public class RicePropertyGrid : TemplatedControl
{
    public static readonly StyledProperty<object?> SelectedObjectProperty =
        AvaloniaProperty.Register<RicePropertyGrid, object?>(nameof(SelectedObject));

    public static readonly StyledProperty<bool> IsCategorizedProperty =
        AvaloniaProperty.Register<RicePropertyGrid, bool>(nameof(IsCategorized), true,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string?> FilterProperty =
        AvaloniaProperty.Register<RicePropertyGrid, string?>(nameof(Filter),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<RicePropertyGrid, bool>(nameof(ShowToolbar), true);

    public static readonly StyledProperty<bool> ShowDescriptionProperty =
        AvaloniaProperty.Register<RicePropertyGrid, bool>(nameof(ShowDescription), true);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<RicePropertyGrid, bool>(nameof(IsReadOnly));

    public static readonly StyledProperty<double> NameColumnWidthProperty =
        AvaloniaProperty.Register<RicePropertyGrid, double>(nameof(NameColumnWidth), 160);

    public static readonly DirectProperty<RicePropertyGrid, RicePropertyEntry?> SelectedEntryProperty =
        AvaloniaProperty.RegisterDirect<RicePropertyGrid, RicePropertyEntry?>(nameof(SelectedEntry), g => g.SelectedEntry);

    private static readonly Dictionary<Type, IReadOnlyList<RicePropertyEntry>> EntryCache = [];

    private readonly List<Row> _rows = [];
    private readonly List<(Control Header, Panel Body, string Category)> _groups = [];
    private readonly HashSet<string> _collapsedCategories = [];
    private RicePropertyEntry? _selectedEntry;
    private Row? _selectedRow;
    private Panel? _rowsHost;
    private TextBlock? _descriptionTitle;
    private TextBlock? _descriptionText;
    private RiceSegmentedControl? _viewSwitch;
    private TextBox? _searchBox;
    private INotifyPropertyChanged? _observed;

    /// <summary>The object whose properties are shown.</summary>
    public object? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    /// <summary>Groups the rows by category (default); false lists them sorted by name.</summary>
    public bool IsCategorized
    {
        get => GetValue(IsCategorizedProperty);
        set => SetValue(IsCategorizedProperty, value);
    }

    /// <summary>Text of the search box: only the properties whose name contains it are shown.</summary>
    public string? Filter
    {
        get => GetValue(FilterProperty);
        set => SetValue(FilterProperty, value);
    }

    /// <summary>Shows the search box and the categorized / alphabetical switch. Default true.</summary>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <summary>Shows the name and description of the selected property below the rows. Default true.</summary>
    public bool ShowDescription
    {
        get => GetValue(ShowDescriptionProperty);
        set => SetValue(ShowDescriptionProperty, value);
    }

    /// <summary>Shows every property read-only.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    public double NameColumnWidth
    {
        get => GetValue(NameColumnWidthProperty);
        set => SetValue(NameColumnWidthProperty, value);
    }

    /// <summary>The property of the row that was clicked or focused last.</summary>
    public RicePropertyEntry? SelectedEntry
    {
        get => _selectedEntry;
        private set => SetAndRaise(SelectedEntryProperty, ref _selectedEntry, value);
    }

    /// <summary>Raised after an editor changed a property of <see cref="SelectedObject"/>.</summary>
    public event EventHandler<RicePropertyValueChangedEventArgs>? PropertyValueChanged;

    protected override Type StyleKeyOverride => typeof(RicePropertyGrid);

    /// <summary>Reads every value from <see cref="SelectedObject"/> again, e.g. after it changed without notification.</summary>
    public void RefreshValues()
    {
        foreach (var row in _rows)
            row.Refresh();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        _viewSwitch?.SelectionChanged -= OnViewSwitchChanged;
        _rowsHost = e.NameScope.Find<Panel>("PART_Rows");
        _descriptionTitle = e.NameScope.Find<TextBlock>("PART_DescriptionTitle");
        _descriptionText = e.NameScope.Find<TextBlock>("PART_DescriptionText");
        _searchBox = e.NameScope.Find<TextBox>("PART_SearchBox");
        _searchBox?.PlaceholderText = RiceStrings.PropertyGridSearch;
        _viewSwitch = e.NameScope.Find<RiceSegmentedControl>("PART_ViewSwitch");
        if (_viewSwitch is not null)
        {
            _viewSwitch.ItemsSource = new[]
            {
                new RiceSegmentedItem { Content = new MaterialIcon { Kind = MaterialIconKind.FormatListGroup, Width = 16, Height = 16 } },
                new RiceSegmentedItem { Content = new MaterialIcon { Kind = MaterialIconKind.SortAlphabeticalAscending, Width = 16, Height = 16 } },
            };
            ToolTip.SetTip((Control)_viewSwitch.Items[0]!, RiceStrings.PropertyGridCategorized);
            ToolTip.SetTip((Control)_viewSwitch.Items[1]!, RiceStrings.PropertyGridAlphabetical);
            _viewSwitch.SelectedIndex = IsCategorized ? 0 : 1;
            _viewSwitch.SelectionChanged += OnViewSwitchChanged;
        }
        Rebuild();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Observe(SelectedObject);
        foreach (var row in _rows)
            row.SetObserving(true);
    }

    // The grid must not be kept alive by the objects it shows.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
        foreach (var row in _rows)
            row.SetObserving(false);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedObjectProperty)
        {
            SelectedEntry = null;
            if (this.IsAttachedToVisualTree())
                Observe(SelectedObject);
            Rebuild();
        }
        else if (change.Property == IsCategorizedProperty)
        {
            _viewSwitch?.SelectedIndex = IsCategorized ? 0 : 1;
            Rebuild();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == NameColumnWidthProperty)
        {
            Rebuild();
        }
        else if (change.Property == FilterProperty)
        {
            ApplyFilter();
        }
        else if (change.Property == SelectedEntryProperty)
        {
            UpdateDescription();
        }
    }

    private void OnViewSwitchChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_viewSwitch is { SelectedIndex: >= 0 } viewSwitch)
            IsCategorized = viewSwitch.SelectedIndex == 0;
    }

    private void Observe(object? target)
    {
        _observed?.PropertyChanged -= OnObservedPropertyChanged;
        _observed = target as INotifyPropertyChanged;
        _observed?.PropertyChanged += OnObservedPropertyChanged;
    }

    private void OnObservedPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnObservedPropertyChanged(sender, e));
            return;
        }
        foreach (var row in _rows)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || row.Accessor.Name == e.PropertyName)
                row.Refresh();
        }
    }

    private void Rebuild()
    {
        if (_rowsHost is null)
            return;
        _rowsHost.Children.Clear();
        foreach (var row in _rows)
            row.SetObserving(false);
        _rows.Clear();
        _groups.Clear();
        _selectedRow = null;
        if (SelectedObject is not { } target)
        {
            UpdateDescription();
            return;
        }

        var entries = Entries(target.GetType());
        var groups = IsCategorized
            ? entries.GroupBy(e => e.Category).Select(g => (Category: g.Key, Entries: g.ToList()))
            : [(Category: "", Entries: entries.OrderBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList())];

        foreach (var (category, groupEntries) in groups)
        {
            var body = new StackPanel();
            foreach (var entry in groupEntries)
            {
                var accessor = Accessor.ForProperty(entry, () => target, null, false, null);
                var row = new Row(this, accessor, 0, new HashSet<object>(ReferenceEqualityComparer.Instance) { target });
                _rows.Add(row);
                body.Children.Add(row.Container);
            }
            if (IsCategorized)
            {
                var header = CreateCategoryHeader(category, body);
                _rowsHost.Children.Add(header);
                _groups.Add((header, body, category));
            }
            _rowsHost.Children.Add(body);
        }
        ApplyFilter();
        UpdateDescription();
    }

    private static IReadOnlyList<RicePropertyEntry> Entries(Type type)
    {
        lock (EntryCache)
        {
            if (!EntryCache.TryGetValue(type, out var entries))
                EntryCache[type] = entries = RicePropertyEntry.FromType(type);
            return entries;
        }
    }

    private Control CreateCategoryHeader(string category, Panel body)
    {
        var chevron = new MaterialIcon { Kind = MaterialIconKind.ChevronDown, Width = 16, Height = 16 };
        var header = new Button
        {
            Classes = { "rice-category" },
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                Children = { chevron, new TextBlock { Text = category, FontWeight = FontWeight.SemiBold } },
            },
        };
        void Update()
        {
            var collapsed = _collapsedCategories.Contains(category);
            chevron.Kind = collapsed ? MaterialIconKind.ChevronRight : MaterialIconKind.ChevronDown;
            body.IsVisible = !collapsed && body.Children.Any(c => c.IsVisible);
        }
        header.Click += (_, _) =>
        {
            if (!_collapsedCategories.Remove(category))
                _collapsedCategories.Add(category);
            Update();
        };
        header.Tag = (Action)Update;
        return header;
    }

    private void ApplyFilter()
    {
        var filter = Filter?.Trim();
        foreach (var row in _rows)
        {
            row.Container.IsVisible = string.IsNullOrEmpty(filter)
                                      || row.Accessor.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                      || row.Accessor.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var (header, body, _) in _groups)
        {
            header.IsVisible = body.Children.Any(c => c.IsVisible);
            (header.Tag as Action)?.Invoke();
        }
    }

    private void Select(Row row)
    {
        _selectedRow?.Header.Classes.Set("selected", false);
        _selectedRow = row;
        row.Header.Classes.Set("selected", true);
        SelectedEntry = row.Accessor.Entry;
        UpdateDescription();
    }

    /// <summary>Expands or collapses every expandable row (objects and collections), at most a few levels deep.</summary>
    public void ExpandAll(bool expanded = true, int depth = 1)
    {
        IEnumerable<Row> level = _rows;
        for (var i = 0; i < depth; i++)
        {
            foreach (var row in level)
                row.SetExpanded(expanded);
            level = level.SelectMany(r => r.Descendants).ToList();
        }
    }

    private void UpdateDescription()
    {
        var accessor = _selectedRow?.Accessor;
        _descriptionTitle?.Text = accessor is null ? null : accessor.Path.Contains('.') || accessor.Path.Contains('[') ? accessor.Path : accessor.DisplayName;
        _descriptionText?.Text = accessor?.Description;
        _descriptionText?.IsVisible = !string.IsNullOrEmpty(accessor?.Description);
    }

    private void OnValueChanged(Accessor accessor, object? oldValue, object? newValue) =>
        PropertyValueChanged?.Invoke(this, new RicePropertyValueChangedEventArgs(accessor.Entry, accessor.Path, oldValue, newValue));

    /// <summary>
    /// What a row shows and edits: a property of an object or an item of a list / dictionary. <see cref="Path"/> names it
    /// from the selected object, e.g. "Font.Size" or "Tags[2]".
    /// </summary>
    private sealed record Accessor(string Name, string DisplayName, string? Description, Type Type, bool IsReadOnly,
        double? Minimum, double? Maximum, RicePropertyEntry? Entry, string Path, Func<object?> Get, Action<object?> Set)
    {
        // A property of a value type is changed on a boxed copy, which is then written back to its owner.
        public static Accessor ForProperty(RicePropertyEntry entry, Func<object?> owner, Action<object?>? writeBack,
            bool ownerReadOnly, string? parentPath) => new(
            entry.Name, entry.DisplayName, entry.Description, entry.PropertyType,
            entry.IsReadOnly || ownerReadOnly, entry.Minimum, entry.Maximum, entry,
            parentPath is null ? entry.Name : $"{parentPath}.{entry.Name}",
            () => owner() is { } o ? entry.GetValue(o) : null,
            value =>
            {
                if (owner() is not { } o)
                    return;
                entry.SetValue(o, value);
                writeBack?.Invoke(o);
            });

        public static Accessor ForItem(string key, Type type, bool isReadOnly, string parentPath, Func<object?> get,
            Action<object?> set) =>
            new(key, key, null, type, isReadOnly, null, null, null, $"{parentPath}{key}", get, set);
    }

    /// <summary>
    /// One property: the name on the left, the editor on the right. Objects and collections without an editor can be
    /// expanded: their properties / items become child rows below.
    /// </summary>
    private sealed class Row
    {
        // Deeper levels and longer lists are not expanded: the grid is not meant to browse whole object graphs.
        private const int MaxDepth = 6;
        private const int MaxItems = 100;
        private const double IndentStep = 16;

        private readonly RicePropertyGrid _grid;
        private readonly Row? _parent;
        private readonly Accessor _accessor;
        private readonly int _depth;
        private readonly IReadOnlySet<object> _ancestors;
        private readonly Type _type;
        private readonly bool _isNullable;
        private readonly bool _isReadOnly;
        private readonly bool _canExpand;
        private readonly StackPanel _children = new() { IsVisible = false };
        private readonly List<Row> _childRows = [];
        private Action _refresh = () => { };
        private bool _updating;
        private ToggleButton? _expander;
        private object? _expandedValue;
        private int _expandedCount;
        private INotifyPropertyChanged? _observedObject;
        private INotifyCollectionChanged? _observedCollection;
        private bool _observing = true;

        public Row(RicePropertyGrid grid, Accessor accessor, int depth, IReadOnlySet<object> ancestors, Row? parent = null)
        {
            _grid = grid;
            _parent = parent;
            _accessor = accessor;
            _depth = depth;
            _ancestors = ancestors;
            // An item declared as object (e.g. of a List<object>) is edited as what it holds.
            var declared = accessor.Type == typeof(object) && SafeGet() is { } current ? current.GetType() : accessor.Type;
            _type = Nullable.GetUnderlyingType(declared) ?? declared;
            _isNullable = !declared.IsValueType || _type != declared;
            _isReadOnly = accessor.IsReadOnly || grid.IsReadOnly;

            var name = new TextBlock
            {
                Classes = { "rice-property-name" },
                Text = accessor.DisplayName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (!string.IsNullOrEmpty(accessor.Description))
                ToolTip.SetTip(name, accessor.Description);
            var editor = CreateEditor(out var isDisplayOnly);
            editor.Margin = new Thickness(0, 2, 4, 2);
            editor.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(editor, 1);

            _canExpand = isDisplayOnly && depth < MaxDepth && IsExpandableType(_type);
            var nameCell = new DockPanel
            {
                Margin = new Thickness((grid.IsCategorized ? 22 : 8) + depth * IndentStep - (_canExpand ? 18 : 0), 0, 8, 0),
            };
            if (_canExpand)
            {
                _expander = new ToggleButton
                {
                    Classes = { "rice-property-expander" },
                    Width = 18,
                    Height = 18,
                    Padding = new Thickness(0),
                    Focusable = false,
                    Content = new MaterialIcon { Kind = MaterialIconKind.ChevronRight, Width = 14, Height = 14 },
                };
                _expander.IsCheckedChanged += (_, _) => SetExpanded(_expander.IsChecked == true);
                DockPanel.SetDock(_expander, Dock.Left);
                nameCell.Children.Add(_expander);
            }
            nameCell.Children.Add(name);

            Header = new Grid
            {
                Classes = { "rice-property-row" },
                ColumnDefinitions = new ColumnDefinitions($"{grid.NameColumnWidth.ToString(CultureInfo.InvariantCulture)},*"),
                MinHeight = 32,
                Background = Brushes.Transparent,
                Children = { nameCell, editor },
            };
            Header.AddHandler(InputElement.PointerPressedEvent, (_, _) => grid.Select(this), handledEventsToo: true);
            Header.AddHandler(InputElement.GotFocusEvent, (_, _) => grid.Select(this));
            Container = new StackPanel { Children = { Header, _children } };
            Refresh();
        }

        public Accessor Accessor => _accessor;

        public Grid Header { get; }

        /// <summary>The row and its child rows.</summary>
        public StackPanel Container { get; }

        public bool IsExpanded => _expander?.IsChecked == true;

        public void SetExpanded(bool expanded)
        {
            if (_expander is null)
                return;
            _expander.IsChecked = expanded;
            ((MaterialIcon)_expander.Content!).Kind = expanded ? MaterialIconKind.ChevronDown : MaterialIconKind.ChevronRight;
            _children.IsVisible = expanded;
            if (expanded)
                BuildChildren();
            else
                ClearChildren();
        }

        /// <summary>The expanded rows below this one, at any depth.</summary>
        public IEnumerable<Row> Descendants => _childRows.SelectMany(r => r.Descendants.Prepend(r));

        public void Refresh()
        {
            _updating = true;
            try
            {
                _refresh();
            }
            finally
            {
                _updating = false;
            }
            var value = Value;
            _expander?.IsVisible = value is not null && !_ancestors.Contains(value);
            if (!IsExpanded)
                return;
            // A new object (or a list of another length) needs new child rows; else the values are read again.
            if (!ReferenceEquals(value, _expandedValue) || CountOf(value) != _expandedCount)
                BuildChildren();
            else
                foreach (var child in _childRows)
                    child.Refresh();
        }

        /// <summary>Follows the changes of the expanded objects and collections; off while the grid is not shown.</summary>
        public void SetObserving(bool observing)
        {
            _observing = observing;
            Observe(observing && IsExpanded ? _expandedValue : null);
            foreach (var child in _childRows)
                child.SetObserving(observing);
        }

        private void BuildChildren()
        {
            ClearChildren();
            var value = Value;
            _expandedValue = value;
            _expandedCount = CountOf(value);
            if (value is null || _ancestors.Contains(value))
                return;

            var ancestors = value.GetType().IsValueType ? _ancestors : new HashSet<object>(_ancestors, ReferenceEqualityComparer.Instance) { value };
            foreach (var accessor in ChildAccessors(value))
            {
                var row = new Row(_grid, accessor, _depth + 1, ancestors, this);
                _childRows.Add(row);
                _children.Children.Add(row.Container);
            }
            Observe(_observing ? value : null);
        }

        private void ClearChildren()
        {
            Observe(null);
            foreach (var child in _childRows)
                child.SetObserving(false);
            _childRows.Clear();
            _children.Children.Clear();
            _expandedValue = null;
        }

        private IEnumerable<Accessor> ChildAccessors(object value)
        {
            var path = _accessor.Path;
            switch (value)
            {
                case IDictionary dictionary:
                {
                    var keys = dictionary.Keys.Cast<object>().Take(MaxItems).ToList();
                    var valueType = ElementType(value.GetType(), 1) ?? typeof(object);
                    foreach (var key in keys)
                    {
                        var k = key;
                        yield return Accessor.ForItem($"[{Convert.ToString(k, CultureInfo.InvariantCulture)}]", valueType,
                            dictionary.IsReadOnly, path, () => dictionary.Contains(k) ? dictionary[k] : null, v => dictionary[k] = v);
                    }
                    break;
                }
                case IList list:
                {
                    var itemType = ElementType(value.GetType(), 0) ?? typeof(object);
                    for (var i = 0; i < Math.Min(list.Count, MaxItems); i++)
                    {
                        var index = i;
                        yield return Accessor.ForItem($"[{i}]", itemType, list.IsReadOnly, path,
                            () => index < list.Count ? list[index] : null, v => list[index] = v);
                    }
                    break;
                }
                case IEnumerable enumerable:
                {
                    // Not indexable: a read-only snapshot.
                    var items = enumerable.Cast<object?>().Take(MaxItems).ToList();
                    var itemType = ElementType(value.GetType(), 0) ?? typeof(object);
                    for (var i = 0; i < items.Count; i++)
                    {
                        var item = items[i];
                        yield return Accessor.ForItem($"[{i}]", itemType, true, path, () => item, _ => { });
                    }
                    break;
                }
                default:
                {
                    // A struct is a copy: changing one of its properties means writing the whole struct back.
                    var isStruct = value.GetType().IsValueType;
                    Func<object?> owner = isStruct ? () => Value : () => value;
                    Action<object?>? writeBack = isStruct ? v => TrySet(v) : null;
                    foreach (var entry in Entries(value.GetType()))
                        yield return Accessor.ForProperty(entry, owner, writeBack, isStruct && _isReadOnly, path);
                    break;
                }
            }
        }

        private void Observe(object? target)
        {
            _observedObject?.PropertyChanged -= OnChildPropertyChanged;
            _observedCollection?.CollectionChanged -= OnCollectionChanged;
            _observedObject = target as INotifyPropertyChanged;
            _observedCollection = target as INotifyCollectionChanged;
            _observedObject?.PropertyChanged += OnChildPropertyChanged;
            _observedCollection?.CollectionChanged += OnCollectionChanged;
        }

        private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e) => OnUiThread(() =>
        {
            foreach (var child in _childRows)
            {
                if (string.IsNullOrEmpty(e.PropertyName) || child.Accessor.Name == e.PropertyName)
                    child.Refresh();
            }
            RefreshSummary();
        });

        // The summary of an object (its ToString) or a collection (its count) changes with its contents; so do the
        // summaries of the rows above it.
        private void RefreshSummary()
        {
            _updating = true;
            try
            {
                _refresh();
            }
            finally
            {
                _updating = false;
            }
            _parent?.RefreshSummary();
        }

        // A replaced item keeps its row; added, removed or moved items need new rows (their keys / indexes change).
        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => OnUiThread(() =>
        {
            if (IsExpanded && e.Action == NotifyCollectionChangedAction.Replace)
            {
                foreach (var child in _childRows)
                    child.Refresh();
            }
            else if (IsExpanded)
            {
                BuildChildren();
            }
            RefreshSummary();
        });

        private static void OnUiThread(Action action)
        {
            if (Dispatcher.UIThread.CheckAccess())
                action();
            else
                Dispatcher.UIThread.Post(action);
        }

        private object? Value => SafeGet();

        private object? SafeGet()
        {
            try
            {
                return _accessor.Get();
            }
            catch (Exception e) when (e is TargetInvocationException or ArgumentException or InvalidOperationException)
            {
                return null;
            }
        }

        // False when the setter refused the value (it threw); the editor is then marked as invalid.
        private bool TrySet(object? value)
        {
            if (_updating)
                return true;
            var old = Value;
            // Editors may report the value they were just given (e.g. a deferred TextChanged): writing it back would
            // only raise change notifications, and a collection would rebuild the rows that write again.
            if (Equals(old, value))
                return true;
            try
            {
                _accessor.Set(value);
            }
            catch (Exception e) when (e is TargetInvocationException or ArgumentException or InvalidCastException
                                          or NotSupportedException or InvalidOperationException)
            {
                return false;
            }
            _grid.OnValueChanged(_accessor, old, value);
            _parent?.RefreshSummary();
            return true;
        }

        private static readonly Dictionary<Type, string> Aliases = new()
        {
            [typeof(string)] = "string", [typeof(object)] = "object", [typeof(bool)] = "bool", [typeof(int)] = "int",
            [typeof(long)] = "long", [typeof(double)] = "double", [typeof(float)] = "float", [typeof(decimal)] = "decimal",
            [typeof(byte)] = "byte", [typeof(short)] = "short", [typeof(char)] = "char",
        };

        private static int CountOf(object? value) => value is ICollection collection ? collection.Count : -1;

        // The type argument of IList<T> / IEnumerable<T> (index 0) or of IDictionary<K, V> (index 1 = V); arrays too.
        private static Type? ElementType(Type type, int argument)
        {
            if (type.IsArray)
                return type.GetElementType();
            var generic = type.GetInterfaces().Prepend(type)
                .Where(t => t.IsGenericType)
                .FirstOrDefault(t => t.GetGenericTypeDefinition() == (argument == 1 ? typeof(IDictionary<,>) : typeof(IEnumerable<>)));
            return generic?.GetGenericArguments()[argument];
        }

        private static bool IsExpandableType(Type type)
        {
            if (type.IsPrimitive || type.IsEnum || type.IsPointer || type == typeof(string) || type == typeof(object)
                || typeof(Delegate).IsAssignableFrom(type) || typeof(MemberInfo).IsAssignableFrom(type)
                || typeof(Task).IsAssignableFrom(type))
                return false;
            // Values written as text (Guid, TimeSpan, Uri, Version, …) are scalars, even if they have properties.
            var converter = TypeDescriptor.GetConverter(type);
            if (converter is not ExpandableObjectConverter && converter.CanConvertFrom(typeof(string)))
                return false;
            return typeof(IEnumerable).IsAssignableFrom(type) || Entries(type).Count > 0;
        }

        // "(3) List<string>" for collections (the count first, so trimming keeps it); the object's own text unless it
        // is just its type name.
        private static string? Summary(object? value)
        {
            switch (value)
            {
                case null:
                    return null;
                case ICollection collection:
                    return $"({collection.Count}) {FriendlyName(value.GetType())}";
                case IEnumerable when value is not string:
                    return FriendlyName(value.GetType());
            }
            var text = value.ToString();
            return text == value.GetType().ToString() ? FriendlyName(value.GetType()) : text;
        }

        private static string FriendlyName(Type type)
        {
            if (Aliases.TryGetValue(type, out var alias))
                return alias;
            if (type.IsArray)
                return FriendlyName(type.GetElementType()!) + "[]";
            if (!type.IsGenericType)
                return type.Name;
            var name = type.Name[..type.Name.IndexOf('`')];
            return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(FriendlyName))}>";
        }

        private Control CreateEditor(out bool isDisplayOnly)
        {
            isDisplayOnly = false;
            if (_type == typeof(bool))
                return BoolEditor();
            if (_type.IsEnum && !_type.IsDefined(typeof(FlagsAttribute)))
                return EnumEditor();
            if (IsNumeric(_type))
                return NumericEditor();
            if (_type == typeof(DateTime))
                return DateEditor();
            if (_type == typeof(Color))
                return TextEditor(v => v is Color c ? c.ToString() : "", text => Color.TryParse(text, out var c) ? c : Invalid, swatch: true);
            if (_type == typeof(string))
                return TextEditor(v => v as string ?? "", text => text);

            var converter = TypeDescriptor.GetConverter(_type);
            if (!_isReadOnly && _type != typeof(object) && converter.CanConvertFrom(typeof(string)) && converter.CanConvertTo(typeof(string)))
            {
                return TextEditor(
                    v => v is null ? "" : converter.ConvertToInvariantString(v) ?? "",
                    text =>
                    {
                        if (text.Length == 0 && _isNullable)
                            return null;
                        try
                        {
                            return converter.ConvertFromInvariantString(text);
                        }
                        catch (Exception e) when (e is FormatException or NotSupportedException or ArgumentException or OverflowException)
                        {
                            return Invalid;
                        }
                    });
            }

            var display = new TextBlock
            {
                Classes = { "rice-property-value" },
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 8, 0),
            };
            _refresh = () => display.Text = Summary(Value);
            isDisplayOnly = true;
            return display;
        }

        private static readonly object Invalid = new();

        private Control BoolEditor()
        {
            var box = new CheckBox { IsThreeState = _isNullable, IsEnabled = !_isReadOnly };
            box.IsCheckedChanged += (_, _) => TrySet(_isNullable ? box.IsChecked : box.IsChecked == true);
            _refresh = () => box.IsChecked = Value as bool?;
            return box;
        }

        private Control EnumEditor()
        {
            var box = new ComboBox
            {
                ItemsSource = Enum.GetValues(_type),
                IsEnabled = !_isReadOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            box.SelectionChanged += (_, _) =>
            {
                if (box.SelectedItem is not null)
                    TrySet(box.SelectedItem);
            };
            _refresh = () => box.SelectedItem = Value;
            return box;
        }

        private Control NumericEditor()
        {
            var integral = _type != typeof(double) && _type != typeof(float) && _type != typeof(decimal);
            var box = new NumericUpDown
            {
                Increment = 1,
                FormatString = integral ? "0" : "",
                IsReadOnly = _isReadOnly,
                ShowButtonSpinner = !_isReadOnly,
                AllowSpin = !_isReadOnly,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Minimum = ToDecimal(_accessor.Minimum) ?? MinOf(_type),
                Maximum = ToDecimal(_accessor.Maximum) ?? MaxOf(_type),
            };
            box.ValueChanged += (_, e) =>
            {
                if (e.NewValue is not { } value)
                {
                    if (_isNullable)
                        TrySet(null);
                    else
                        Refresh();
                    return;
                }
                object converted;
                try
                {
                    converted = Convert.ChangeType(value, _type, CultureInfo.InvariantCulture);
                }
                catch (OverflowException)
                {
                    Refresh();
                    return;
                }
                if (!TrySet(converted))
                    Refresh();
            };
            _refresh = () => box.Value = Value switch
            {
                double d when double.IsNaN(d) || double.IsInfinity(d) => null,
                float f when float.IsNaN(f) || float.IsInfinity(f) => null,
                IConvertible c => ToDecimal(c),
                _ => null,
            };
            return box;
        }

        private Control DateEditor()
        {
            var picker = new CalendarDatePicker { IsEnabled = !_isReadOnly, HorizontalAlignment = HorizontalAlignment.Stretch };
            picker.SelectedDateChanged += (_, _) =>
            {
                if (picker.SelectedDate is { } date)
                    TrySet(date.Date + ((Value as DateTime?)?.TimeOfDay ?? TimeSpan.Zero));
                else if (_isNullable)
                    TrySet(null);
            };
            _refresh = () => picker.SelectedDate = Value as DateTime?;
            return picker;
        }

        private Control TextEditor(Func<object?, string> format, Func<string, object?> parse, bool swatch = false)
        {
            var box = new TextBox { IsReadOnly = _isReadOnly, HorizontalAlignment = HorizontalAlignment.Stretch };
            box.TextChanged += (_, _) =>
            {
                if (_updating)
                    return;
                var value = parse(box.Text ?? "");
                var valid = value != Invalid && TrySet(value);
                box.Classes.Set("error", !valid);
            };
            // Typing writes the value as it goes; leaving the box shows the value as the object formats it.
            box.LostFocus += (_, _) => Refresh();

            Border? colorSwatch = null;
            _refresh = () =>
            {
                var value = Value;
                var text = format(value);
                if (box.Text != text)
                    box.Text = text;
                box.Classes.Set("error", false);
                colorSwatch?.Background = value is Color c ? new SolidColorBrush(c) : null;
            };
            if (!swatch)
                return box;

            colorSwatch = new Border
            {
                Width = 24,
                Height = 24,
                CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 6, 0),
                Classes = { "rice-color-swatch" },
            };
            var panel = new DockPanel { Children = { colorSwatch, box } };
            DockPanel.SetDock(colorSwatch, Dock.Left);
            return panel;
        }

        private static bool IsNumeric(Type type) => Type.GetTypeCode(type) is TypeCode.Byte or TypeCode.SByte
            or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64
            or TypeCode.UInt64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal && !type.IsEnum;

        private static decimal? ToDecimal(object? value)
        {
            try
            {
                return value is IConvertible c ? c.ToDecimal(CultureInfo.InvariantCulture) : null;
            }
            catch (OverflowException)
            {
                return null;
            }
        }

        private static decimal MinOf(Type type) => Type.GetTypeCode(type) switch
        {
            TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.UInt64 => 0,
            TypeCode.SByte => sbyte.MinValue,
            TypeCode.Int16 => short.MinValue,
            TypeCode.Int32 => int.MinValue,
            TypeCode.Int64 => long.MinValue,
            _ => decimal.MinValue,
        };

        private static decimal MaxOf(Type type) => Type.GetTypeCode(type) switch
        {
            TypeCode.Byte => byte.MaxValue,
            TypeCode.SByte => sbyte.MaxValue,
            TypeCode.Int16 => short.MaxValue,
            TypeCode.UInt16 => ushort.MaxValue,
            TypeCode.Int32 => int.MaxValue,
            TypeCode.UInt32 => uint.MaxValue,
            TypeCode.Int64 => long.MaxValue,
            TypeCode.UInt64 => ulong.MaxValue,
            _ => decimal.MaxValue,
        };
    }
}

public class RicePropertyValueChangedEventArgs(RicePropertyEntry? entry, string path, object? oldValue, object? newValue) : EventArgs
{
    /// <summary>The changed property; null for an item of a list or dictionary.</summary>
    public RicePropertyEntry? Entry { get; } = entry;

    /// <summary>Where the value is, from the selected object: "FontSize", "Font.Size", "Tags[2]".</summary>
    public string Path { get; } = path;

    public object? OldValue { get; } = oldValue;

    public object? NewValue { get; } = newValue;
}
