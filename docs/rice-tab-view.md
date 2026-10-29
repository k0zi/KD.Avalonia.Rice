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

## Properties, methods and events

| Member | Default | Description |
|--------|---------|-------------|
| `ShowAddButton` | `false` | Shows the "+" button after the tabs. |
| `AddTabCommand` | `null` | Run by the "+" button (after `AddTabRequested`). |
| `AddTabRequested` event | | Raised by the "+" button. |
| `CanReorderTabs` | `true` | Lets the tabs be dragged to a new position (`Items` or a modifiable `IList` source). |
| `CanCloseTabs` | `true` | Default of `IsClosable` for the tabs (inherited). |
| `TabStripFooter` | `null` | Content at the right end of the tab strip, e.g. a menu button. |
| `TabCloseRequested` event | | See above. |
| `CloseTab(index)` | | Closes a tab as its close button would; `false` when cancelled. |

Ctrl+PageDown / Ctrl+PageUp select the next / previous tab.

`RiceTabViewItem`: `Header`, `Icon` (`IImage`, `MaterialIconKind`, string or control), `IsClosable`, `Close()`.
Tabs are 80–240 px wide; when they do not fit, the strip scrolls.

## Styling

`RiceTabViewItem` pseudo classes: `:selected`, `:closable`, `:dragging`. The tab strip uses `RiceTrackBrush`, the
content `Background` (`RiceSurfaceBrush`). Tooltips: `RiceStrings.TabClose`, `RiceStrings.TabAdd`.
