# KD.Avalonia.Rice

[![Build](https://github.com/k0zi/KD.Avalonia.Rice/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/k0zi/KD.Avalonia.Rice/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/KD.Avalonia.Rice.svg)](https://www.nuget.org/packages/KD.Avalonia.Rice)
[![NuGet downloads](https://img.shields.io/nuget/dt/KD.Avalonia.Rice.svg)](https://www.nuget.org/packages/KD.Avalonia.Rice)

<img src="https://raw.githubusercontent.com/k0zi/KD.Avalonia.Rice/main/src/KD.Avalonia.Rice/Assets/icon.png" alt="KD.Avalonia.Rice icon" width="96" />

Avalonia 12 library: frameless `RiceWindow` with a custom title bar and Linux distro inspired themes
(Ubuntu, Fedora, Manjaro, openSUSE, Nord, Solarized, Dracula, Gruvbox, Catppuccin) plus a Windows 11 theme – each with
light and dark variants.

Controls that follow the theme: side menu with sub items (`RiceSideMenu`), card (`RiceCard`), group box
(`RiceGroupBox`), chat (`RiceChat`), progress ring and bar (`RiceProgressRing`, `RiceProgressBar`), busy spinner (`RiceSpinner`), loading overlay (`RiceLoadingOverlay`) and a theme
selector (`ThemeSelector`, `ThemeDialog`).

## Documentation

Usage guides for every control are in the [docs](docs/README.md) folder.

## Usage

```csharp
AppBuilder.Configure<App>()
    .UsePlatformDetect()
    .UseRice(o =>
    {
        o.AppInfo = new RiceAppInfo { AppId = "my-app", Name = "My App", Author = "me",
                                      IconUri = "avares://MyApp/Assets/icon.png" };
        o.SupportedThemes = [BuiltInThemes.Ubuntu, BuiltInThemes.Nord, BuiltInThemes.Dracula];
        o.DefaultThemeId = "ubuntu";
    });
```

```xml
<!-- App.axaml: replaces FluentTheme -->
<Application.Styles>
  <rice:RiceStyles />
</Application.Styles>

<!-- MainWindow.axaml -->
<rice:RiceWindow ShowThemeSettingsButton="True" ShowAboutButton="True">
  ...
</rice:RiceWindow>

<!-- or embed the selector in your own settings view -->
<rice:ThemeSelector />
```

- **Light/Dark** follows the system on startup; the title bar button switches it for the session only (not saved).
- **Theme** (`ThemeManager.Instance.CurrentTheme`) is saved to `~/.config/<AppId>/rice.json` when the app supports more than one theme.
- Window corners are rounded (8 px) by default: set `RiceOptions.WindowCornerRadius` for every window
  (`new CornerRadius(0)` = square), or `CornerRadius` on a single `RiceWindow`. Corners are square when maximized
  and when the platform cannot draw transparent windows (e.g. X11 without a compositor).
- The theme settings button (`ShowThemeSettingsButton`) handles the theme only: it opens the built-in `ThemeDialog`.
  Your app's own settings belong elsewhere (e.g. a settings page); open the same dialog from there with
  `window.ShowThemeDialog()`.
- The about button opens a built-in dialog (name, author, version); handle `AboutRequested` to replace it.
- Brushes for your own controls: `RiceAccentBrush`, `RiceBackgroundBrush`, `RiceSurfaceBrush`, `RiceForegroundBrush`, `RiceForegroundMutedBrush`, `RiceBorderBrush`, … (use `DynamicResource`).
- Custom themes: `new RiceTheme("id", "Name", Light: new RicePalette(accent, bg, surface, fg), Dark: ...)`.
- Texts can be localized via `RiceStrings`.

### Side menu

```xml
<!-- xmlns:icons="clr-namespace:Material.Icons;assembly=Material.Icons" -->
<rice:RiceSideMenu DockPanel.Dock="Left" IsCollapsed="{Binding MenuCollapsed}">
  <rice:RiceSideMenu.Header>
    <TextBlock FontWeight="SemiBold" Text="My App" />
  </rice:RiceSideMenu.Header>
  <rice:RiceSideMenuItem Icon="{x:Static icons:MaterialIconKind.HomeOutline}" Header="Home" IsSelected="True" />
  <rice:RiceSideMenuItem Header="Photos">
    <rice:RiceSideMenuItem.Icon><Image Source="/Assets/photos.png" /></rice:RiceSideMenuItem.Icon>
  </rice:RiceSideMenuItem>
  <rice:RiceSideMenuItem Icon="{x:Static icons:MaterialIconKind.FolderOutline}" Header="Library" IsExpanded="True">
    <rice:RiceSideMenuItem Icon="{x:Static icons:MaterialIconKind.MusicNote}" Header="Music" />
    <rice:RiceSideMenuItem Icon="{x:Static icons:MaterialIconKind.Video}" Header="Videos" />
  </rice:RiceSideMenuItem>
  <rice:RiceSideMenu.Footer>
    <rice:RiceSideMenuItem Icon="{x:Static icons:MaterialIconKind.CogOutline}" Header="Settings"
                           Command="{Binding OpenSettingsCommand}" />
  </rice:RiceSideMenu.Footer>
</rice:RiceSideMenu>
```

- `RiceSideMenu` is a `TreeView`: use `SelectedItem` / `SelectionChanged` (`IsSelected` on an item), or `ItemsSource`
  with a `TreeDataTemplate` (sub items) or a `RiceSideMenuItem` style that binds `Icon`, `Header` and `ItemsSource`.
- Sub items: nest `RiceSideMenuItem`s to any depth. A parent shows a chevron and expands / collapses on click
  (`IsExpanded`, also ←/→ keys) instead of being selected; sub items are indented by `Indent` (default 16) per level.
  Selecting a sub item expands its parents; a closed parent shows the selection indicator while a sub item is selected.
- `Icon` takes an `IImage` (bitmap), a `MaterialIconKind`, a string (e.g. emoji) or any control.
- The hamburger button (`ShowCollapseButton`) toggles `IsCollapsed`: only the icons stay visible (width animates from
  `ExpandedWidth` to `CollapsedWidth`), the items keep working and show their text as tool tip. Sub items are hidden
  then; clicking a parent opens the menu with that parent expanded.
- `Header` and `Footer` are optional. Items in the footer are not selectable; they raise `Click` / run `Command`.
- `IsCollapsed` is inherited, so custom header/footer content can react to it as well.

### Card

```xml
<rice:RiceCard Header="Downloads" Subtitle="3 files" Icon="{x:Static icons:MaterialIconKind.DownloadOutline}">
  <rice:RiceCard.HeaderActions>
    <Button Content="⋮" />
  </rice:RiceCard.HeaderActions>
  <TextBlock TextWrapping="Wrap" Text="The ISO image is ready." />
  <rice:RiceCard.Footer>
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Cancel" />
      <Button Classes="accent" Content="Save" />
    </StackPanel>
  </rice:RiceCard.Footer>
</rice:RiceCard>
```

- Every part is optional: the header (`Header` as title, `Subtitle`, `Icon`, `HeaderActions` on the right) shows when
  any of them is set; the `Footer` bar (right aligned, separated by a line) when it is set.
- `Elevation` 0–3 (default 1) sets the shadow depth, 0 is flat. The shadows are the `RiceCardShadow1..3` resources
  (stronger in dark variants). Leave some space around the card (margin/spacing), a clipping parent cuts the shadow.
- `Background` (default `RiceSurfaceBrush`), `BorderBrush`, `CornerRadius` (default 12), `Padding` (default 16).

### Group box

```xml
<rice:RiceGroupBox Header="Network" Icon="{x:Static icons:MaterialIconKind.Tune}">
  <StackPanel Spacing="8">
    <CheckBox Content="Use DHCP" />
    <TextBox Text="192.168.1.10" />
  </StackPanel>
</rice:RiceGroupBox>
```

- Like the WinForms GroupBox: the title (`Header` + optional `Icon`) sits on the top line and cuts it.
  The line is drawn with a gap, so it works on any background (no need to paint the title background).
- `Icon` takes the same kinds as the side menu (`IImage`, `MaterialIconKind`, string, control). Without `Header`
  and `Icon` the frame is closed.
- `BorderBrush` (default `RiceBorderBrush`), `BorderThickness`, `CornerRadius` (default 8), `Background`, `Padding`.

### Chat

```xml
<rice:RiceChat ItemsSource="{Binding Messages}" ChatStyle="Bubbles" MessageSent="OnMessageSent" />
```

```csharp
public ObservableCollection<RiceChatMessage> Messages { get; } = [];

private void OnMessageSent(object? sender, RiceChatMessageSentEventArgs e) =>
    Messages.Add(new RiceChatMessage { Author = "me", Text = e.Text, IsOwn = true });
```

- Messages on top, input box at the bottom: Enter sends, Shift+Enter starts a new line, empty text is not sent.
- `ChatStyle="Bubbles"` (default): modern bubbles, own messages (`IsOwn`) on the right in accent color.
  `ChatStyle="Classic"`: IRC like log, `[12:34] <author> text` in a monospace font.
- The control does not add the sent message: handle `MessageSent` or bind `SendCommand` (gets the text as parameter)
  and add it to your collection. `Text` holds the draft (two-way), `Send()` sends it from code.
- `TimestampFormat` (default `HH:mm`, local time), `PlaceholderText`, `ShowInput="False"` for a read-only log,
  `AutoScroll` (follows new messages while scrolled to the bottom).
- Brushes: `RiceChatBubbleBrush`, `RiceChatOwnBubbleBrush`; texts: `RiceStrings.ChatPlaceholder`, `RiceStrings.Send`.

### Progress ring, progress bar, spinner and loading overlay

```xml
<rice:RiceProgressRing Value="{Binding Percent}" Width="64" Height="64" Thickness="6" ShowValue="True" />
<rice:RiceProgressRing IsIndeterminate="True" />
<rice:RiceProgressBar Value="{Binding Percent}" ShowValue="True" />
<rice:RiceProgressBar IsIndeterminate="True" />

<Button Classes="accent">
  <StackPanel Orientation="Horizontal" Spacing="8">
    <rice:RiceSpinner IsActive="{Binding IsSaving}" />
    <TextBlock Text="Save" />
  </StackPanel>
</Button>

<rice:RiceLoadingOverlay IsActive="{Binding IsLoading}" Message="Loading profile…">
  <local:ProfileForm />
</rice:RiceLoadingOverlay>
```

- Same API for both: determinate fills by `Value` between `Minimum` and `Maximum` (0–100), `ShowValue` shows the
  percentage (`ValueStringFormat`); indeterminate moves (ring: spins, bar: slides) until `IsIndeterminate` is
  switched off.
- `Thickness` (ring: stroke width, bar: height), `Foreground` (accent), `Background` (track).
  See the [ring](docs/rice-progress-ring.md) and [bar](docs/rice-progress-bar.md) docs.
- `RiceSpinner`: 16 px spinning arc in the current text color (white in an accent button), `IsActive="False"` hides it.
  See the [spinner docs](docs/rice-spinner.md).
- `RiceLoadingOverlay`: while `IsActive`, covers its content with a translucent layer, a ring (indeterminate or
  `Value`) and a `Message`, and blocks pointer and keyboard input to it. See the [overlay docs](docs/rice-loading-overlay.md).

## Demo

```sh
dotnet run --project samples/KD.Avalonia.Rice.Demo
```

## Versioning & NuGet package

Every build of `src/KD.Avalonia.Rice` produces `artifacts/packages/KD.Avalonia.Rice.<version>.nupkg` (+ `.snupkg`).
The version comes from [Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning):
the version is `<yy>.<month>.<git height>` (e.g. `26.10.5`): `version.json` holds year.month, the patch is the number
of commits since it was set. The build warns when `version.json` is not the current month – bump it by editing the file
(not with `nbgv set-version`, which drops the other settings).
Builds on `main`/`master` or `v*` tags get a clean version; other branches get a `-g<commit>` suffix.

```sh
dotnet pack -c Release src/KD.Avalonia.Rice   # release package
```

## Icon

The package icon (`src/KD.Avalonia.Rice/Assets/icon.png`) is based on the
[Linux icon](https://www.iconarchive.com/show/material-icons-by-pictogrammers/linux-icon.html)
from the Material Icons set by Pictogrammers, licensed under the
[Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0).
