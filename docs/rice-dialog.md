# Dialogs

`RiceDialog` (namespace `KD.Avalonia.Rice.Dialogs`) shows modal dialogs in a `RiceDialogWindow`: a frameless window
**without a title bar** – only the icon, the message, optional custom content and the buttons, in the theme's thin
frame with rounded corners. Drag it by its background; Enter clicks the default button, Esc the cancel button.

```csharp
using KD.Avalonia.Rice.Dialogs;

await RiceDialog.ShowInfoAsync("The file was saved.");
await RiceDialog.ShowErrorAsync($"Could not open {path}.");

if (await RiceDialog.AskAsync("Delete the selected files?"))
    DeleteFiles();

var result = await RiceDialog.ShowAsync("Save the changes?", DialogIcon.Warning, DialogButtons.YesNoCancel);
```

Without an `owner` argument the active window of the application owns the dialog (else its main window).

## Icons

`DialogIcon`: `None`, `Question`, `Information`, `Warning`, `Error`. The operating system's own icons are shown:

| Platform | Source |
|----------|--------|
| Windows | The shell's stock icons (`SHGetStockIconInfo`), at the size of the screen's scaling |
| Linux | The `dialog-question` / `-information` / `-warning` / `-error` PNG icons of the icon theme (GNOME: `gsettings`, KDE: `kdeglobals`), its inherited themes and hicolor |
| macOS, or no PNG icon | Material icons: accent color for question / information, amber for warning, `RiceDangerBrush` for error |

Many Linux icon themes (Adwaita, Breeze, and Yaru's question icon) ship only SVG files; the Material icon is shown
then. `SystemDialogIcons.Get(icon, size, renderScaling)` returns the loaded image (or `null`) for your own views.

## Buttons

Standard sets – `DialogButtons`: `Ok`, `OkCancel`, `YesNo`, `YesNoCancel`, `RetryCancel`, `AbortRetryIgnore`,
`Close`. The first button is the default and accented. Esc clicks Cancel, else No, or the only button. The result
is a `DialogResult`. When the dialog is closed otherwise, the result is the cancel button's result, or
`DialogResult.None`. The captions come from `RiceStrings` (`Ok`, `Cancel`, `Yes`, `No`, `Retry`, `Abort`, `Ignore`,
`Close`).

Custom buttons – `DialogButton`:

| Property | Default | Description |
|----------|---------|-------------|
| `Text` | | Caption. |
| `Command`, `CommandParameter` | `null` | Executed on click before the dialog closes; `CanExecute` enables the button. |
| `Result` | `null` | Returned by `ShowAsync` when this button closes the dialog. |
| `IsDefault` / `IsCancel` | `false` | Clicked by Enter / Esc. |
| `IsAccent` | `false` | Accent colored. |
| `ClosesDialog` | `true` | `false` keeps the dialog open, e.g. when the command validates first. |

`DialogButton.For(DialogResult.Cancel)` creates a standard button.

```csharp
var result = await RiceDialog.ShowAsync(new DialogOptions
{
    Icon = DialogIcon.Warning,
    Message = "Save the changes of \"report.md\"?",
    Buttons =
    [
        new DialogButton("Save", "save") { IsAccent = true, IsDefault = true, Command = SaveCommand },
        new DialogButton("Don't save", "discard"),
        DialogButton.For(DialogResult.Cancel),
    ],
});
```

`DialogOptions` also has `Title` (not displayed, used by the task switcher and screen readers), `Width` (420) and
`MaxWidth`.

## Custom content: view and view model

```csharp
var viewModel = new RenameViewModel(fileName);
var result = await RiceDialog.ShowAsync<RenameView>(viewModel,
[
    new DialogButton("Rename") { IsAccent = true, IsDefault = true, Command = viewModel.RenameCommand, ClosesDialog = false },
    DialogButton.For(DialogResult.Cancel),
], DialogIcon.Question);
```

The view is created and gets the view model as its `DataContext`. With `DialogIcon.None` and no message, the dialog
shows only the view and the buttons. `DialogOptions.Content` takes a control too. It can also take a view model,
which is shown through `ContentTemplate` or the application's data templates / view locator.

A view model that implements `IDialogContent` can close the dialog itself, with any result:

```csharp
public sealed class RenameViewModel : IDialogContent
{
    public event EventHandler<object?>? CloseRequested;

    private void Rename() => CloseRequested?.Invoke(this, Name.Trim()); // ShowAsync returns the new name
}
```

## RiceWindow without title bar

The dialog uses the new `RiceWindow.ShowTitleBar` property. Set it to `False` on any `RiceWindow` to hide the whole
title bar.
