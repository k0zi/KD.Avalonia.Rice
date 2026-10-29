using System.Windows.Input;
using Avalonia.Input;

namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// One entry of the <see cref="RiceCommandPalette"/>: what it shows (<see cref="Title"/>, <see cref="Description"/>,
/// <see cref="Category"/>, <see cref="Icon"/>, <see cref="Gesture"/>) and what it does (<see cref="Command"/> or
/// <see cref="Execute"/>). <see cref="Keywords"/> are found by the search without being shown.
/// </summary>
public class RiceCommandItem
{
    public RiceCommandItem()
    {
    }

    public RiceCommandItem(string title, Action execute)
    {
        Title = title;
        Execute = execute;
    }

    public string Title { get; set; } = "";

    /// <summary>Muted second line below the title.</summary>
    public string? Description { get; set; }

    /// <summary>Muted text on the right, e.g. "View" or "File"; the search finds it too.</summary>
    public string? Category { get; set; }

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon { get; set; }

    /// <summary>
    /// Shown as the shortcut of the entry. A <see cref="RiceWindow"/> also runs the entry when this gesture is pressed
    /// in it (for entries of its <see cref="RiceWindow.Commands"/>).
    /// </summary>
    public KeyGesture? Gesture { get; set; }

    /// <summary>Extra search terms, e.g. synonyms ("dark light mode" for a theme toggle).</summary>
    public string? Keywords { get; set; }

    public ICommand? Command { get; set; }

    public object? CommandParameter { get; set; }

    /// <summary>Run when the entry is chosen, before <see cref="Command"/>.</summary>
    public Action? Execute { get; set; }

    /// <summary>
    /// Runs the entry without closing the palette, e.g. one that switches the palette to another mode or list.
    /// The palette keeps its search box focused.
    /// </summary>
    public bool KeepOpen { get; set; }

    /// <summary>The <see cref="Gesture"/> as shown on this platform, e.g. "Ctrl+Shift+P".</summary>
    public string? GestureText => Gesture?.ToString("p", null);

    /// <summary>False while <see cref="Command"/> cannot execute: the palette shows the entry dimmed.</summary>
    public bool CanInvoke() => Command?.CanExecute(CommandParameter) ?? true;

    public void Invoke()
    {
        if (!CanInvoke())
            return;
        Execute?.Invoke();
        Command?.Execute(CommandParameter);
    }

    public override string ToString() => Title;
}
