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
| [ThemeSelector and ThemeDialog](theme-selector.md) | Theme picker with color previews, embedded or as a dialog |

All examples use these XML namespaces:

```xml
xmlns:rice="clr-namespace:KD.Avalonia.Rice.Controls;assembly=KD.Avalonia.Rice"
xmlns:icons="clr-namespace:Material.Icons;assembly=Material.Icons"
```

The [demo app](../samples/KD.Avalonia.Rice.Demo) shows every control:

```sh
dotnet run --project samples/KD.Avalonia.Rice.Demo
```
