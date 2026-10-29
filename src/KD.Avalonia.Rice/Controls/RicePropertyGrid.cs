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
    }

    // The grid must not be kept alive by the object it shows.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Observe(null);
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
            if (string.IsNullOrEmpty(e.PropertyName) || row.Entry.Name == e.PropertyName)
                row.Refresh();
        }
    }

    private void Rebuild()
    {
        if (_rowsHost is null)
            return;
        _rowsHost.Children.Clear();
        _rows.Clear();
        _groups.Clear();
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
                var row = new Row(this, entry, target);
                _rows.Add(row);
                body.Children.Add(row.Root);
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
            row.Root.IsVisible = string.IsNullOrEmpty(filter)
                                 || row.Entry.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                                 || row.Entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var (header, body, _) in _groups)
        {
            header.IsVisible = body.Children.Any(c => c.IsVisible);
            (header.Tag as Action)?.Invoke();
        }
    }

    private void Select(Row row)
    {
        foreach (var other in _rows)
            other.Root.Classes.Set("selected", other == row);
        SelectedEntry = row.Entry;
    }

    private void UpdateDescription()
    {
        _descriptionTitle?.Text = SelectedEntry?.DisplayName;
        _descriptionText?.Text = SelectedEntry?.Description;
        _descriptionText?.IsVisible = !string.IsNullOrEmpty(SelectedEntry?.Description);
    }

    private void OnValueChanged(RicePropertyEntry entry, object? oldValue, object? newValue) =>
        PropertyValueChanged?.Invoke(this, new RicePropertyValueChangedEventArgs(entry, oldValue, newValue));

    /// <summary>One property: the name on the left, the editor on the right.</summary>
    private sealed class Row
    {
        private readonly RicePropertyGrid _grid;
        private readonly object _target;
        private readonly Type _type;
        private readonly bool _isNullable;
        private readonly bool _isReadOnly;
        private Action _refresh = () => { };
        private bool _updating;

        public Row(RicePropertyGrid grid, RicePropertyEntry entry, object target)
        {
            _grid = grid;
            _target = target;
            Entry = entry;
            _type = Nullable.GetUnderlyingType(entry.PropertyType) ?? entry.PropertyType;
            _isNullable = !entry.PropertyType.IsValueType || _type != entry.PropertyType;
            _isReadOnly = entry.IsReadOnly || grid.IsReadOnly;

            var name = new TextBlock
            {
                Classes = { "rice-property-name" },
                Text = entry.DisplayName,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(grid.IsCategorized ? 22 : 8, 0, 8, 0),
            };
            if (!string.IsNullOrEmpty(entry.Description))
                ToolTip.SetTip(name, entry.Description);
            var editor = CreateEditor();
            editor.Margin = new Thickness(0, 2, 4, 2);
            editor.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(editor, 1);

            Root = new Grid
            {
                Classes = { "rice-property-row" },
                ColumnDefinitions = new ColumnDefinitions($"{grid.NameColumnWidth.ToString(CultureInfo.InvariantCulture)},*"),
                MinHeight = 32,
                Background = Brushes.Transparent,
                Children = { name, editor },
            };
            Root.AddHandler(InputElement.PointerPressedEvent, (_, _) => grid.Select(this), handledEventsToo: true);
            Root.AddHandler(InputElement.GotFocusEvent, (_, _) => grid.Select(this));
            Refresh();
        }

        public RicePropertyEntry Entry { get; }

        public Grid Root { get; }

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
        }

        private object? Value
        {
            get
            {
                try
                {
                    return Entry.GetValue(_target);
                }
                catch (TargetInvocationException)
                {
                    return null;
                }
            }
        }

        // False when the setter refused the value (it threw); the editor is then marked as invalid.
        private bool TrySet(object? value)
        {
            if (_updating)
                return true;
            var old = Value;
            try
            {
                Entry.SetValue(_target, value);
            }
            catch (Exception e) when (e is TargetInvocationException or ArgumentException or InvalidCastException)
            {
                return false;
            }
            if (!Equals(old, value))
                _grid.OnValueChanged(Entry, old, value);
            return true;
        }

        private Control CreateEditor()
        {
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
            _refresh = () => display.Text = Value?.ToString();
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
                Minimum = ToDecimal(Entry.Minimum) ?? MinOf(_type),
                Maximum = ToDecimal(Entry.Maximum) ?? MaxOf(_type),
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

public class RicePropertyValueChangedEventArgs(RicePropertyEntry entry, object? oldValue, object? newValue) : EventArgs
{
    public RicePropertyEntry Entry { get; } = entry;

    public object? OldValue { get; } = oldValue;

    public object? NewValue { get; } = newValue;
}
