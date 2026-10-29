# RicePropertyGrid

Shows and edits the public properties of an object, like the property window of form designers: grouped by category (or
sorted by name), with a search box on top and the description of the selected property below. Handy for settings pages,
debugging panels and editors.

```xml
<rice:RicePropertyGrid SelectedObject="{Binding Settings}" Width="380" />
```

```csharp
public sealed class EditorSettings : INotifyPropertyChanged
{
    [Category("Text"), Description("Height of the letters in pixels."), Range(8, 40)]
    public double FontSize { get; set { … } } = 14;

    [Category("Text"), DisplayName("Color"), Description("Hex color, e.g. #E95420.")]
    public Color TextColor { get; set { … } }

    [Display(Name = "Shown name", GroupName = "Card", Description = "Header of the card.", Order = 1)]
    public string Title { get; set { … } } = "";

    [Browsable(false)]
    public string Internal { get; set; } = "";
}
```

## What is shown

Every public instance property with a public getter, except indexers and properties marked `[Browsable(false)]` or
`[Display(AutoGenerateField = false)]`. Base class properties come first, then the declaration order (or
`[Display(Order)]`).

| Attribute | Effect |
|-----------|--------|
| `[DisplayName]`, `[Display(Name)]` | Name in the grid. Without it the property name is split into words: `FontSize` → "Font size". |
| `[Category]`, `[Display(GroupName)]` | Group; without it `RiceStrings.PropertyGridMisc` ("Misc"). |
| `[Description]`, `[Display(Description)]` | Shown below the rows when the property is selected, and as tooltip of the name. |
| `[ReadOnly(true)]`, `[Editable(false)]`, no public setter | Read-only row. |
| `[Range(min, max)]` | Limits of the numeric editor. |

## Editors

| Type | Editor |
|------|--------|
| `bool`, `bool?` | Check box (three-state for `bool?`) |
| `enum` | Drop-down; `[Flags]` enums use a text box (`Bold, Italic`) |
| Numbers (`int`, `double`, `decimal`, …, nullable too) | Numeric box with spin buttons, limited to the type's range or `[Range]` |
| `DateTime` | Date picker (the time of day is kept) |
| `string` | Text box |
| `Avalonia.Media.Color` | Text box (`#RRGGBB`, `#AARRGGBB` or a color name) with a swatch |
| Anything a `TypeConverter` reads from text (`TimeSpan`, `Guid`, `Uri`, …) | Text box |
| Objects, structs, collections, dictionaries | Expandable row: a summary (`ToString()` or "(3) List<string>"), and the properties / items as child rows below |
| Other types | Read-only text (`ToString()`) |

## Nested objects and collections

A property holding an object without an editor of its own gets a chevron: expanding it shows the object's properties as
child rows, indented, with the same editors and rules – also when the property itself is read-only (e.g.
`public AuthorInfo Author { get; }`).

- **Structs** are values: changing one of their properties writes the whole struct back to the owning property (which
  must be settable).
- **Lists** (`IList`, arrays) show one row per item, `[0]`, `[1]`, …, edited in place (unless the list is read-only).
  Items are not added or removed by the grid. Other enumerables are shown read-only.
- **Dictionaries** show one row per key, `[Width]`, `[Height]`, ….
- Objects implementing `INotifyPropertyChanged` and collections implementing `INotifyCollectionChanged` update their
  rows (and the summaries above them) when they change.
- Values written as text (`Guid`, `TimeSpan`, `Uri`, … – anything whose `TypeConverter` reads strings) are not expanded.
- At most 6 levels and 100 items per list are shown, and an object is not expanded inside itself (cycles).

`ExpandAll(expanded, depth)` expands or collapses the rows from code.

Text boxes write the value while typing; an invalid text (or a setter that throws) marks the box red and keeps the
old value. If the object implements `INotifyPropertyChanged`, changes made elsewhere show up in the grid right away;
otherwise call `RefreshValues()`.

## Properties, methods and events

| Member | Default | Description |
|--------|---------|-------------|
| `SelectedObject` | `null` | The object to show. |
| `IsCategorized` | `true` | Groups by category (collapsible); `false` sorts by name. Also switched by the toolbar. |
| `Filter` | `null` | Text of the search box: only matching property names are shown. |
| `ShowToolbar` | `true` | Search box and categorized / alphabetical switch. |
| `ShowDescription` | `true` | Name and description of the selected property below the rows. |
| `IsReadOnly` | `false` | Shows every property read-only. |
| `NameColumnWidth` | `160` | Width of the name column. |
| `SelectedEntry` | | The `RicePropertyEntry` of the selected row. |
| `RefreshValues()` | | Reads every value again. |
| `ExpandAll(expanded, depth)` | | Expands / collapses the object and collection rows, `depth` levels deep (default 1). |
| `PropertyValueChanged` event | | After an editor changed a value: `e.Path` ("FontSize", "Author.Name", "Tags[2]"), `e.Entry` (null for list and dictionary items), `e.OldValue`, `e.NewValue`. A change of a struct's property raises it for the struct too. |

`RicePropertyEntry.FromType(type)` returns the rows of a type (name, category, description, read-only, range) if you want
to build your own editor on the same rules.

## Styling

Rows: `Grid.rice-property-row` (`.selected`), expanders `ToggleButton.rice-property-expander`, names `TextBlock.rice-property-name`, read-only values
`TextBlock.rice-property-value`, category headers `Button.rice-category`, invalid text boxes `TextBox.error`. Brushes:
`RiceHoverBrush`, `RiceSelectedRowBrush`, `RiceTrackBrush` (description panel). Texts: `RiceStrings.PropertyGridSearch`,
`PropertyGridMisc`, `PropertyGridCategorized`, `PropertyGridAlphabetical`.
