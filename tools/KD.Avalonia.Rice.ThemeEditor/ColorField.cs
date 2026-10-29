using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace KD.Avalonia.Rice.ThemeEditor;

/// <summary>Editor row of one palette color. Optional colors show the derived value until they are overridden.</summary>
public sealed class ColorField(PaletteField field) : INotifyPropertyChanged
{
    private Color _color;
    private bool _isOverridden;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raised when the user changes the color or the override.</summary>
    public event EventHandler? Changed;

    public PaletteField Field { get; } = field;

    public string Label => Field.Label;

    public bool IsOptional => !Field.IsCore;

    public bool CanEdit => Field.IsCore || IsOverridden;

    public bool IsOverridden
    {
        get => _isOverridden;
        set
        {
            if (_isOverridden == value || Field.IsCore)
                return;
            _isOverridden = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanEdit));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public Color Color
    {
        get => _color;
        set
        {
            value = Color.FromRgb(value.R, value.G, value.B);
            if (_color == value || !CanEdit)
                return;
            SetColor(value);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Hex
    {
        get => PaletteFields.ToHex(_color);
        set
        {
            if (Color.TryParse(value.Trim(), out var color))
                Color = color;
            else
                OnPropertyChanged(); // puts the last valid value back
        }
    }

    /// <summary>The value to store and build from; null while an optional color is derived.</summary>
    public Color? StoredValue => CanEdit ? _color : null;

    /// <summary>Loads a value without raising <see cref="Changed"/>.</summary>
    public void Load(Color color, bool isOverridden)
    {
        if (!Field.IsCore && _isOverridden != isOverridden)
        {
            _isOverridden = isOverridden;
            OnPropertyChanged(nameof(IsOverridden));
            OnPropertyChanged(nameof(CanEdit));
        }
        SetColor(color);
    }

    /// <summary>Shows the derived value of an optional color that is not overridden.</summary>
    public void ShowDerived(Color color)
    {
        if (!CanEdit && _color != color)
            SetColor(color);
    }

    private void SetColor(Color color)
    {
        _color = color;
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(Hex));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
