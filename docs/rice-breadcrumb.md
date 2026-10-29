# RiceBreadcrumb

Path of the current location – Settings › Appearance › Colors – with a chevron between the levels. The last item is the
current location (semi-bold, not clickable); the parent levels highlight under the pointer and raise `ItemClicked` when
clicked (or activated with Enter / Space).

```xml
<rice:RiceBreadcrumb TrimOnClick="True" ItemClicked="OnBreadcrumbClicked">
  <rice:RiceBreadcrumbItem Icon="{x:Static icons:MaterialIconKind.HomeOutline}" Content="Home" />
  <rice:RiceBreadcrumbItem Content="Settings" />
  <rice:RiceBreadcrumbItem Content="Appearance" />
  <rice:RiceBreadcrumbItem Content="Colors" />
</rice:RiceBreadcrumb>
```

From data – a path of folders, each shown with its `ToString()` or an `ItemTemplate`:

```xml
<rice:RiceBreadcrumb ItemsSource="{Binding Path}" Command="{Binding OpenFolderCommand}" TrimOnClick="True" />
```

With `TrimOnClick` a click on a parent level removes the levels after it – from `Items`, or from an `ItemsSource` that is
a modifiable `IList` such as an `ObservableCollection` – which is "navigate back" in one line. Without it, handle
`ItemClicked` / `Command` and change the path yourself.

## Properties and events

| Member | Default | Description |
|--------|---------|-------------|
| `Items` / `ItemsSource` | | The levels, root first. `RiceBreadcrumbItem`s or data items. |
| `Command` | `null` | Run with the clicked item as parameter. |
| `TrimOnClick` | `false` | Removes the levels after the clicked one. Set `Handled` in `ItemClicked` to skip it once. |
| `ItemClicked` event | | `e.Item` (the data item or the `RiceBreadcrumbItem`), `e.Index`. |
| `TrimAfter(index)` | | Removes the levels after `index`. |

`RiceBreadcrumbItem` has an optional `Icon` (`IImage`, `MaterialIconKind`, string or control) and is at most 240 px wide.

## Styling

`RiceBreadcrumbItem` pseudo classes: `:first` (no chevron), `:last` (current location), `:has-icon`. The items flow in a
`WrapPanel`, so a long path wraps instead of being cut; change `ItemsPanel` for a single line.
