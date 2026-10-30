# RiceEmptyState

Placeholder for a view without data – "No projects yet", "No search results", "Inbox zero": a large muted icon, a title,
a description and, as content, the buttons that get the user going. Centered in the space it gets.

<picture>
  <source media="(prefers-color-scheme: dark)" srcset="images/feedback-dark.png" />
  <img src="images/feedback.png" alt="RiceEmptyState" width="800" />
</picture>

```xml
<rice:RiceEmptyState Icon="{x:Static icons:MaterialIconKind.FolderOpenOutline}"
                     Title="No projects yet"
                     Description="Create a project or open an existing folder to get started.">
  <StackPanel Orientation="Horizontal" Spacing="8">
    <Button Classes="accent" Content="New project" Command="{Binding NewProjectCommand}" />
    <Button Content="Open folder…" Command="{Binding OpenFolderCommand}" />
  </StackPanel>
</rice:RiceEmptyState>
```

Swap it with the list when there is data, e.g. `IsVisible="{Binding !Items.Count}"` on the empty state and
`IsVisible="{Binding Items.Count}"` on the list.

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Icon` | `null` | `IImage`, `MaterialIconKind`, string (e.g. emoji) or any control; shown muted. |
| `IconSize` | `64` | Width and height of the icon. |
| `Title` | `null` | 18 px semi-bold line. |
| `Description` | `null` | Muted, wrapped text below the title. |
| `Content` | `null` | Below the text, usually buttons. |
| `Padding` | `24` | Space around the whole block. |
| `HorizontalContentAlignment`, `VerticalContentAlignment` | `Center` | Where the block sits; `Top` for long views. |

The text block is at most 420 px wide, so the description wraps into a readable column.
