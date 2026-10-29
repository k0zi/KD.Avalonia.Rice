# Rice Theme Editor

Development tool for creating `RiceTheme`s. It is not packed and not shipped with KD.Avalonia.Rice.

```sh
dotnet run --project tools/KD.Avalonia.Rice.ThemeEditor
```

- **Start from a theme:** pick a built-in or a saved theme on the left; its colors are loaded into the editor.
  A built-in theme gets the name "My &lt;name&gt;", so saving does not clash with it.
- **Edit the colors:** the four core colors (accent, background, surface, foreground) are required for both the light
  and the dark variant. The other colors are derived from them, as in `RicePalette`; check one to set it yourself.
  The whole editor window uses the theme being edited, the Light / Dark tab switches the variant.
- **Save:** writes `<id>.json` (the id is made from the name, e.g. "My Theme" → `my-theme`) to
  `<ApplicationData>/kd-rice-theme-editor/themes` (`~/.config/kd-rice-theme-editor/themes` on Linux). Saving under
  the name of an existing saved theme replaces it. Only the core colors and the overridden ones are stored:

  ```json
  {
    "id": "my-theme",
    "displayName": "My Theme",
    "light": { "accent": "#E95420", "background": "#FAFAFA", "surface": "#FFFFFF", "foreground": "#3D3D3D", "danger": "#C7162B" },
    "dark": { "accent": "#E95420", "background": "#2C2C2C", "surface": "#3D3D3D", "foreground": "#F7F7F7" }
  }
  ```

- **Copy as C#:** copies the theme as a property in the style of `BuiltInThemes`, ready to paste into an app and add
  to `RiceOptions.SupportedThemes`:

  ```csharp
  public static RiceTheme MyTheme { get; } = new("my-theme", "My Theme",
      Light: new(Color.Parse("#E95420"), Color.Parse("#FAFAFA"), Color.Parse("#FFFFFF"), Color.Parse("#3D3D3D"))
      {
          Danger = Color.Parse("#C7162B"),
      },
      Dark: new(Color.Parse("#E95420"), Color.Parse("#2C2C2C"), Color.Parse("#3D3D3D"), Color.Parse("#F7F7F7")));
  ```
