using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace KD.Avalonia.Rice.Controls.Uml;

/// <summary>Measures text with the font the canvas draws with.</summary>
internal sealed class AvaloniaTextMeasurer(FontFamily family) : IUmlTextMeasurer
{
    private readonly Dictionary<(string, double, bool, bool), Size> _cache = [];

    public FontFamily Family { get; } = family;

    public Typeface Typeface(bool bold, bool italic) =>
        new(Family, italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.SemiBold : FontWeight.Normal);

    public FormattedText Format(string text, double fontSize, bool bold, bool italic, IBrush? brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface(bold, italic), fontSize, brush);

    public Size Measure(string text, double fontSize, bool bold = false, bool italic = false)
    {
        var key = (text, fontSize, bold, italic);
        if (_cache.TryGetValue(key, out var size))
            return size;
        var formatted = Format(text, fontSize, bold, italic, null);
        size = new Size(Math.Ceiling(formatted.WidthIncludingTrailingWhitespace), Math.Ceiling(formatted.Height));
        if (_cache.Count > 4096)
            _cache.Clear();
        _cache[key] = size;
        return size;
    }
}
