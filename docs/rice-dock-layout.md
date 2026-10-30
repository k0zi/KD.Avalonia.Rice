# RiceDockLayout

Docking layout like Visual Studio's: tool windows and documents (`RiceDockPane`) in tab groups, split around a
document area. Everything can be rearranged with the mouse: drag a pane to another edge, next to another group, into
its tabs or out into a window of its own; hide tool windows to strips at the edges, close panes and show them again,
add new ones from code. The whole arrangement is saved and restored.

```xml
<rice:RiceDockLayout LayoutKey="main">
  <rice:RiceDockPane Name="Explorer" Header="Explorer" Dock="Left" Size="240"
                     Icon="{x:Static icons:MaterialIconKind.FileTreeOutline}">
    <TreeView ItemsSource="{Binding Files}" />
  </rice:RiceDockPane>
  <rice:RiceDockPane Name="Output" Header="Output" Dock="Bottom" Size="160" Group="bottom">
    <TextBlock Text="{Binding Log}" />
  </rice:RiceDockPane>
  <rice:RiceDockPane Name="Problems" Header="Problems" Dock="Bottom" Group="bottom">
    <ListBox ItemsSource="{Binding Problems}" />
  </rice:RiceDockPane>
  <rice:RiceDockPane Name="Properties" Header="Properties" Dock="Right" Size="280">
    <rice:RicePropertyGrid SelectedObject="{Binding Selected}" />
  </rice:RiceDockPane>
  <rice:RiceDockPane Id="doc:readme" Header="README.md" Dock="Document">
    <md:MarkdownView Markdown="{Binding Readme}" />
  </rice:RiceDockPane>
  <!-- Not a pane: shown in the document area while no document is open. -->
  <rice:RiceEmptyState Title="No open documents" />
</rice:RiceDockLayout>
```

## The starting layout

The declared panes start like the children of a `DockPanel`: the first takes its edge of the whole layout, the next one
its edge of what is left, and so on. Above, the explorer gets the whole left side, the output group the bottom of the
rest (also under the properties), the properties the right side above it. `Dock="Document"` panes are the tabs of the
document area in the middle. Panes with the same `Group` start as tabs of one group, at the place of the first of them.
`Size` is the width (left, right) or height (top, bottom) a tool window takes at its edge.

A child that is not a pane is the `EmptyContent`: shown in the document area while it has no documents.

## Rearranging with the mouse

- **Drag a title bar** to move the whole group (all its tabs), **drag a tab** to move only that pane. Along the tab
  strip a tab is reordered instead.
- While dragging, **dock guides** appear: an arrow at the middle of each edge of the layout (dock there, at the pane's
  `Size`), a compass over the group under the pointer (its four sides split it, the middle adds the panes as tabs) and,
  over a document group, four outer arrows that dock a tool window beside the whole document area. The area a drop
  would fill is shaded. Escape cancels.
- **Dropped anywhere else**, the panes float in a window of their own. Drag that window's title back onto a guide to dock
  it again; double click the title of a docked group to float it, of a floating one to dock it back where it was.
- The **splitters** between the groups resize them: tool windows keep their pixel size when the window is resized, the
  document area takes the rest.
- The **pin** of a tool window hides its group to a strip at its edge (auto hide): a button per pane, which slides the
  pane out over the content; a click elsewhere hides it again, its pin docks it back.
- The **chevron** opens the window position menu: Float / Dock, Dock as tabbed document, Auto hide, Close.
- **×** closes the pane (on documents: the tab's ×, or a middle click on the tab). Ctrl+PageDown / Ctrl+PageUp select
  the next / previous tab of the group with the focus.

The group with the focus is the active one: its title bar takes the accent color (`ActivePane`).

## Adding, closing and showing panes from code

```csharp
// A new document (or a tool window at an edge: AddPane(pane, DockSide.Right)).
Layout.AddPane(new RiceDockPane { Id = "doc:" + path, Header = Path.GetFileName(path), Dock = DockSide.Document,
                                  Content = new RiceCodeEditor { Text = File.ReadAllText(path), Language = "cs" } });

// Next to another pane, or as another tab of its group.
Layout.AddPane(searchPane, Output, DockPosition.Tab);

Explorer.Close();         // or Layout.ClosePane(Explorer); PaneClosing may cancel it
Explorer.Show();          // a closed pane comes back where it was; an open one comes to the front
Properties.Float();
Output.IsCollapsed = true; // auto hide (false docks it back)
```

`PaneClosing` is raised before a pane closes (`e.Pane`; set `e.Cancel` to keep it, e.g. after an "unsaved changes"
question), `PaneClosed` after. A closed pane is kept in `ClosedPanes`, so a "View" menu can show it again: list
`AllPanes` and call `Show()` / `Close()` by their `IsOpen`. `RemovePane(pane)` forgets a pane for good. Panes added to
or removed from `Children` at run time are added and removed the same way.

## Saving the layout

With `LayoutKey` the layout saves itself a moment after every change, to `~/.config/<AppId>/layouts/<key>.json` (the
`AppId` of `RiceOptions.AppInfo`), and restores it when it is loaded: the splits and their sizes, the groups, their
tabs and selected tab, the auto hidden panes, the floating windows with their places and sizes, and the closed panes.

Panes are saved by their `Id`, or their `Name` when there is no `Id`; panes without either are not saved. Panes the saved
layout does not know (new in this version of the app) start at their declared place; saved panes that no longer exist
are skipped. Panes added from code (e.g. documents) must be added before the layout is loaded – in the view's
constructor – to be placed by the saved layout.

```csharp
var json = Layout.SaveLayout();   // the whole arrangement as JSON
Layout.RestoreLayout(json);       // false when the text cannot be read
Layout.ResetLayout();             // every pane open at its declared place and size, no floating windows
```

`LayoutChanged` is raised (after the same short delay) when the arrangement changed.

## Properties and methods

`RiceDockLayout`:

| Member | Default | Description |
|--------|---------|-------------|
| `Children` | | The declared panes (and the empty content). |
| `EmptyContent` | first non pane child | Shown in the document area without documents. |
| `ContentMinSize` | `120` | Width and height the document area keeps when tool windows are resized. |
| `LayoutKey` | `null` | Saves and restores the layout under this key. |
| `ActivePane` | | The pane last clicked or focused. |
| `Panes` / `ClosedPanes` / `AllPanes` | | The open, the closed and all panes. |
| `AddPane(pane, side)`, `AddPane(pane, relativeTo, position)` | | Adds or shows a pane at a side, next to another pane or as its tab. |
| `ClosePane(pane)`, `ShowPane(pane)`, `ActivatePane(pane)`, `FloatPane(pane)`, `RemovePane(pane)` | | See above. |
| `SaveLayout()`, `RestoreLayout(json)`, `ResetLayout()` | | See above. |
| `LayoutChanged`, `PaneClosing`, `PaneClosed` events | | See above. |

`RiceDockPane` (a `HeaderedContentControl`):

| Property | Default | Description |
|----------|---------|-------------|
| `Header` | | Title on the title bar, tab and strip. |
| `Icon` | `null` | `IImage`, `MaterialIconKind`, string or control before the title. |
| `HeaderActions` | `null` | Content in the title bar while the pane is selected, e.g. small buttons. |
| `Dock` | `Left` | Starting place: `Left`, `Top`, `Right`, `Bottom` or `Document`. |
| `Group` | `null` | Panes with the same name start as tabs of one group. |
| `Size` | `240` | Width / height at an edge or slid out from a strip; follows the splitter. |
| `MinSize` / `MaxSize` | `80` / ∞ | Limits of `Size`. |
| `IsCollapsed` | `false` | Auto hidden to a strip (two way). |
| `CanCollapse`, `CanClose`, `CanFloat`, `CanMove` | `true` | Offer auto hide, close, float, dragging. |
| `Id` | `null` | Key in the saved layout; `Name` when not set. |
| `IsOpen`, `IsFloating` | | Read only: shown somewhere / in a floating window. |

Methods: `Close()`, `Show()`, `Float()`, `ToggleCollapsed()`.

## Styling

The parts are `RiceDockGroupView` (pseudo classes `:document`, `:floating`, `:flyout`, `:active`, `:has-tabs`,
`:empty`) and `RiceDockTab` (`:selected`, `:document`, `:closable`). The layout's background (`RiceTrackBrush`) shows
between the groups; groups use `RiceSurfaceBrush`, the active title bar `RiceAccentBrush`, the guides and the drop
preview `RiceSurfaceBrush`, `RiceAccentBrush` and `RiceSelectedRowBrush`. Texts: `RiceStrings.DockClose`,
`DockAutoHide`, `DockPin`, `DockOptions`, `DockFloat`, `DockDock`, `DockAsDocument`.
