# RiceCommandPalette

Search box over the commands of the application, like the command palette of code editors. Typing filters and ranks the
commands (fuzzy: "ld" finds "Light / Dark", the matched letters are highlighted), the arrow keys move the selection,
Enter runs the selected command, Escape closes. Recently used commands come first.

## In a RiceWindow

A `RiceWindow` opens the palette with **Ctrl+Shift+P**. It lists the window's `Commands` and – with
`IncludeNavigationCommands` (default) – every item of its `RiceSideMenu` under "Go to", so the pages are reachable from
the keyboard without any extra code.

```csharp
Commands.Add(new RiceCommandItem("Toggle light / dark", ThemeManager.Instance.ToggleVariant)
{
    Icon = MaterialIconKind.LightbulbOutline,
    Category = "View",
    Keywords = "dark light mode",
    Gesture = new KeyGesture(Key.T, KeyModifiers.Control | KeyModifiers.Shift),
});
Commands.Add(new RiceCommandItem { Title = "Save", Command = viewModel.SaveCommand, Gesture = KeyGesture.Parse("Ctrl+S") });
```

`RiceCommandItem` is a plain class (not a control), so set it up in code – XAML bindings cannot target it.

The `Gesture` of a command is shown in the palette **and** runs the command in the whole window, also while the palette
is closed. Open the palette from code with `window.ShowCommandPalette()` or `ShowCommandPalette("theme")` (pre-filled).
Change the shortcut with `CommandPaletteGesture` (`null` turns it off). Override `GetPaletteCommands()` in a subclass to
build the list differently (e.g. context dependent commands).

## Over any window

```csharp
RiceCommandPalette.Show(commands, owner: this, query: null);
RiceCommandPalette.Hide(this);
bool open = RiceCommandPalette.IsShown(this);
```

The palette is shown in the window's overlay layer, centered near the top, on a dimmed backdrop; a click on the backdrop
closes it, and the focus goes back where it was. Each window keeps its palette, so the recent commands are remembered
while the application runs.

## Embedded

The control can also be placed in a layout, e.g. as a quick picker. Embedded, choosing an entry raises `CommandInvoked`
and runs it, but the palette stays where it is.

```xml
<rice:RiceCommandPalette Commands="{Binding Distros}" Width="NaN" Height="300"
                         Placeholder="Pick a distribution…" CommandInvoked="OnPicked" />
```

## RiceCommandItem

| Property | Description |
|----------|-------------|
| `Title` | Main text; the search matches it first and highlights the matched letters. |
| `Description` | Muted second line. |
| `Category` | Muted text on the right ("View", "File", "Go to"). |
| `Icon` | `IImage`, `MaterialIconKind`, string (e.g. emoji) or any control. |
| `Gesture` | `KeyGesture` shown as the shortcut (and run by a `RiceWindow`). |
| `Keywords` | Extra search terms that are not shown, e.g. synonyms. |
| `Command`, `CommandParameter` | Run when chosen. While `Command.CanExecute` is false the entry is dimmed and cannot run. |
| `Execute` | `Action` run when chosen (before `Command`); the `(title, execute)` constructor sets it. |

Category, keywords and description are searched too, but count less than the title. Every word of the query must match
somewhere ("theme set" finds "Theme settings").

## RiceCommandPalette

| Member | Default | Description |
|--------|---------|-------------|
| `Commands` | `null` | The entries. |
| `Query` | `""` | Text of the search box (two-way bindable). |
| `Placeholder` | `RiceStrings.CommandPalettePlaceholder` | Shown in the empty search box. |
| `MaxResults` | `50` | Number of entries listed at most. |
| `Matches` | | The ranked results (`RiceCommandMatch`: `Command`, `Score`, `Highlights`, `IsEnabled`). |
| `SelectedMatch` | | The highlighted result. |
| `RecentCommands` | | The commands run from this palette, most recent first. |
| `Invoke(match)` | | Runs a result (default: the selected one). |
| `FocusSearchBox()` | | Focuses the search box and selects its text. |
| `CommandInvoked` event | | A command was chosen, before it runs. |
| `CloseRequested` event | | A command was chosen or Escape was pressed. |

PageUp / PageDown move by a page; Up on the first entry wraps to the last one.

## Styling

Brushes: `RiceBackdropBrush` (dimmed window), `RicePopupShadow`, `RiceHoverBrush`, `RiceSelectedRowBrush`. The palette is
600 px wide and at most 460 px high by default (`Width`, `MaxHeight`). Texts: `RiceStrings.CommandPalettePlaceholder`,
`CommandPaletteNoResults`, `CommandPaletteNavigation`.
