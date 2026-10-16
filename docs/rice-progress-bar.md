# RiceProgressBar

Horizontal progress bar with rounded (pill shaped) ends and two modes:

- **Determinate** – the bar fills from the left by `Value` (0–100 by default), optionally with the percentage on the
  right. Value changes animate smoothly, resizing redraws at once.
- **Indeterminate** – a segment keeps sliding through (its head runs ahead, then the tail catches up) until
  `IsIndeterminate` is switched off.

It shares its API with [`RiceProgressRing`](rice-progress-ring.md) (both derive from `RiceProgressBase`, a
`RangeBase`), so the two can be swapped or bound to the same values.

```xml
<!-- 0–100 % with the value on the right -->
<rice:RiceProgressBar Value="{Binding DownloadPercent}" ShowValue="True" />

<!-- moves while busy -->
<rice:RiceProgressBar IsIndeterminate="True" IsVisible="{Binding IsBusy}" />
```

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Value` | `0` | Current progress, clamped between `Minimum` and `Maximum`. |
| `Minimum`, `Maximum` | `0`, `100` | Range of `Value`. |
| `IsIndeterminate` | `false` | Sliding segment; `Value` is ignored meanwhile. |
| `Thickness` | `4` | Height of the bar (px). The control is as high as the bar or the text, whichever is higher. |
| `ShowValue` | `false` | Shows `ValueText` right of the bar (determinate mode only, min. 36 px wide so the bar does not jump). |
| `ValueStringFormat` | `{0:0}%` | Format of the text, the argument is `Percentage`. |
| `Percentage` | – | Read-only, 0–100 (0 when the range is empty). |
| `Fraction` | – | Read-only, the filled part, 0–1. |
| `ValueText` | – | Read-only, the formatted text. |
| `Foreground` | `RiceAccentBrush` | Color of the filled part / moving segment. |
| `Background` | `RiceBorderBrush` | Color of the track; `Transparent` hides it. |
| `MinWidth` | `80` | The bar stretches horizontally by default; set `Width` for a fixed size. |
| `FontSize` | `12` | Size of the value text. |

## Examples

Thin line under a header, e.g. while a page loads:

```xml
<DockPanel>
  <rice:RiceProgressBar DockPanel.Dock="Top" IsIndeterminate="True" Thickness="2" Background="Transparent"
                        IsVisible="{Binding IsLoading}" />
  <ContentControl Content="{Binding Page}" />
</DockPanel>
```

File copy with a custom range and text:

```xml
<rice:RiceProgressBar Maximum="{Binding TotalFiles}" Value="{Binding CopiedFiles}"
                      Thickness="8" ShowValue="True" ValueStringFormat="{}{0:0} %" />
```

Unknown length first, then real progress:

```csharp
Bar.IsIndeterminate = true;
await ConnectAsync();
Bar.IsIndeterminate = false;
await UploadAsync(new Progress<double>(p => Bar.Value = p * 100));
```

Ring and bar showing the same work:

```xml
<rice:RiceProgressRing Name="Ring" Value="{Binding Percent}" IsIndeterminate="{Binding IsPreparing}" />
<rice:RiceProgressBar Value="{Binding #Ring.Value}" IsIndeterminate="{Binding #Ring.IsIndeterminate}" />
```

## Styling

Pseudo classes: `:determinate`, `:indeterminate`, `:showvalue`.
Template parts: `PART_Track` and `PART_Indicator` (`RiceProgressSegment`), `PART_Text` (`TextBlock`).
The sliding is a `Style.Animations` animation of `PART_Indicator`'s `Start` / `End` under `:indeterminate`
(1.6 s per pass); override it in a style to change the speed or the motion.

`RiceProgressSegment` is a public primitive that draws a pill shaped segment between `Start` and `End` (fractions of
its width, the part outside 0–1 is cut) with `Fill` – useful for custom bars, e.g. a buffered / played media bar.
