using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Material.Icons;
using Material.Icons.Avalonia;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// Shows an <c>Icon</c> property: an <c>IImage</c> (scaled to fit), a <c>MaterialIconKind</c> (sized like the presenter),
/// a string (e.g. an emoji) or any control. Used by <see cref="RiceCard"/>, <see cref="RiceGroupBox"/> and
/// <see cref="RiceSideMenuItem"/>; styled as a plain <see cref="ContentPresenter"/>.
/// </summary>
public class RiceIconPresenter : ContentPresenter
{
    public RiceIconPresenter()
    {
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        DataTemplates.Add(new FuncDataTemplate<IImage>((image, _) => new Image { Source = image, Stretch = Stretch.Uniform }));
        DataTemplates.Add(new FuncDataTemplate<MaterialIconKind>((kind, _) => new MaterialIcon
        {
            Kind = kind,
            [!WidthProperty] = this[!WidthProperty],
            [!HeightProperty] = this[!HeightProperty],
        }));
    }

    protected override Type StyleKeyOverride => typeof(ContentPresenter);
}
