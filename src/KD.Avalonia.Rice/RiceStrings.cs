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
}
