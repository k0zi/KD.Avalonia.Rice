# RiceProgressRing

Circular progress indicator with two modes:

- **Determinate** – the ring fills from the top, clockwise, by `Value` (0–100 by default), optionally with the
  percentage in the center. Value changes animate smoothly.
- **Indeterminate** – an arc keeps spinning (growing and shrinking) until `IsIndeterminate` is switched off; use it
  for work of unknown length.

`RiceProgressRing` is a `RangeBase` like `ProgressBar`, so `Minimum`, `Maximum` and `Value` work the same way.
It shares its API with the horizontal [`RiceProgressBar`](rice-progress-bar.md) (both derive from `RiceProgressBase`).
For a small inline busy indicator (e.g. in a button) use [`RiceSpinner`](rice-spinner.md).

```xml
<!-- 0–100 % with the value in the center -->
<rice:RiceProgressRing Value="{Binding DownloadPercent}" Width="64" Height="64" Thickness="6" ShowValue="True" />

<!-- spins while busy -->
<rice:RiceProgressRing IsIndeterminate="True" IsVisible="{Binding IsBusy}" />
```

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Value` | `0` | Current progress, clamped between `Minimum` and `Maximum`. |
| `Minimum`, `Maximum` | `0`, `100` | Range of `Value`, e.g. `Maximum="{Binding TotalBytes}"`. |
| `IsIndeterminate` | `false` | Spinning arc; `Value` is ignored meanwhile. |
| `Thickness` | `4` | Stroke width of the ring (px). |
| `ShowValue` | `false` | Shows `ValueText` in the center (determinate mode only). |
| `ValueStringFormat` | `{0:0}%` | Format of the center text, the argument is `Percentage`, e.g. `{0:0.0} %`. |
| `Percentage` | – | Read-only, 0–100, computed from `Value`, `Minimum`, `Maximum` (0 when the range is empty). |
| `ValueText` | – | Read-only, the formatted center text. |
| `Foreground` | `RiceAccentBrush` | Color of the arc. |
| `Background` | `RiceBorderBrush` | Color of the track (the full circle behind the arc); `Transparent` hides it. |
| `Width`, `Height` | `40` | The ring fits the smaller side. |
| `FontSize` | `11` | Size of the center text; increase it for bigger rings. |

## Examples

Sizes – a small ring for inline use, a big one for a page:

```xml
<rice:RiceProgressRing IsIndeterminate="True" Width="16" Height="16" Thickness="2" />
<rice:RiceProgressRing Value="42" Width="96" Height="96" Thickness="10" ShowValue="True" FontSize="18" />
```

Unknown length first, then real progress:

```csharp
Ring.IsIndeterminate = true;           // preparing…
await PrepareAsync();
Ring.IsIndeterminate = false;
await DownloadAsync(new Progress<double>(p => Ring.Value = p * 100));
```

Custom range and text:

```xml
<rice:RiceProgressRing Minimum="0" Maximum="{Binding TotalFiles}" Value="{Binding CopiedFiles}"
                       ShowValue="True" ValueStringFormat="{}{0:0}" />
```

Other colors:

```xml
<rice:RiceProgressRing IsIndeterminate="True"
                       Foreground="{DynamicResource RiceDangerBrush}" Background="Transparent" />
```

## Styling

Pseudo classes: `:determinate`, `:indeterminate`, `:showvalue`.
Template parts: `PART_Track` and `PART_Indicator` (`RiceProgressArc`), `PART_Text` (`TextBlock`).
The spinning is a `Style.Animations` animation of `PART_Indicator`'s `StartAngle` / `SweepAngle` under
`:indeterminate` (1.5 s per turn); override it in a style to change the speed or the motion.

`RiceProgressArc` is a public primitive that draws one arc (`StartAngle`, `SweepAngle` in degrees, 0 = right side,
clockwise; `Stroke`, `StrokeThickness`) – useful for custom gauges and rings.
