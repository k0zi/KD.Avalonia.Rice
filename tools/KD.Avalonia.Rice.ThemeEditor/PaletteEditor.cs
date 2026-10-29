using KD.Avalonia.Rice.Theming;

namespace KD.Avalonia.Rice.ThemeEditor;

/// <summary>The color rows of one theme variant (light or dark).</summary>
public sealed class PaletteEditor
{
    public PaletteEditor()
    {
        Fields = PaletteFields.All.Select(f => new ColorField(f)).ToList();
        foreach (var field in Fields)
            field.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<ColorField> Fields { get; }

    public IEnumerable<ColorField> CoreFields => Fields.Where(f => !f.IsOptional);

    public IEnumerable<ColorField> OptionalFields => Fields.Where(f => f.IsOptional);

    public void Load(RicePalette palette)
    {
        foreach (var field in Fields)
        {
            var stored = PaletteFields.StoredValue(palette, field.Field);
            field.Load(field.Field.Get(palette), stored is not null);
        }
    }

    /// <summary>Builds the palette and refreshes the derived colors shown in the rows.</summary>
    public RicePalette Build()
    {
        var palette = PaletteFields.Build(f => Fields.First(x => x.Field == f).StoredValue);
        foreach (var field in Fields)
            field.ShowDerived(field.Field.Get(palette));
        return palette;
    }
}
