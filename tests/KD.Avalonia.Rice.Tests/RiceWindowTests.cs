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
    public void Window_is_transparent_only_for_rounded_corners()
    {
        Assert.Empty(RiceWindow.GetTransparencyHint(new CornerRadius(0)));
        Assert.Equal([WindowTransparencyLevel.Transparent], RiceWindow.GetTransparencyHint(new CornerRadius(8)));
    }

    [Fact]
    public void Windows_are_gradient_by_default() => Assert.Equal(RiceWindowMaterial.Gradient, new RiceOptions().WindowMaterial);

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
    public void Shading_keeps_hue_and_saturation()
    {
        var accent = Color.Parse("#E95420");

        var lighter = ColorMath.Shade(accent, 0.12).ToHsl();
        var original = accent.ToHsl();

        Assert.Equal(original.H, lighter.H, 1.0);
        Assert.Equal(original.S, lighter.S, 2);
        Assert.Equal(original.L + 0.12, lighter.L, 2);
        Assert.Equal(0.1, ColorMath.Shade(accent, -1).ToHsl().L, 2);
    }
}
