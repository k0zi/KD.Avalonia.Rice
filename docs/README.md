# KD.Avalonia.Rice documentation

Usage guides for every control of the library. Start with [Getting started](getting-started.md), it sets up the
themes that every control relies on.

| Page | Contents |
|------|----------|
| [Getting started](getting-started.md) | `UseRice`, `RiceStyles`, options, themes, brushes, localization |
| [RiceWindow](rice-window.md) | Frameless window with the custom title bar, `AboutDialog` |
| [RiceSideMenu](rice-side-menu.md) | Navigation menu with icons, sub items, header, footer and collapse button |
| [RiceCard](rice-card.md) | Raised surface with header, actions, footer and shadow |
| [RiceGroupBox](rice-group-box.md) | Frame with the title on the top line |
| [RiceChat](rice-chat.md) | Chat view with bubbles or IRC like log and an input box |
| [RiceProgressRing](rice-progress-ring.md) | Circular progress, 0–100 % or indeterminate (spinning) |
| [RiceProgressBar](rice-progress-bar.md) | Horizontal progress bar, 0–100 % or indeterminate (sliding) |
| [RiceSpinner](rice-spinner.md) | Small busy indicator for buttons and status lines |
| [RiceLoadingOverlay](rice-loading-overlay.md) | Covers and blocks content while it loads, with ring and message |
| [Dialogs](rice-dialog.md) | `RiceDialog`: frameless message, question and custom view dialogs with the system icons |
| [Toasts](rice-toast.md) | `RiceToast`: self-closing info / success / warning / error notifications in a corner of the window |
| [RiceInfoBar](rice-info-bar.md) | Message bar in the page: info / success / warning / error, with action and close button |
| [RiceEmptyState](rice-empty-state.md) | Placeholder for a view without data: icon, title, description and buttons |
| [RiceSkeleton](rice-skeleton.md) | Shimmering placeholder in the shape of content that is still loading |
| [RiceBreadcrumb](rice-breadcrumb.md) | Path of the current location with clickable parent levels |
| [RiceSegmentedControl](rice-segmented-control.md) | Row of linked buttons, one of them selected |
| [RiceTabView](rice-tab-view.md) | Tabs with icons, close buttons, drag to reorder and a "+" button |
| [RiceRibbon](rice-ribbon.md) | Office like ribbon: tabs of command groups that collapse when narrow, minimizable, Alt key tips |
| [RiceCommandPalette](rice-command-palette.md) | Ctrl+Shift+P search over the commands and pages of the window |
| [RicePropertyGrid](rice-property-grid.md) | Edits the public properties of an object, grouped by category |
| [RiceTimeline](rice-timeline.md) | Events along a line, grouped under day headers |
| [RiceKanbanBoard](rice-kanban-board.md) | Columns of cards moved by drag and drop or the keyboard, with WIP limits |
| [RiceCodeEditor](rice-code-editor.md) | Text box with syntax colors, line numbers and editor keys (indent, comment, auto indent) |
| [RiceDockLayout](rice-dock-layout.md) | Resizable, collapsible side panes around the content; saves the layout |
| [ThemeSelector and ThemeDialog](theme-selector.md) | Theme picker with color previews, embedded or as a dialog |
| [UML diagrams](rice-uml.md) | `UmlDiagramView`: all 14 UML diagram types, read from and written to PlantUML (`.puml`) |
| [Markdown and Mermaid](rice-markdown.md) | `MarkdownView`: CommonMark + GFM with colored code and diagrams; `MermaidView` for Mermaid diagrams |
| [Charts](rice-charts.md) | 23 chart controls (column, bar, line, pie, gantt, gauge, …) from a typed model, also as Markdown `chart` blocks |

All examples use these XML namespaces:

```xml
xmlns:rice="clr-namespace:KD.Avalonia.Rice.Controls;assembly=KD.Avalonia.Rice"
xmlns:icons="clr-namespace:Material.Icons;assembly=Material.Icons"
```

The UML, Markdown and chart controls have their own namespaces:

```xml
xmlns:uml="clr-namespace:KD.Avalonia.Rice.Controls.Uml;assembly=KD.Avalonia.Rice"
xmlns:md="clr-namespace:KD.Avalonia.Rice.Controls.Markdowns;assembly=KD.Avalonia.Rice"
xmlns:charts="clr-namespace:KD.Avalonia.Rice.Controls.Charts;assembly=KD.Avalonia.Rice"
```

The dialogs are used from code, namespace `KD.Avalonia.Rice.Dialogs`; the toasts too, namespace
`KD.Avalonia.Rice.Notifications`.

The [demo app](../samples/KD.Avalonia.Rice.Demo) shows every control:

```sh
dotnet run --project samples/KD.Avalonia.Rice.Demo
```
