# Toasts

`RiceToast` (namespace `KD.Avalonia.Rice.Notifications`) shows toasts: short, non-blocking notifications stacked in a
corner of the window that close themselves. Each toast is a card tinted with the color of its kind, with a colored
stripe, an icon, an optional title, the message and a thin bar counting down the time until it closes.

```csharp
using KD.Avalonia.Rice.Notifications;

RiceToast.ShowSuccess("report.md was saved.");
RiceToast.ShowError("The server did not respond.", "Upload failed");
RiceToast.ShowWarning("Only 2 GB are left on the home partition.");
RiceToast.ShowInfo("3 files were moved to the trash.", "Deleted", new ToastAction("Undo", RestoreFiles));
```

Without an `owner` argument the active window of the application shows the toast (else its main window). Every
method can be called from any thread.

## Kinds

| `ToastKind` | Color | Icon | Default duration |
|-------------|-------|------|------------------|
| `Info` | The theme's accent color | Information | 5 s |
| `Success` | `RicePalette.Success` (green) | Check circle | 5 s |
| `Warning` | `RicePalette.Warning` (amber) | Alert triangle | 5 s |
| `Error` | `RicePalette.Danger` (red) | Alert circle | 8 s |

The colors come from the theme, so every built-in theme uses its own green, yellow and red (Nord's aurora colors,
Dracula's, Gruvbox's, …).

## Behavior

- The countdown pauses while the pointer is over the toasts, so a toast does not vanish while it is being read.
- The close button, Esc (while a button of the toast has the focus) or an action button closes a toast.
- At most `MaxItems` (5) toasts are shown; a new one closes the oldest.
- The newest toast is nearest to the edge: at the bottom of the stack in the bottom positions, at the top in the top ones.
- Toasts slide and fade in from their side; closing fades one out and the others slide into its place.
- Screen readers announce toasts as they appear (errors interrupt).

## Options

`RiceToast.Show(new ToastOptions { ... })` returns a `RiceToastHandle` like the shortcuts:

| Property | Default | Description |
|----------|---------|-------------|
| `Kind` | `Info` | Color and icon. |
| `Title`, `Message` | `null` | Texts; either can be left out. |
| `Content`, `ContentTemplate` | `null` | Custom content below the message: a control, or a view model with a template. |
| `Duration` | `null` | Time until the toast closes itself; `null` is the kind's default, `TimeSpan.Zero` keeps it until closed. |
| `ShowCloseButton` | `true` | The × button. |
| `ShowProgress` | `true` | The countdown bar. |
| `Action` | `null` | A `ToastAction` button (`Text` and an `ICommand` or an `Action`); clicking it closes the toast. |
| `OnClick` | `null` | Called when the toast itself is clicked; the toast closes then. |
| `OnClosed` | `null` | Called once the toast is closed. |

```csharp
var toast = RiceToast.Show(new ToastOptions
{
    Kind = ToastKind.Warning,
    Title = "Unsaved changes",
    Message = "Save before closing?",
    Duration = TimeSpan.Zero,
    Action = new ToastAction("Save", saveCommand),
});

await toast.Closed;      // completes when the toast is closed
toast.Close();           // closes it from code
RiceToast.CloseAll();    // closes every toast of the active window
```

## Position and host

`RiceWindow` has a built-in `RiceToastHost` over its content, below the title bar. `ToastPosition` sets its corner:
`TopLeft`, `TopCenter`, `TopRight`, `BottomLeft`, `BottomCenter`, `BottomRight` (default).

```xml
<rice:RiceWindow ToastPosition="TopRight" ... />
```

Other windows get a host in their overlay layer when they show their first toast. `RiceToastHost.For(window)`
returns the host of a window, for its `Position`, `MaxItems` (5) and `ToastWidth` (360).

## Colors

| Resource | Use |
|----------|-----|
| `RiceToastInfoBrush`, `RiceToastSuccessBrush`, `RiceToastWarningBrush`, `RiceToastErrorBrush` | Stripe, icon, countdown and action button frame |
| `RiceToast…BackgroundBrush` | The surface color tinted with the kind's color |
| `RiceToast…BorderBrush` | The frame, a stronger tint |
| `RiceToastShadow` | Shadow of the toasts |

`RicePalette.Success` / `Warning` (and `SuccessForeground` / `WarningForeground`, derived unless set) are also exposed
as `RiceSuccessBrush`, `RiceWarningBrush` and their foreground brushes for your own controls.

`RiceToastItem` can also be placed in XAML, e.g. in a preview; it then stays until closed:

```xml
<toast:RiceToastItem Kind="Success" Title="Saved" Message="report.md was saved." Width="320" />
```

with `xmlns:toast="clr-namespace:KD.Avalonia.Rice.Notifications;assembly=KD.Avalonia.Rice"`.

The tooltip of the close button is `RiceStrings.ToastClose`.
