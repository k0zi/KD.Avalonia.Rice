using Avalonia;
using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;
using KD.Avalonia.Rice.Options;

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
}
