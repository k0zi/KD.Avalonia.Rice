using Avalonia.Media;
using KD.Avalonia.Rice.Notifications;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Tests;

public class ToastTests
{
    [Theory]
    [InlineData(ToastKind.Info, 5)]
    [InlineData(ToastKind.Success, 5)]
    [InlineData(ToastKind.Warning, 5)]
    [InlineData(ToastKind.Error, 8)]
    public void Kinds_have_their_default_duration(ToastKind kind, int seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), ToastOptions.DefaultDuration(kind));
        Assert.Equal(TimeSpan.FromSeconds(seconds), new ToastOptions { Kind = kind }.EffectiveDuration);
        Assert.Equal(TimeSpan.Zero, new ToastOptions { Kind = kind, Duration = TimeSpan.Zero }.EffectiveDuration);
    }

    [Fact]
    public void Text_on_success_and_warning_is_derived_unless_set()
    {
        var palette = new RicePalette(Colors.Blue, Colors.White, Colors.White, Colors.Black)
        {
            Success = Color.Parse("#0E8420"),
            Warning = Color.Parse("#FCE100"),
        };

        Assert.Equal(Colors.White, palette.SuccessForeground);
        Assert.Equal(Color.Parse("#1A1A1A"), palette.WarningForeground);
        Assert.Equal(Colors.Red, (palette with { WarningForeground = Colors.Red }).WarningForeground);
    }
}
