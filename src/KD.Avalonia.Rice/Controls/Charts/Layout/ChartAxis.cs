using System.Globalization;

namespace KD.Avalonia.Rice.Controls.Charts;

/// <summary>A value range with round tick steps (1, 2, 2.5, 5 × 10ⁿ).</summary>
internal readonly record struct ChartScale(double Min, double Max, double Step)
{
    public double Range => Max - Min;

    public IEnumerable<double> Ticks
    {
        get
        {
            if (Step <= 0)
                yield break;
            var count = (int)Math.Round(Range / Step);
            for (var i = 0; i <= count; i++)
                yield return Clean(Min + i * Step);
        }
    }

    /// <summary>Position of <paramref name="value"/> between <paramref name="from"/> (at Min) and <paramref name="to"/> (at Max).</summary>
    public double Map(double value, double from, double to) => Range <= 0 ? from : from + (value - Min) / Range * (to - from);

    // 0.1 + 0.2 shows as 0.30000000000000004 otherwise.
    private double Clean(double value) => Math.Abs(value) < Step * 1e-9 ? 0 : Math.Round(value, 10);
}

internal static class ChartAxis
{
    /// <summary>
    /// A scale covering [<paramref name="min"/>, <paramref name="max"/>] with at most about <paramref name="maxTicks"/>
    /// round ticks, e.g. 0–97 gives 0, 20, …, 100. With <paramref name="includeZero"/> the scale starts or ends at 0.
    /// </summary>
    public static ChartScale Nice(double min, double max, int maxTicks = 6, bool includeZero = true)
    {
        if (double.IsNaN(min) || double.IsInfinity(min))
            min = 0;
        if (double.IsNaN(max) || double.IsInfinity(max))
            max = 0;
        if (min > max)
            (min, max) = (max, min);
        if (includeZero)
        {
            min = Math.Min(0, min);
            max = Math.Max(0, max);
        }
        if (max - min < 1e-12)
        {
            // A single value: a range around it.
            var pad = Math.Abs(max) > 1e-12 ? Math.Abs(max) * 0.5 : 1;
            if (includeZero && min >= 0)
                max += pad * 2;
            else
            {
                min -= pad;
                max += pad;
            }
        }

        maxTicks = Math.Max(2, maxTicks);
        var step = NiceStep((max - min) / (maxTicks - 1));
        var niceMin = Math.Floor(min / step + 1e-9) * step;
        var niceMax = Math.Ceiling(max / step - 1e-9) * step;
        return new ChartScale(niceMin, niceMax, step);
    }

    /// <summary>The smallest round step (1, 2, 2.5, 5 × 10ⁿ) not below <paramref name="rough"/>.</summary>
    public static double NiceStep(double rough)
    {
        if (rough <= 0 || double.IsNaN(rough))
            return 1;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var fraction = rough / magnitude;
        var nice = fraction <= 1 + 1e-9 ? 1 : fraction <= 2 + 1e-9 ? 2 : fraction <= 2.5 + 1e-9 ? 2.5 : fraction <= 5 + 1e-9 ? 5 : 10;
        return nice * magnitude;
    }

    /// <summary>How many ticks fit on an axis of <paramref name="length"/> pixels.</summary>
    public static int TickCount(double length, double spacing) => Math.Clamp((int)(length / Math.Max(1, spacing)) + 1, 2, 11);

    /// <summary>
    /// Formats a value: a .NET number format (<c>N1</c>, <c>P0</c>, <c>#,0.0 'kg'</c>, …) or, without one, up to two
    /// decimals with group separators and k / M / G for large numbers.
    /// </summary>
    public static string Format(double value, string? format)
    {
        var culture = CultureInfo.CurrentCulture;
        if (!string.IsNullOrEmpty(format))
            return value.ToString(format, culture);
        var abs = Math.Abs(value);
        return abs switch
        {
            >= 1e9 => (value / 1e9).ToString("0.##", culture) + "G",
            >= 1e6 => (value / 1e6).ToString("0.##", culture) + "M",
            >= 1e4 => (value / 1e3).ToString("0.##", culture) + "k",
            _ => value.ToString("#,0.##", culture),
        };
    }

    /// <summary>Date ticks for a time axis: days, weeks, months or years, depending on the span.</summary>
    public static (List<DateTime> Ticks, string Format) DateTicks(DateTime start, DateTime end, double width, double labelWidth)
    {
        var days = Math.Max(1, (end - start).TotalDays);
        var maxTicks = Math.Max(2, width / Math.Max(1, labelWidth));
        var ticks = new List<DateTime>();
        string format;
        if (days / 1 <= maxTicks)
        {
            format = "MMM d";
            for (var t = start.Date; t <= end; t = t.AddDays(1))
                ticks.Add(t);
        }
        else if (days / 7 <= maxTicks)
        {
            format = "MMM d";
            var t = start.Date.AddDays(((int)DayOfWeek.Monday - (int)start.DayOfWeek + 7) % 7);
            for (; t <= end; t = t.AddDays(7))
                ticks.Add(t);
        }
        else if (days / 30 <= maxTicks)
        {
            format = "MMM yyyy";
            var months = Math.Max(1, (int)Math.Ceiling(days / 30 / maxTicks));
            var t = new DateTime(start.Year, start.Month, 1).AddMonths(start.Day > 1 ? 1 : 0);
            for (; t <= end; t = t.AddMonths(months))
                ticks.Add(t);
        }
        else
        {
            format = "yyyy";
            var years = Math.Max(1, (int)Math.Ceiling(days / 365 / maxTicks));
            var t = new DateTime(start.Year + (start.DayOfYear > 1 ? 1 : 0), 1, 1);
            for (; t <= end; t = t.AddYears(years))
                ticks.Add(t);
        }
        return (ticks, format);
    }
}
