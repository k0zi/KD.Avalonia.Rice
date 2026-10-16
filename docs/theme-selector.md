# ThemeSelector and ThemeDialog

## ThemeSelector

Lists the themes the app supports (`RiceOptions.SupportedThemes`) with light and dark color previews. Selecting one
switches the theme immediately and saves it (to `<ApplicationData>/<AppId>/rice.json`, i.e. `~/.config/<AppId>/rice.json` on Linux). It follows theme changes made
elsewhere, so several selectors can be open at once.

Embed it in your own settings page:

```xml
<StackPanel Spacing="12">
  <TextBlock FontWeight="SemiBold" Text="Appearance" />
  <rice:ThemeSelector />
</StackPanel>
```

It has no properties of its own; the list comes from `ThemeManager.Instance`. With a single supported theme there is
nothing to choose, so show the selector only when `ThemeManager.Instance.HasThemeChoice` is `true`.

## ThemeDialog

A modal `RiceWindow` containing a `ThemeSelector` and a close button. The title bar's gear button
(`ShowThemeSettingsButton="True"` on the `RiceWindow`) opens it; from code:

```csharp
await mainWindow.ShowThemeDialog();
// or
await new ThemeDialog().ShowDialog(owner);
```

The gear button is meant for the theme only – the app's own settings belong to your own settings view, which can open
the same dialog with `ShowThemeDialog()`.

## Without the UI

```csharp
var themes = ThemeManager.Instance;
themes.SetTheme("nord");                  // or themes.CurrentTheme = BuiltInThemes.Nord;
themes.ToggleVariant();                   // light <-> dark, for this session
themes.PropertyChanged += (_, e) => { };  // CurrentTheme / IsDark changes
```

Texts: `RiceStrings.Theme` (dialog title), `RiceStrings.Close`.
