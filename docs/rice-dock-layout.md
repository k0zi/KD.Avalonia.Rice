# RiceDockLayout

Layout of resizable, collapsible side panels around a content area, like the tool windows of an IDE. Every
`RiceDockPane` is docked to an edge and has a header bar (icon, title, actions, collapse button) and a splitter on its
inner edge; collapsed, it shrinks to a strip with its icon and title. The sizes and collapsed states can be saved and
restored, by hand or by themselves.

```xml
<rice:RiceDockLayout LayoutKey="main">
  <rice:RiceDockPane Name="Explorer" Dock="Left" Size="240" Header="EXPLORER"
                     Icon="{x:Static icons:MaterialIconKind.FileTreeOutline}">
    <TreeView ItemsSource="{Binding Files}" />
  </rice:RiceDockPane>
  <rice:RiceDockPane Name="Output" Dock="Bottom" Size="160" MinSize="60" Header="OUTPUT">
    <TextBlock Text="{Binding Log}" />
  </rice:RiceDockPane>
  <rice:RiceDockPane Name="Properties" Dock="Right" Size="280" Header="PROPERTIES">
    <rice:RicePropertyGrid SelectedObject="{Binding Selected}" />
  </rice:RiceDockPane>
  <!-- Not a pane: fills the rest. -->
  <rice:RiceCodeEditor Text="{Binding Code}" Language="cs" />
</rice:RiceDockLayout>
```

The panes take their edge in order, as in a `DockPanel`: above, the explorer gets the whole left side, the output the
bottom of what is left (also under the properties), the properties the right side above the output. Every other child
fills the rest. The content keeps at least `ContentMinSize` (120) in both directions: a splitter cannot be dragged
further, and when the window gets small the panes give way.

## Resizing and collapsing

- Drag the splitter on a pane's inner edge (it shows an accent line under the pointer). The size stays between
  `MinSize` and `MaxSize`.
- Double click the splitter: back to the size the pane started with (`DefaultSize`).
- The splitter can take the focus (Tab): the arrow keys resize by 10 px, with Ctrl by 100 px.
- The chevron in the header collapses the pane (`IsCollapsed`); a click on the strip opens it again. On the left and
  right edges the strip shows the title running down.

`Size` and `IsCollapsed` bind two way, so a view model can keep the layout as well.

## Saving the layout

With `LayoutKey` the layout saves itself, a moment after a change, to `~/.config/<AppId>/layouts/<key>.json` (the
`AppId` of `RiceOptions.AppInfo`), and comes back when the layout is loaded. Every pane is saved under its `Id`, or its
`Name` when there is no `Id`; panes without either are not saved. Unknown and missing panes are skipped, so adding a pane
in a new version keeps the saved state of the others.

From code:

```csharp
var json = Layout.SaveLayout();   // {"panes":{"Explorer":{"size":240,"isCollapsed":false},…}}
Layout.RestoreLayout(json);       // false when the text cannot be read
Layout.ResetLayout();             // every pane open at its starting size
```

`LayoutChanged` is raised (after the same short delay) when a pane was resized, collapsed or opened.

## Properties

`RiceDockLayout` (a `Panel`):

| Member | Default | Description |
|--------|---------|-------------|
| `ContentMinSize` | `120` | Width and height the content keeps. |
| `LayoutKey` | `null` | Saves and restores the layout under this key. |
| `Panes` | | The `RiceDockPane` children. |
| `SaveLayout()` / `RestoreLayout(json)` / `ResetLayout()` | | See above. |
| `LayoutChanged` event | | A pane was resized, collapsed or opened. |

`RiceDockPane` (a `HeaderedContentControl`):

| Property | Default | Description |
|----------|---------|-------------|
| `Dock` | `Left` | The edge: `Left`, `Top`, `Right`, `Bottom`. |
| `Size` | `240` | Width (left / right) or height (top / bottom) when open. |
| `MinSize` / `MaxSize` | `120` / ∞ | Limits of `Size`. |
| `IsCollapsed` | `false` | Shrinks the pane to a strip. |
| `CanCollapse` | `true` | Shows the collapse button. |
| `CanResize` | `true` | Shows the splitter. |
| `Header` | | Title in the header bar and on the strip. |
| `Icon` | `null` | `IImage`, `MaterialIconKind`, string or control before the title. |
| `HeaderActions` | `null` | Content in the header bar before the collapse button, e.g. small buttons. |
| `Id` | `null` | Key in the saved layout; `Name` when not set. |

`ToggleCollapsed()` opens / collapses a pane from code.

## Styling

`RiceDockPane` pseudo classes: `:left`, `:top`, `:right`, `:bottom`, `:collapsed`, `:resizing`. Panes use
`RiceSurfaceBrush` and a `RiceBorderBrush` line on the inner edge; the splitter line is `RiceAccentBrush`. Texts:
`RiceStrings.DockCollapse`, `RiceStrings.DockExpand`, `RiceStrings.DockResize`.
