namespace KD.Avalonia.Rice;

/// <summary>User visible texts of the library; assign them at startup to localize.</summary>
public static class RiceStrings
{
    public static string ToggleTheme { get; set; } = "Light / Dark";
    public static string ThemeSettings { get; set; } = "Theme settings";
    public static string Theme { get; set; } = "Theme";
    public static string About { get; set; } = "About";
    public static string Minimize { get; set; } = "Minimize";
    public static string Maximize { get; set; } = "Maximize";
    public static string Restore { get; set; } = "Restore";
    public static string Close { get; set; } = "Close";
    public static string Author { get; set; } = "Author";
    public static string Version { get; set; } = "Version";
    public static string Ok { get; set; } = "OK";
    public static string Cancel { get; set; } = "Cancel";
    public static string Yes { get; set; } = "Yes";
    public static string No { get; set; } = "No";
    public static string Retry { get; set; } = "Retry";
    public static string Abort { get; set; } = "Abort";
    public static string Ignore { get; set; } = "Ignore";
    public static string LightVariant { get; set; } = "Light";
    public static string DarkVariant { get; set; } = "Dark";
    public static string CollapseMenu { get; set; } = "Collapse menu";
    public static string ExpandMenu { get; set; } = "Expand menu";
    public static string ChatPlaceholder { get; set; } = "Type a message…";
    public static string Send { get; set; } = "Send";
    public static string UmlZoomIn { get; set; } = "Zoom in";
    public static string UmlZoomOut { get; set; } = "Zoom out";
    public static string UmlZoomToFit { get; set; } = "Fit to view";
    public static string UmlResetZoom { get; set; } = "Actual size (100 %)";
    /// <summary>Diagram reading problem; {0} is the line number, {1} the message.</summary>
    public static string UmlParseError { get; set; } = "Line {0}: {1}";
    public static string MarkdownCopy { get; set; } = "Copy";
    public static string MarkdownCopied { get; set; } = "Copied";
    public static string MarkdownExpandDiagram { get; set; } = "Open the diagram";
    public static string MarkdownCloseDiagram { get; set; } = "Close";
    /// <summary>Shown instead of an image that cannot be loaded; {0} is the alt text.</summary>
    public static string MarkdownImageFailed { get; set; } = "{0}";
    public static string MarkdownFootnotes { get; set; } = "Footnotes";
    public static string MarkdownBackToReference { get; set; } = "Back to the reference";
    /// <summary>Mermaid reading problem; {0} is the line number, {1} the message.</summary>
    public static string MermaidParseError { get; set; } = "Line {0}: {1}";
    /// <summary>Shown by a chart without data.</summary>
    public static string ChartNoData { get; set; } = "No data";
    /// <summary>Label under the total in the middle of a donut chart.</summary>
    public static string ChartTotal { get; set; } = "Total";
    /// <summary>Chart reading problem (Markdown <c>chart</c> blocks); {0} is the line number, {1} the message.</summary>
    public static string ChartParseError { get; set; } = "Line {0}: {1}";
    /// <summary>Tooltip of the close button of a toast.</summary>
    public static string ToastClose { get; set; } = "Close notification";
    /// <summary>Tooltip of the close button of an info bar.</summary>
    public static string InfoBarClose { get; set; } = "Close";
    public static string CommandPalettePlaceholder { get; set; } = "Type a command…";
    /// <summary>Placeholder of a palette with modes (prefixes).</summary>
    public static string CommandPalettePlaceholderWithModes { get; set; } = "Type a command, or ? for the modes…";
    /// <summary>Mode of the window's own commands in the command palette (prefix "&gt;").</summary>
    public static string CommandPaletteCommandsMode { get; set; } = "Commands";
    /// <summary>Mode of the side menu items in the command palette (prefix "@").</summary>
    public static string CommandPaletteNavigationMode { get; set; } = "Go to a page";
    public static string CommandPaletteNoResults { get; set; } = "No matching commands";
    /// <summary>Category of the side menu items in the command palette.</summary>
    public static string CommandPaletteNavigation { get; set; } = "Go to";
    public static string TabClose { get; set; } = "Close tab";
    public static string TabAdd { get; set; } = "New tab";
    public static string PropertyGridSearch { get; set; } = "Search properties…";
    /// <summary>Category of the properties without a <c>[Category]</c>.</summary>
    public static string PropertyGridMisc { get; set; } = "Misc";
    public static string PropertyGridCategorized { get; set; } = "Categorized";
    public static string PropertyGridAlphabetical { get; set; } = "Alphabetical";
    /// <summary>Tooltip of the chevron that minimizes the ribbon to its tabs.</summary>
    public static string RibbonCollapse { get; set; } = "Collapse the ribbon (Ctrl+F1)";
    /// <summary>Tooltip of the chevron of a minimized ribbon.</summary>
    public static string RibbonExpand { get; set; } = "Pin the ribbon (Ctrl+F1)";
    /// <summary>Tooltip of the launcher button in the corner of a ribbon group.</summary>
    public static string RibbonGroupLauncher { get; set; } = "More options";
}
