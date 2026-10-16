# RiceCard

Raised surface for related content: an optional header (title, subtitle, icon, actions on the right), the content and an
optional footer bar, with a shadow whose depth is set by `Elevation`. Every part is optional.

```xml
<rice:RiceCard Header="Downloads" Subtitle="3 files, 1.2 GB" Icon="{x:Static icons:MaterialIconKind.DownloadOutline}">
  <rice:RiceCard.HeaderActions>
    <Button Content="⋮" />
  </rice:RiceCard.HeaderActions>

  <TextBlock TextWrapping="Wrap" Text="The ISO image is ready. Verify the checksum before installing." />

  <rice:RiceCard.Footer>
    <StackPanel Orientation="Horizontal" Spacing="8">
      <Button Content="Cancel" />
      <Button Classes="accent" Content="Save" />
    </StackPanel>
  </rice:RiceCard.Footer>
</rice:RiceCard>
```

Content only (a plain raised panel):

```xml
<rice:RiceCard Elevation="2">
  <TextBlock Text="Only content, no header or footer." />
</rice:RiceCard>
```

## Layout

- **Header** – shown when any of `Header`, `Subtitle`, `Icon`, `HeaderActions` is set. `Header` is the title (16 px,
  semi-bold), `Subtitle` the muted line below it, `Icon` (24 × 24 px) sits on the left, `HeaderActions` on the right.
- **Content** – `Content`, surrounded by `Padding`.
- **Footer** – shown when `Footer` is set: a bar below the content, separated by a line, right aligned by default.

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Header`, `HeaderTemplate` | `null` | Title. |
| `Subtitle` | `null` | Secondary text under the title. |
| `Icon` | `null` | `IImage`, `MaterialIconKind`, string (e.g. emoji) or any control; Material icons use the accent color. |
| `HeaderActions` | `null` | Content on the right of the header, e.g. a menu button. |
| `Footer`, `FooterTemplate` | `null` | Bar below the content, e.g. buttons. |
| `Elevation` | `1` | Shadow depth, `0` (flat) to `3`; other values are clamped. |
| `Background` | `RiceSurfaceBrush` | Card color. |
| `BorderBrush`, `BorderThickness` | `RiceBorderBrush`, `1` | Outline. |
| `CornerRadius` | `12` | |
| `Padding` | `16` | Space around the content. |

## Tips

- The shadow is drawn outside the card: leave some space around it (`Margin`, panel `Spacing`). A parent with
  `ClipToBounds="True"` (e.g. a `ScrollViewer` without padding) cuts it.
- Cards in a grid: `WrapPanel` or `UniformGrid` with a margin on each card.
- Data templates work as usual: `<rice:RiceCard Header="{Binding Title}" Content="{Binding}" ContentTemplate="..." />`.

## Styling

Pseudo classes: `:has-header`, `:has-footer`, `:elevation0` … `:elevation3`.
Shadows are the `RiceCardShadow1` … `RiceCardShadow3` resources (`BoxShadows`, stronger in dark variants); override
them in your resources to change every card.
