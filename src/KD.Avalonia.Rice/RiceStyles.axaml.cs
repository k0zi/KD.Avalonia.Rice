using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice;

/// <summary>
/// Application styles: Fluent theme, Material icons and the Rice controls.
/// Add it to <c>Application.Styles</c> instead of <c>FluentTheme</c>.
/// </summary>
public class RiceStyles : Styles
{
    public RiceStyles()
    {
        AvaloniaXamlLoader.Load(this);
        ThemeManager.Instance.Attach(Resources, this.OfType<FluentTheme>().First());
    }
}
