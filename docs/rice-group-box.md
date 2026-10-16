# RiceGroupBox

Frame around related controls with the title on the top line, like the WinForms `GroupBox`. The line is cut where the
title is (it is drawn with a gap, not hidden behind a painted background), so the group box works on any background.

```xml
<rice:RiceGroupBox Header="Network" Icon="{x:Static icons:MaterialIconKind.Tune}">
  <StackPanel Spacing="8">
    <CheckBox Content="Use DHCP" />
    <TextBox Text="192.168.1.10" />
  </StackPanel>
</rice:RiceGroupBox>
```

Without `Header` and `Icon` the frame is closed:

```xml
<rice:RiceGroupBox>
  <TextBlock Text="Just a frame" />
</rice:RiceGroupBox>
```

## Properties

| Property | Default | Description |
|----------|---------|-------------|
| `Header`, `HeaderTemplate` | `null` | Title on the top line (semi-bold). Any content works, e.g. a `CheckBox`. |
| `Icon` | `null` | `IImage`, `MaterialIconKind`, string (e.g. emoji) or any control, 16 × 16 px before the title; Material icons use the accent color. |
| `BorderBrush` | `RiceBorderBrush` | Line color. |
| `BorderThickness` | `1` | |
| `CornerRadius` | `8` | |
| `Background` | `Transparent` | Fill inside the frame. |
| `Padding` | `12,8,12,12` | Space between the frame and the content. |

## Examples

A check box as title, enabling the content:

```xml
<rice:RiceGroupBox>
  <rice:RiceGroupBox.Header>
    <CheckBox Name="ProxyEnabled" Content="Use a proxy" />
  </rice:RiceGroupBox.Header>
  <TextBox IsEnabled="{Binding #ProxyEnabled.IsChecked}" PlaceholderText="proxy.example.com:8080" />
</rice:RiceGroupBox>
```

Filled, with an accent frame:

```xml
<rice:RiceGroupBox Header="Danger zone" Icon="⚠️"
                   BorderBrush="{DynamicResource RiceDangerBrush}"
                   Background="{DynamicResource RiceSurfaceAltBrush}">
  <Button Content="Delete account" />
</rice:RiceGroupBox>
```

## Styling

Template parts: `PART_Frame` (`RiceGroupBoxFrame`, draws the line with the gap), `PART_Header`, `PART_IconPresenter`,
`PART_HeaderPresenter`. The gap follows the header's size automatically.
