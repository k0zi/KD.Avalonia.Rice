# RiceRibbon

Office like ribbon: tabs of named command groups with large and small buttons. When the ribbon is too narrow, the
groups collapse into a single button from the right, and that button opens the group in a flyout. The ribbon can be
minimized to its tabs; then a click on a tab opens its groups over the content below. Alt shows key tips, so every
command can be run from the keyboard.

```xml
<rice:RiceRibbon DockPanel.Dock="Top" ApplicationButtonContent="File">
  <rice:RiceRibbon.ApplicationMenu>
    <MenuFlyout Placement="BottomEdgeAlignedLeft">
      <MenuItem Header="Open…" Command="{Binding OpenCommand}" />
      <MenuItem Header="Save" Command="{Binding SaveCommand}" />
    </MenuFlyout>
  </rice:RiceRibbon.ApplicationMenu>

  <rice:RiceRibbonTab Header="Home">
    <rice:RiceRibbonGroup Header="Clipboard" Icon="{x:Static icons:MaterialIconKind.ClipboardOutline}"
                          ShowLauncher="True" LauncherCommand="{Binding ClipboardOptionsCommand}">
      <rice:RiceRibbonButton Icon="{x:Static icons:MaterialIconKind.ContentPaste}" Content="Paste"
                             Command="{Binding PasteCommand}" />
      <rice:RiceRibbonButton Size="Medium" Icon="{x:Static icons:MaterialIconKind.ContentCut}" Content="Cut" />
      <rice:RiceRibbonButton Size="Medium" Icon="{x:Static icons:MaterialIconKind.ContentCopy}" Content="Copy" />
    </rice:RiceRibbonGroup>

    <rice:RiceRibbonGroup Header="Font" Icon="{x:Static icons:MaterialIconKind.FormatFont}">
      <ComboBox Width="150" MinHeight="26" ItemsSource="{Binding Fonts}" SelectedItem="{Binding Font}" />
      <StackPanel Orientation="Horizontal">
        <rice:RiceRibbonToggleButton Size="Small" Icon="{x:Static icons:MaterialIconKind.FormatBold}"
                                     IsChecked="{Binding IsBold}" ToolTip.Tip="Bold" />
        <rice:RiceRibbonToggleButton Size="Small" Icon="{x:Static icons:MaterialIconKind.FormatItalic}"
                                     IsChecked="{Binding IsItalic}" ToolTip.Tip="Italic" />
      </StackPanel>
    </rice:RiceRibbonGroup>
  </rice:RiceRibbonTab>

  <rice:RiceRibbonTab Header="Insert">
    …
  </rice:RiceRibbonTab>
</rice:RiceRibbon>
```

It is a `TabControl` underneath: `SelectedIndex`, `SelectedItem` and `SelectionChanged` work as usual. The tabs are
declared in XAML (`RiceRibbonTab`), each with its groups as content.

## Groups and item sizes

A `RiceRibbonGroup` lays out its items from the left, with its `Header` below them and a line on its right. Every item
has a size, the attached `RiceRibbon.Size` (`RiceRibbonButton.Size` on the buttons):

| Size | Layout |
|------|--------|
| `Large` | A column of the full height: big (32 px) icon above a label of up to two lines. Default of the ribbon buttons and `RiceRibbonSeparator`. |
| `Medium` | One of three rows: small icon and label. Default of any other control (combo box, text box, check box, a `StackPanel` of small buttons, …). |
| `Small` | One of three rows: only the small icon – add a `ToolTip.Tip`. |

Medium and Small items fill columns of up to three rows, as long as their heights fit (Fluent's 32 px check boxes fit
two per column). Put several Small buttons in a horizontal `StackPanel` to get a row of them, like Bold / Italic /
Underline. `RiceRibbonSeparator` draws a vertical line between items.

| Group property | Default | Description |
|----------------|---------|-------------|
| `Header` | | Name under the items, and the label of the collapsed button. |
| `Icon` | | Icon of the collapsed button (`IImage`, `MaterialIconKind`, string or control). |
| `ShowLauncher` | `false` | Small arrow button next to the header ("more options", e.g. a dialog). |
| `LauncherCommand` / `LauncherClick` | | Run by the launcher button. |
| `LauncherKeyTip` | | Key tip of the launcher button; chosen from the header when not set. |
| `CanCollapse` | `true` | False keeps the group expanded even when the ribbon is too narrow. |
| `IsCollapsed` | | Read only; true while the group is shown as a button (pseudo-class `:collapsed`). |

## Collapsing

When the groups of the selected tab do not fit, they collapse from the right, one after the other, until the rest fits
(groups with `CanCollapse="False"` are skipped). A collapsed group is a large button with the group's icon and header;
it opens the items of the group in a flyout. A command run from the flyout closes it, like a menu (buttons that open a
flyout of their own keep it open). When the ribbon gets wider again, the groups expand.

## Buttons

`RiceRibbonButton` is a `Button`: `Command`, `CommandParameter`, `Click` and `IsEnabled` work as usual. Its content is
the label, `Icon` the icon (`IImage`, `MaterialIconKind`, string such as an emoji, or any control). With a `Flyout`
(e.g. a `MenuFlyout`) it shows a drop down arrow and opens the flyout:

```xml
<rice:RiceRibbonButton Icon="{x:Static icons:MaterialIconKind.ContentPaste}" Content="Paste">
  <rice:RiceRibbonButton.Flyout>
    <MenuFlyout>
      <MenuItem Header="Paste as plain text" />
      <MenuItem Header="Paste special…" />
    </MenuFlyout>
  </rice:RiceRibbonButton.Flyout>
</rice:RiceRibbonButton>
```

`RiceRibbonToggleButton` stays pressed while `IsChecked` (two way bindable). Toggle buttons with the same `GroupName`
in the same panel work as radio buttons, e.g. text alignment:

```xml
<rice:RiceRibbonToggleButton Size="Small" GroupName="align" IsChecked="True" Icon="{x:Static icons:MaterialIconKind.FormatAlignLeft}" />
<rice:RiceRibbonToggleButton Size="Small" GroupName="align" Icon="{x:Static icons:MaterialIconKind.FormatAlignCenter}" />
<rice:RiceRibbonToggleButton Size="Small" GroupName="align" Icon="{x:Static icons:MaterialIconKind.FormatAlignRight}" />
```

## Minimizing

`IsMinimized` (two way bindable) hides the groups, only the tabs show. A double click on a tab, the chevron at the right
end of the tab strip and Ctrl+F1 (anywhere in the window) toggle it. While minimized, a click on a tab opens its groups
in a popup over the content below; a command, a click outside, Escape or another click on the tab closes it.

## Key tips

Like in Office, Alt (the left one; released without another key) or F10 shows a letter badge on the application button
and on every tab. Typing a tab's letter selects it and shows the badges of its commands; typing a command's letter runs
it, and the badges go away:

| Item | Typing its key tip |
|------|--------------------|
| Tab | Selects it (in a minimized ribbon it opens the popup) and shows the key tips of its commands. |
| Button, toggle button | Clicks it (runs `Command`, raises `Click`, toggles, opens its `Flyout`). |
| Collapsed group | Opens its flyout and shows the key tips of its items. |
| Check box | Toggles it. |
| Combo box | Focuses and opens it. |
| Anything else that takes the focus (e.g. a text box) | Focuses it. |

Key tips of more letters (e.g. "FN") work by typing them in turn; after the first letter only the matching badges stay.
Escape goes back a level (from the commands to the tabs, from a collapsed group to the tab), Alt or F10 again, a click,
or a key that is no letter (arrows, Tab, …) closes them. The letters are not typed into the focused text box.

Set a key tip with the attached `RiceRibbon.KeyTip` on a tab, a command or a group (its collapsed button), the
`ApplicationButtonKeyTip` of the ribbon and the `LauncherKeyTip` of a group. Items without one get a letter of their
label (the content, the header, or a string `ToolTip.Tip`), word initials first, then digits ("1"…"9", "Y1", …), so that
no key tip is the start of another one.

```xml
<rice:RiceRibbonTab Header="Insert" rice:RiceRibbon.KeyTip="N">
  <rice:RiceRibbonGroup Header="Font" rice:RiceRibbon.KeyTip="ZF" ShowLauncher="True" LauncherKeyTip="FN">
    <ComboBox rice:RiceRibbon.KeyTip="FF" … />
    <rice:RiceRibbonButton Content="Paste" rice:RiceRibbon.KeyTip="V" />
```

`ShowKeyTips()` / `HideKeyTips()` and `AreKeyTipsShown` do the same from code; `IsKeyTipsEnabled="False"` turns them off.
The badges are `RiceKeyTip` controls in the adorner layer, styled with its control theme.

## Ribbon properties

| Property | Default | Description |
|----------|---------|-------------|
| `IsMinimized` | `false` | Only the tabs show (pseudo-class `:minimized`). |
| `ShowMinimizeButton` | `true` | The chevron at the right end of the tab strip. |
| `ApplicationButtonContent` | | Content of the accent button before the tabs (e.g. "File"); hidden while null. |
| `ApplicationMenu` | | Flyout of the application button, e.g. a `MenuFlyout`. |
| `ApplicationButtonCommand` | | Command of the application button (e.g. to show a backstage page). |
| `TabStripFooter` | | Content at the right end of the tab strip, e.g. a search box. |
| `IsKeyTipsEnabled` | `true` | Alt / F10 shows the key tips. |
| `ApplicationButtonKeyTip` | | Key tip of the application button; chosen from its content when not set. |
| `Background` | `RiceSurfaceBrush` | Behind the groups; the tabs sit on the window background. |

## Contextual tabs

A tab with `IsContextual="True"` has an accent colored header, for tools of the current selection (e.g. "Table design"
while a table is selected). Show and hide it with `IsVisible`:

```xml
<rice:RiceRibbonTab Header="Table design" IsContextual="True" IsVisible="{Binding IsTableSelected}">
  …
</rice:RiceRibbonTab>
```

## Localization

The tooltips come from `RiceStrings`: `RibbonCollapse`, `RibbonExpand` (the chevron) and `RibbonGroupLauncher`.
