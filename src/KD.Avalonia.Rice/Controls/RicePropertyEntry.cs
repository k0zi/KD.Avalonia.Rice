using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One row of a <see cref="RicePropertyGrid"/>: a public property with the name, category and description shown for it,
/// read from <c>[DisplayName]</c>, <c>[Category]</c>, <c>[Description]</c>, <c>[ReadOnly]</c> or <c>[Display]</c>.
/// </summary>
public sealed class RicePropertyEntry
{
    internal RicePropertyEntry(PropertyInfo property, string displayName, string category, string? description,
        bool isReadOnly, int order, double? minimum, double? maximum)
    {
        Property = property;
        DisplayName = displayName;
        Category = category;
        Description = description;
        IsReadOnly = isReadOnly;
        Order = order;
        Minimum = minimum;
        Maximum = maximum;
    }

    public PropertyInfo Property { get; }

    public string Name => Property.Name;

    public Type PropertyType => Property.PropertyType;

    public string DisplayName { get; }

    public string Category { get; }

    public string? Description { get; }

    /// <summary>No public setter, or marked <c>[ReadOnly(true)]</c> / <c>[Editable(false)]</c>.</summary>
    public bool IsReadOnly { get; }

    /// <summary><c>[Display(Order = …)]</c>; 0 when not set.</summary>
    public int Order { get; }

    /// <summary>From <c>[Range]</c>, for numeric editors.</summary>
    public double? Minimum { get; }

    public double? Maximum { get; }

    public object? GetValue(object target) => Property.GetValue(target);

    public void SetValue(object target, object? value) => Property.SetValue(target, value);

    /// <summary>
    /// The browsable public instance properties of <paramref name="type"/> (not indexers, not
    /// <c>[Browsable(false)]</c> / <c>[Display(AutoGenerateField = false)]</c>), in declaration order.
    /// </summary>
    public static IReadOnlyList<RicePropertyEntry> FromType(Type type)
    {
        var entries = new List<RicePropertyEntry>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetMethod?.IsPublic != true || property.GetIndexParameters().Length > 0)
                continue;
            if (property.GetCustomAttribute<BrowsableAttribute>() is { Browsable: false })
                continue;
            var display = property.GetCustomAttribute<DisplayAttribute>();
            if (display?.GetAutoGenerateField() == false)
                continue;

            var readOnly = property.SetMethod?.IsPublic != true
                           || property.GetCustomAttribute<ReadOnlyAttribute>() is { IsReadOnly: true }
                           || property.GetCustomAttribute<EditableAttribute>() is { AllowEdit: false };
            var range = property.GetCustomAttribute<RangeAttribute>();
            entries.Add(new RicePropertyEntry(
                property,
                display?.GetName() ?? property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? Humanize(property.Name),
                display?.GetGroupName() ?? property.GetCustomAttribute<CategoryAttribute>()?.Category ?? RiceStrings.PropertyGridMisc,
                display?.GetDescription() ?? property.GetCustomAttribute<DescriptionAttribute>()?.Description,
                readOnly,
                display?.GetOrder() ?? 0,
                ToDouble(range?.Minimum),
                ToDouble(range?.Maximum)));
        }
        // GetProperties lists a derived class first; the base class properties come first instead.
        return entries
            .OrderBy(e => Depth(e.Property.DeclaringType))
            .ThenBy(e => e.Order)
            .ToList();

        static double? ToDouble(object? value) => value is IConvertible convertible ? convertible.ToDouble(null) : null;

        static int Depth(Type? declaring)
        {
            var depth = 0;
            for (var t = declaring?.BaseType; t is not null; t = t.BaseType)
                depth++;
            return depth;
        }
    }

    /// <summary>"FontSize" → "Font size", "HTTPProxy" → "HTTP proxy", "Ipv6Address" → "Ipv6 address".</summary>
    public static string Humanize(string name)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c == '_')
            {
                Flush();
                continue;
            }
            var startsWord = i > 0 && char.IsUpper(c)
                             && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1])
                                 || (i + 1 < name.Length && char.IsLower(name[i + 1]) && char.IsUpper(name[i - 1])));
            if (startsWord)
                Flush();
            word.Append(c);
        }
        Flush();

        // Acronyms keep their case; the other words after the first are lower case.
        for (var i = 1; i < words.Count; i++)
        {
            if (!words[i].All(char.IsUpper))
                words[i] = char.ToLowerInvariant(words[i][0]) + words[i][1..];
        }
        return string.Join(' ', words);

        void Flush()
        {
            if (word.Length > 0)
                words.Add(word.ToString());
            word.Clear();
        }
    }
}
