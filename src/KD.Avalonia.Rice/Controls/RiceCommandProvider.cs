namespace KD.Avalonia.Rice.Controls;

/// <summary>
/// A mode of the <see cref="RiceCommandPalette"/>, chosen by typing its <see cref="Prefix"/> first: "@ chr" searches only
/// the pages, "> theme" only the commands, "= 2*21" could compute. The text after the prefix filters <see cref="Commands"/>
/// or is handed to <see cref="GetCommands"/>. Typing "?" lists the modes.
/// </summary>
public class RiceCommandProvider
{
    public RiceCommandProvider()
    {
    }

    public RiceCommandProvider(string prefix, string title, IEnumerable<RiceCommandItem> commands)
    {
        Prefix = prefix;
        Title = title;
        Commands = commands;
    }

    /// <summary>Text that starts the mode, e.g. "@" or "#"; one or more characters, without spaces.</summary>
    public string Prefix { get; set; } = "";

    /// <summary>Shown in the "?" list, e.g. "Go to a page".</summary>
    public string Title { get; set; } = "";

    /// <summary>An <c>IImage</c>, a <c>MaterialIconKind</c>, a string (e.g. an emoji) or any control.</summary>
    public object? Icon { get; set; }

    /// <summary>The entries of the mode, when <see cref="GetCommands"/> is not set.</summary>
    public IEnumerable<RiceCommandItem> Commands { get; set; } = [];

    /// <summary>
    /// Builds the entries from the text after the prefix (trimmed), e.g. "Go to line 42" for "#42". Called on every
    /// change of the search text, so keep it fast.
    /// </summary>
    public Func<string, IEnumerable<RiceCommandItem>>? GetCommands { get; set; }

    /// <summary>
    /// Ranks the entries by the text after the prefix like the plain search does. Default true; set it to false when
    /// <see cref="GetCommands"/> already returns exactly what should be listed.
    /// </summary>
    public bool FilterResults { get; set; } = true;
}
