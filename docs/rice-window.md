# RiceWindow

Frameless window with a custom title bar: the app icon and title on the left, then optional custom content, the
light / dark toggle, the theme settings and about buttons and the window controls (minimize, maximize, close) on the
right. Corners are rounded, the window can be moved by the title bar (double click maximizes) and resized at the edges.

```xml
<rice:RiceWindow xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:rice="clr-namespace:KD.Avalonia.Rice.Controls;assembly=KD.Avalonia.Rice"
                 x:Class="MyApp.MainWindow"
                 Width="980" Height="600"
                 ShowThemeSettingsButton="True" ShowAboutButton="True">
  <DockPanel>
    ...
  </DockPanel>
</rice:RiceWindow>
```

```csharp
public partial class MainWindow : RiceWindow
{
    public MainWindow() => InitializeComponent();
}
```

`Title` and the title bar icon come from `RiceOptions.AppInfo` (see [Getting started](getting-started.md)). `Title`
can be set per window; the title bar icon is `AppInfo.Icon`, so set `AppInfo` to change it for one window.

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `ShowThemeToggle` | `true` | Light bulb button that switches light / dark for the session. |
| `ShowThemeSettingsButton` | `false` | Gear button that opens the [`ThemeDialog`](theme-selector.md). |
| `ShowAboutButton` | `false` | Info button that opens the about dialog. |
| `TitleBarContent` | `null` | Extra content in the title bar, between the title and the buttons. |
| `AppInfo` | `RiceOptions.AppInfo` | Metadata shown by the about dialog. |
| `CornerRadius` | `RiceOptions.WindowCornerRadius` (8) | Window corners; `0` = square. |

Inherited `Window` properties (`CanResize`, `CanMinimize`, `CanMaximize`, `WindowState`, `MinWidth`, …) work as usual:
a non-resizable window has no resize grips, `CanMinimize="False"` / `CanMaximize="False"` hide the matching title bar
button (the latter also disables the double click maximize).

## Methods and events

| Member | Description |
|--------|-------------|
| `ShowThemeDialog()` | Opens the theme dialog modally (same as the gear button). Use it from your own settings page. |
| `RequestAbout()` | Opens the about dialog (same as the info button). |
| `ToggleMaximized()` | Maximizes / restores the window. |
| `AboutRequested` event | Raised before the built-in about dialog opens; set `e.Handled = true` to show your own. |

```csharp
public MainWindow()
{
    InitializeComponent();
    AboutRequested += (_, e) =>
    {
        e.Handled = true;
        new MyAboutWindow().ShowDialog(this);
    };
}
```

## Title bar content

```xml
<rice:RiceWindow.TitleBarContent>
  <TextBox Width="240" PlaceholderText="Search…" VerticalAlignment="Center" />
</rice:RiceWindow.TitleBarContent>
```

The empty part of the title bar stays draggable; interactive controls placed there (buttons, text boxes) receive the
clicks themselves.

## Rounded corners

- All windows: `options.WindowCornerRadius = new CornerRadius(12)` (or `0` for square corners).
- One window: `CornerRadius="0"` on the `RiceWindow`.
- Corners are square while maximized or full screen, and when the platform cannot draw transparent windows (e.g. X11
  without a compositor) – the window falls back automatically.

## AboutDialog

The built-in about dialog shows the icon, name, description, author and version from `RiceAppInfo`. It is a
`RiceWindow` itself, so it can also be opened directly:

```csharp
await new AboutDialog(ThemeManager.Instance.Options.AppInfo).ShowDialog(owner);
```

Its texts (`About`, `Author`, `Version`, `OK`) come from `RiceStrings`.

## Tips

- Subclasses keep the `RiceWindow` look automatically (the control theme is looked up for `RiceWindow`).
- Your own dialogs can derive from `RiceWindow` too; hide the buttons they do not need
  (`ShowThemeToggle="False"`, `CanResize="False"`).
