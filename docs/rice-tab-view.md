# RiceTabView

Tabs like a browser or an editor: each tab has an optional icon and a close button, the tabs can be reordered by
dragging, and an optional "+" button asks for a new tab. The selected tab carries an accent line and takes the color of
the content below it.

```xml
<rice:RiceTabView ShowAddButton="True" AddTabRequested="OnAddTab">
  <rice:RiceTabViewItem Header="Welcome" Icon="{x:Static icons:MaterialIconKind.HomeOutline}" IsClosable="False">
    <TextBlock Margin="16" Text="This tab cannot be closed." />
  </rice:RiceTabViewItem>
  <rice:RiceTabViewItem Header="README.md" Icon="{x:Static icons:MaterialIconKind.LanguageMarkdownOutline}">
    <md:MarkdownView Markdown="{Binding Readme}" />
  </rice:RiceTabViewItem>
</rice:RiceTabView>
```

From data, with an `ObservableCollection` (closing removes the item, dragging moves it):

```xml
<rice:RiceTabView ItemsSource="{Binding Documents}" SelectedItem="{Binding ActiveDocument}"
                  ShowAddButton="True" AddTabCommand="{Binding NewDocumentCommand}">
  <rice:RiceTabView.ItemTemplate>
    <DataTemplate x:DataType="vm:DocumentViewModel">
      <TextBlock Text="{Binding Title}" />
    </DataTemplate>
  </rice:RiceTabView.ItemTemplate>
  <rice:RiceTabView.ContentTemplate>
    <DataTemplate x:DataType="vm:DocumentViewModel">
      <TextBox Text="{Binding Text}" AcceptsReturn="True" />
    </DataTemplate>
  </rice:RiceTabView.ContentTemplate>
</rice:RiceTabView>
```

It is a `TabControl` underneath: `ItemsSource`, `SelectedItem`, `SelectedIndex`, `ItemTemplate` (the header) and
`ContentTemplate` work as usual.

## Closing

The close button, a middle click on the tab and Ctrl+W (on the selected tab, when the focus is in the view) close a tab:

1. `TabCloseRequested` is raised (`e.Item`, `e.Container`). Set `e.Cancel = true` to keep the tab (e.g. after an "unsaved
   changes" question), or `e.Handled = true` to remove it yourself.
2. Otherwise the item is removed from `Items`, or from an `ItemsSource` that is a modifiable `IList`.
3. When the selected tab closed, the tab on its right (else the one on its left) is selected, like in browsers.

## Tearing tabs off

With `CanTearOffTabs="True"` a tab dragged out of the tab strip (more than about 30 px) fades; dropped there, it:

- moves into the tab strip of another `RiceTabView` it was dropped on – in any window – if that view also has
  `CanTearOffTabs` and the same `TabGroup`; or
- gets a window of its own: a `RiceWindow` with a new tab view, placed under the pointer.

A window made this way closes when its last tab is closed or dragged away, so tabs can be docked back by dragging them
onto the original strip.

```xml
<rice:RiceTabView ItemsSource="{Binding Documents}" CanTearOffTabs="True" TabGroup="documents"
                  TearOffWindowCreated="OnTearOffWindowCreated" />
```

```csharp
// The new view copies the settings and templates and gets an empty collection of the same type as ItemsSource;
// event handlers are not copied – wire them here.
private void OnTearOffWindowCreated(object? sender, RiceTabWindowCreatedEventArgs e)
{
    e.View.AddTabRequested += OnAddTab;
    e.View.TabCloseRequested += OnTabCloseRequested;
}
```

`TabTearOffRequested` is raised first (`e.Item`, `e.ScreenPosition`, `e.TargetView`); set `e.Cancel` to keep the tab, or
`e.Handled` to place it yourself (e.g. into a window of your own). From code: `MoveTab(index, targetView, targetIndex)`
and `TearOff(index, screenPosition)`. Override `CreateTearOffView()` / `CreateTearOffWindow(view, position)` to change
what is created. Moving needs `Items` or a modifiable `IList` source on both sides, and an item the target collection
accepts (a typed collection refuses other types; the tab then stays).

## Properties, methods and events

| Member | Default | Description |
|--------|---------|-------------|
| `ShowAddButton` | `false` | Shows the "+" button after the tabs. |
| `AddTabCommand` | `null` | Run by the "+" button (after `AddTabRequested`). |
| `AddTabRequested` event | | Raised by the "+" button. |
| `CanReorderTabs` | `true` | Lets the tabs be dragged to a new position (`Items` or a modifiable `IList` source). |
| `CanCloseTabs` | `true` | Default of `IsClosable` for the tabs (inherited). |
| `TabStripFooter` | `null` | Content at the right end of the tab strip, e.g. a menu button. |
| `CanTearOffTabs` | `false` | Lets tabs be dragged out: into a new window or another view (see above). |
| `TabGroup` | `null` | Views exchange tabs only within the same group. |
| `TabTearOffRequested` event | | A tab was dropped outside the strip. |
| `TearOffWindowCreated` event | | A torn off tab got a window (`e.Window`, `e.View`), before it is shown. |
| `MoveTab(index, target, targetIndex)` | | Moves a tab into another view; `false` when it cannot. |
| `TearOff(index, position)` | | Moves a tab into a new window and shows it. |
| `TabCloseRequested` event | | See above. |
| `CloseTab(index)` | | Closes a tab as its close button would; `false` when cancelled. |

Ctrl+PageDown / Ctrl+PageUp select the next / previous tab.

`RiceTabViewItem`: `Header`, `Icon` (`IImage`, `MaterialIconKind`, string or control), `IsClosable`, `Close()`.
Tabs are 80–240 px wide; when they do not fit, the strip scrolls.

## Styling

`RiceTabViewItem` pseudo classes: `:selected`, `:closable`, `:dragging`, `:tearing` (dragged out of the strip). The tab strip uses `RiceTrackBrush`, the
content `Background` (`RiceSurfaceBrush`). Tooltips: `RiceStrings.TabClose`, `RiceStrings.TabAdd`.
