using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.Tests;

public class ThemeManagerTests
{
    private static ResourceDictionary Brushes(ResourceDictionary resources, ThemeVariant variant) =>
        (ResourceDictionary)((ResourceDictionary)resources.MergedDictionaries.Single()).ThemeDictionaries[variant];

    [Fact]
    public void AttachesToSeveralStyleInstances()
    {
        // E.g. a second Application in the same process (headless tests) or RiceStyles added twice:
        // resource providers can have only one owner, so every attachment needs its own dictionaries.
        var first = new ResourceDictionary();
        var second = new ResourceDictionary();
        var firstFluent = new FluentTheme();
        var secondFluent = new FluentTheme();

        ThemeManager.Instance.Attach(first, firstFluent);
        ThemeManager.Instance.Attach(second, secondFluent);

        Assert.NotSame(first.MergedDictionaries.Single(), second.MergedDictionaries.Single());
        Assert.NotSame(firstFluent.Palettes[ThemeVariant.Light], secondFluent.Palettes[ThemeVariant.Light]);
        Assert.True(Brushes(second, ThemeVariant.Light).ContainsKey("RiceAccentBrush"));
    }

    [Fact]
    public void ThemeChangesReachEveryAttachment()
    {
        var first = new ResourceDictionary();
        var second = new ResourceDictionary();
        var firstFluent = new FluentTheme();
        var secondFluent = new FluentTheme();
        ThemeManager.Instance.Attach(first, firstFluent);
        ThemeManager.Instance.Attach(second, secondFluent);

        var original = ThemeManager.Instance.CurrentTheme;
        var other = ThemeManager.Instance.SupportedThemes.First(t => t != original);
        try
        {
            ThemeManager.Instance.SetTheme(other);

            foreach (var resources in new[] { first, second })
            {
                var accent = (SolidColorBrush)Brushes(resources, ThemeVariant.Light)["RiceAccentBrush"]!;
                Assert.Equal(other.Light.Accent, accent.Color);
            }

            Assert.Equal(other.Dark.Accent, secondFluent.Palettes[ThemeVariant.Dark].Accent);
        }
        finally
        {
            ThemeManager.Instance.SetTheme(original);
        }
    }

    [Fact]
    public void ProvidesTintedToastBrushesForEveryKind()
    {
        var resources = new ResourceDictionary();
        ThemeManager.Instance.Attach(resources, new FluentTheme());
        var brushes = Brushes(resources, ThemeVariant.Light);
        var palette = ThemeManager.Instance.CurrentTheme.Light;

        foreach (var (kind, color) in new[] { ("Info", palette.Accent), ("Success", palette.Success), ("Warning", palette.Warning), ("Error", palette.Danger) })
        {
            Assert.Equal(color, ((SolidColorBrush)brushes[$"RiceToast{kind}Brush"]!).Color);
            Assert.NotEqual(color, ((SolidColorBrush)brushes[$"RiceToast{kind}BackgroundBrush"]!).Color);
            Assert.True(brushes.ContainsKey($"RiceToast{kind}BorderBrush"));
        }
        Assert.Equal(palette.Success, ((SolidColorBrush)brushes["RiceSuccessBrush"]!).Color);
        Assert.Equal(palette.Warning, ((SolidColorBrush)brushes["RiceWarningBrush"]!).Color);
    }
}
