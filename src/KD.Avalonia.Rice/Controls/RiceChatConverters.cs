using System.Globalization;
using Avalonia.Data.Converters;

namespace KD.Avalonia.Rice.Controls;

/// <summary>Converters used by the <see cref="RiceChat"/> templates.</summary>
public static class RiceChatConverters
{
    /// <summary>
    /// Formats a timestamp (value 0) with a format string (value 1) in local time.
    /// The optional parameter wraps the result, e.g. <c>[{0}]</c>.
    /// </summary>
    public static IMultiValueConverter Timestamp { get; } = new TimestampConverter();

    private sealed class TimestampConverter : IMultiValueConverter
    {
        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            var format = values.Count > 1 && values[1] is string { Length: > 0 } f ? f : "HH:mm";
            var text = values.Count > 0 ? values[0] switch
            {
                DateTimeOffset dto => dto.ToLocalTime().ToString(format, culture),
                DateTime dt => dt.ToString(format, culture),
                _ => "",
            } : "";
            return parameter is string wrap ? string.Format(culture, wrap, text) : text;
        }
    }
}
