# RiceSkeleton

Placeholder in the shape of content that is still loading – a block, a circle or lines of text – with a light band
sweeping over it. Compose several into the outline of a card or a list row and swap them for the real content once it is
there; the layout stays steady instead of jumping when the data arrives. For a whole view that is busy, the
[loading overlay](rice-loading-overlay.md) fits better.

```xml
<!-- Outline of a post: avatar, two header lines, a paragraph. -->
<rice:RiceCard IsVisible="{Binding IsLoading}">
  <Grid ColumnDefinitions="Auto,*" RowDefinitions="Auto,Auto">
    <rice:RiceSkeleton Shape="Circle" Width="48" Height="48" Margin="0,0,12,0" />
    <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="8">
      <rice:RiceSkeleton LineHeight="14" Width="160" HorizontalAlignment="Left" />
      <rice:RiceSkeleton LineHeight="10" Width="100" HorizontalAlignment="Left" />
    </StackPanel>
    <rice:RiceSkeleton Grid.Row="1" Grid.ColumnSpan="2" Margin="0,16,0,0" Shape="Text" Lines="3" />
  </Grid>
</rice:RiceCard>
```

## Shapes

| `Shape` | Draws | Size without `Width` / `Height` |
|---------|-------|---------------------------------|
| `Rectangle` (default) | A rounded block filling the control | Stretches; `LineHeight` high |
| `Circle` | A circle as large as the smaller side, centered | 40 × 40 |
| `Text` | `Lines` lines of `LineHeight`, `LineSpacing` apart; the last one is 60 % wide | Stretches; as high as the lines |

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Shape` | `Rectangle` | See above. |
| `Lines` | `3` | Number of text lines (at least 1). |
| `LineHeight` | `12` | Height of a text line, and of a rectangle without `Height`. |
| `LineSpacing` | `8` | Gap between text lines. |
| `CornerRadius` | `4` | Corners of the rectangle and the lines (the top left value is used). |
| `IsAnimated` | `true` | Sweeps the band; `false` draws a still placeholder. |
| `Fill` | `RiceSkeletonBrush` | Placeholder color. |
| `Highlight` | `RiceSkeletonHighlightBrush` | Color of the band (a solid color brush). |

## Styling

The sweep is an animation of `ShimmerPosition` (0 → 1) in the control theme while the `:animated` pseudo class is set;
override the theme to change its speed. The brushes are theme resources, faint in both variants.
