using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
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
        var fluent = this.OfType<FluentTheme>().First();
        ThemeManager.Instance.Attach(Resources, fluent);
        RoundButtons(fluent);
    }

    // The Fluent button themes take ControlCornerRadius like the inputs; these themes based on them give the buttons
    // RiceButtonCornerRadius instead. Corner radii set by styles or other control themes still win over it.
    private void RoundButtons(FluentTheme fluent)
    {
        foreach (var type in new[] { typeof(Button), typeof(ToggleButton), typeof(RepeatButton), typeof(DropDownButton), typeof(SplitButton) })
        {
            if (fluent.TryGetResource(type, null, out var resource) && resource is ControlTheme basedOn)
                Resources[type] = new ControlTheme(type)
                {
                    BasedOn = basedOn,
                    Setters = { new Setter(TemplatedControl.CornerRadiusProperty, new DynamicResourceExtension("RiceButtonCornerRadius")) },
                };
        }
    }
}
