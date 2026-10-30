# Getting started

## Setup

1. Install the package:

   ```sh
   dotnet add package KD.Avalonia.Rice
   ```

2. Configure Rice in `Program.cs`:

   ```csharp
   public static AppBuilder BuildAvaloniaApp() =>
       AppBuilder.Configure<App>()
           .UsePlatformDetect()
           .UseRice(options =>
           {
               options.AppInfo = new RiceAppInfo
               {
                   AppId = "my-app",
                   Name = "My App",
                   Author = "me",
                   Description = "What the app does.",
                   IconUri = "avares://MyApp/Assets/icon.png",
               };
               options.SupportedThemes = [BuiltInThemes.Ubuntu, BuiltInThemes.Nord, BuiltInThemes.Dracula];
               options.DefaultThemeId = "ubuntu";
           });
   ```

3. Add `RiceStyles` to `App.axaml`. It **replaces** `FluentTheme` (it includes it), so remove `<FluentTheme />`:

   ```xml
   <Application xmlns="https://github.com/avaloniaui"
                xmlns:rice="clr-namespace:KD.Avalonia.Rice;assembly=KD.Avalonia.Rice"
                RequestedThemeVariant="Default">
     <Application.Styles>
       <rice:RiceStyles />
     </Application.Styles>
   </Application>
   ```

4. Use [`RiceWindow`](rice-window.md) as the base of your windows.

## Options (`RiceOptions`)

| Property | Default | Description |
|----------|---------|-------------|
| `AppInfo` | entry assembly name | Application metadata, see below. |
| `SupportedThemes` | `BuiltInThemes.All` | Themes the app offers. With a single theme there is no theme choice and nothing is saved. |
| `DefaultThemeId` | first supported theme | Theme used until the user picks one. |
| `WindowCornerRadius` | `8` | Corner radius of every `RiceWindow`; `new CornerRadius(0)` gives square corners. |

`RiceAppInfo`:

| Property | Description |
|----------|-------------|
| `AppId` (required) | Settings folder name: the theme is saved to `<ApplicationData>/<AppId>/rice.json` (`~/.config/…` on Linux, `%APPDATA%\…` on Windows). |
| `Name` (required) | Window title and about dialog title. |
| `Author`, `Description` | Shown in the about dialog when set. |
| `Version` | Defaults to the entry assembly's informational version (without the `+commit` part). |
| `IconUri` | Asset URI of the window / about dialog icon, e.g. `avares://MyApp/Assets/icon.png`. |

## Themes

Built-in themes (`BuiltInThemes`): Ubuntu, Fedora, Manjaro, openSUSE, Nord, Solarized, Dracula, Gruvbox, Catppuccin,
Windows 11 – each with a light and a dark variant.

- **Light / dark** follows the system on startup. The title bar's light bulb button switches it for the session only
  (not saved). From code: `ThemeManager.Instance.ToggleVariant()`, or set `Application.RequestedThemeVariant`.
- **Theme**: `ThemeManager.Instance.CurrentTheme` (or `SetTheme("nord")`). It is saved when the app supports more than
  one theme. Let the user choose with the [ThemeSelector / ThemeDialog](theme-selector.md).
- `ThemeManager.Instance` implements `INotifyPropertyChanged`: `CurrentTheme` notifies on every change, `IsDark` when
  it is switched with `ToggleVariant()` (for system changes use `Application.ActualThemeVariantChanged`).

Custom theme: a palette needs four colors (accent, background, surface, foreground); the others are derived and can be
overridden with `init` properties.

```csharp
var ocean = new RiceTheme("ocean", "Ocean",
    Light: new RicePalette(Color.Parse("#0077B6"), Color.Parse("#F4F8FB"), Colors.White, Color.Parse("#1B263B"))
    {
        TitleBarBackground = Color.Parse("#E1ECF4"),
    },
    Dark: new RicePalette(Color.Parse("#48CAE4"), Color.Parse("#0D1B2A"), Color.Parse("#1B263B"), Color.Parse("#E0E1DD")));

options.SupportedThemes = [ocean, .. BuiltInThemes.All];
```

`RicePalette` derived colors: `AccentForeground`, `SurfaceAlt`, `ForegroundMuted`, `Border`, `TitleBarBackground`,
`TitleBarForeground`, `Danger`, `DangerForeground`, `Success`, `SuccessForeground`, `Warning`, `WarningForeground`.

## Brushes for your own controls

The current theme is exposed as resources; use them with `DynamicResource` so they follow theme and variant changes:

| Resource | Use |
|----------|-----|
| `RiceAccentBrush`, `RiceAccentForegroundBrush` | Accent color and the text on it |
| `RiceBackgroundBrush` | Window background |
| `RiceSurfaceBrush`, `RiceSurfaceAltBrush` | Panels, cards, alternate rows |
| `RiceForegroundBrush`, `RiceForegroundMutedBrush` | Text, secondary text |
| `RiceBorderBrush` | Separators and frames |
| `RiceTitleBarBackgroundBrush`, `RiceTitleBarForegroundBrush`, `RiceTitleBarHoverBrush` | Title bar and side menu |
| `RiceDangerBrush`, `RiceDangerForegroundBrush` | Destructive actions, errors, and the text on them |
| `RiceSuccessBrush`, `RiceSuccessForegroundBrush` | Success states and the text on them |
| `RiceWarningBrush`, `RiceWarningForegroundBrush` | Warnings and the text on them |
| `RiceOverlayBrush` | Translucent background color for layers over content (loading overlay) |
| `RiceBackdropBrush` | Dark translucent layer that dims the window behind a popup (command palette) |
| `RiceTrackBrush` | Recessed strips: tab strip, segmented control, property grid description |
| `RiceHoverBrush`, `RiceSelectedRowBrush` | Row under the pointer, selected row (lists, property grid, command palette) |
| `RiceSkeletonBrush`, `RiceSkeletonHighlightBrush` | Loading placeholders and the band sweeping over them |
| `RiceDiffAddedBrush`, `RiceDiffRemovedBrush`, `RiceDiffAddedWordBrush`, `RiceDiffRemovedWordBrush`, `RiceDiffAddedMarkBrush`, `RiceDiffRemovedMarkBrush`, `RiceDiffEmptyBrush` | Added / removed lines and words of the [diff view](rice-diff.md) |
| `RiceSchedulerGridBrush`, `RiceSchedulerOffHoursBrush`, `RiceSchedulerTodayBrush`, `RiceSchedulerNowBrush` | Half hour lines, hours outside the working day, today, the current time of the [scheduler](rice-scheduler.md) |

```xml
<Border Background="{DynamicResource RiceSurfaceBrush}" BorderBrush="{DynamicResource RiceBorderBrush}" BorderThickness="1">
  <TextBlock Foreground="{DynamicResource RiceForegroundMutedBrush}" Text="Secondary text" />
</Border>
```

Standard Fluent controls (buttons, text boxes, …) are themed automatically. `Classes="accent"` on a `Button` gives the
accent colored button.

## Localization

Every user visible text of the library is in `RiceStrings`. Assign them at startup, before the first window opens:

```csharp
RiceStrings.About = "Névjegy";
RiceStrings.Close = "Bezárás";
RiceStrings.CollapseMenu = "Menü összecsukása";
RiceStrings.ChatPlaceholder = "Írj egy üzenetet…";
```
