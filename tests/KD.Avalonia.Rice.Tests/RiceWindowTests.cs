using Avalonia;
using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Options;
using KD.Avalonia.Rice.Theming;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Tests;

public class RiceWindowTests
{
    [Fact]
    public void Glass_asks_for_blur_first_then_plain_transparency()
    {
        var hint = RiceWindow.GetTransparencyHint(RiceWindowMaterial.Glass, new CornerRadius(0));

        Assert.Equal(
        [
            WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur,
            WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent,
        ], hint);
    }

    [Fact]
    public void Solid_window_is_transparent_only_for_rounded_corners()
    {
        Assert.Empty(RiceWindow.GetTransparencyHint(RiceWindowMaterial.Solid, new CornerRadius(0)));
        Assert.Equal([WindowTransparencyLevel.Transparent], RiceWindow.GetTransparencyHint(RiceWindowMaterial.Solid, new CornerRadius(8)));
    }

    [Fact]
    public void Only_blurring_levels_count_as_glass()
    {
        Assert.True(RiceWindow.IsBlurred(WindowTransparencyLevel.Mica));
        Assert.True(RiceWindow.IsBlurred(WindowTransparencyLevel.AcrylicBlur));
        Assert.True(RiceWindow.IsBlurred(WindowTransparencyLevel.Blur));
        Assert.False(RiceWindow.IsBlurred(WindowTransparencyLevel.Transparent));
        Assert.False(RiceWindow.IsBlurred(WindowTransparencyLevel.None));
    }

    [Fact]
    public void Windows_are_solid_by_default() => Assert.Equal(RiceWindowMaterial.Solid, new RiceOptions().WindowMaterial);

    [Fact]
    public void Gradient_window_needs_no_more_transparency_than_a_solid_one()
    {
        Assert.Empty(RiceWindow.GetTransparencyHint(RiceWindowMaterial.Gradient, new CornerRadius(0)));
        Assert.Equal([WindowTransparencyLevel.Transparent], RiceWindow.GetTransparencyHint(RiceWindowMaterial.Gradient, new CornerRadius(8)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Gradient_blobs_drift_in_a_seamless_loop(int blob)
    {
        var start = RiceGradientBackground.BlobCenter(blob, 0);
        var end = RiceGradientBackground.BlobCenter(blob, 1);

        Assert.Equal(start.X, end.X, 6);
        Assert.Equal(start.Y, end.Y, 6);
        Assert.NotEqual(start, RiceGradientBackground.BlobCenter(blob, 0.25));
    }

    [Fact]
    public void Rotating_the_hue_keeps_saturation_and_lightness()
    {
        var accent = Color.Parse("#E95420");

        var rotated = ColorMath.RotateHue(accent, -50).ToHsl();
        var original = accent.ToHsl();

        Assert.Equal((original.H - 50 + 360) % 360, rotated.H, 1.0);
        Assert.Equal(original.S, rotated.S, 2);
        Assert.Equal(original.L, rotated.L, 2);
        Assert.Equal(accent, ColorMath.RotateHue(accent, 360));
    }
}
