using Avalonia.Media;

namespace KD.Avalonia.Rice.Theming;

/// <summary>Themes inspired by Linux distributions and popular color schemes.</summary>
public static class BuiltInThemes
{
    public static RiceTheme Ubuntu { get; } = new("ubuntu", "Ubuntu",
        Light: new(C("#E95420"), C("#FAFAFA"), C("#FFFFFF"), C("#3D3D3D"))
        {
            TitleBarBackground = C("#EBEBEB"),
            Danger = C("#C7162B"),
        },
        Dark: new(C("#E95420"), C("#2C2C2C"), C("#3D3D3D"), C("#F7F7F7"))
        {
            TitleBarBackground = C("#300A24"),
            Danger = C("#C7162B"),
        });

    public static RiceTheme Fedora { get; } = new("fedora", "Fedora",
        Light: new(C("#3C6EB4"), C("#F5F7FA"), C("#FFFFFF"), C("#1E2A3A"))
        {
            TitleBarBackground = C("#E4EAF2"),
        },
        Dark: new(C("#51A2DA"), C("#1A2433"), C("#22314A"), C("#E8EEF6"))
        {
            TitleBarBackground = C("#294172"),
        });

    public static RiceTheme Manjaro { get; } = new("manjaro", "Manjaro",
        Light: new(C("#16A085"), C("#F2F5F4"), C("#FFFFFF"), C("#222D32"))
        {
            TitleBarBackground = C("#E3EBE8"),
        },
        Dark: new(C("#35BF5C"), C("#222D32"), C("#2B3A40"), C("#E7EFEC"))
        {
            TitleBarBackground = C("#1B2428"),
        });

    public static RiceTheme OpenSuse { get; } = new("opensuse", "openSUSE",
        Light: new(C("#5E9C1B"), C("#F4F8F0"), C("#FFFFFF"), C("#173F4F"))
        {
            TitleBarBackground = C("#E6EFDC"),
        },
        Dark: new(C("#73BA25"), C("#12313D"), C("#173F4F"), C("#EAF2E0"))
        {
            TitleBarBackground = C("#0E2731"),
        });

    public static RiceTheme Nord { get; } = new("nord", "Nord",
        Light: new(C("#5E81AC"), C("#ECEFF4"), C("#E5E9F0"), C("#2E3440"))
        {
            TitleBarBackground = C("#D8DEE9"),
            Danger = C("#BF616A"),
        },
        Dark: new(C("#88C0D0"), C("#2E3440"), C("#3B4252"), C("#ECEFF4"))
        {
            TitleBarBackground = C("#272C36"),
            Danger = C("#BF616A"),
        });

    public static RiceTheme Solarized { get; } = new("solarized", "Solarized",
        Light: new(C("#268BD2"), C("#FDF6E3"), C("#EEE8D5"), C("#586E75"))
        {
            TitleBarBackground = C("#EEE8D5"),
            Danger = C("#DC322F"),
        },
        Dark: new(C("#268BD2"), C("#002B36"), C("#073642"), C("#93A1A1"))
        {
            TitleBarBackground = C("#073642"),
            Danger = C("#DC322F"),
        });

    public static RiceTheme Dracula { get; } = new("dracula", "Dracula",
        Light: new(C("#644AC9"), C("#FFFBEB"), C("#F5F1DC"), C("#1F1F1F"))
        {
            TitleBarBackground = C("#ECE7CF"),
            Danger = C("#CB3A2A"),
        },
        Dark: new(C("#BD93F9"), C("#282A36"), C("#44475A"), C("#F8F8F2"))
        {
            TitleBarBackground = C("#21222C"),
            Danger = C("#FF5555"),
        });

    public static RiceTheme Gruvbox { get; } = new("gruvbox", "Gruvbox",
        Light: new(C("#AF3A03"), C("#FBF1C7"), C("#F2E5BC"), C("#3C3836"))
        {
            TitleBarBackground = C("#EBDBB2"),
            Danger = C("#CC241D"),
        },
        Dark: new(C("#FE8019"), C("#282828"), C("#3C3836"), C("#EBDBB2"))
        {
            TitleBarBackground = C("#1D2021"),
            Danger = C("#FB4934"),
        });

    public static RiceTheme Catppuccin { get; } = new("catppuccin", "Catppuccin",
        Light: new(C("#8839EF"), C("#EFF1F5"), C("#E6E9EF"), C("#4C4F69"))
        {
            TitleBarBackground = C("#DCE0E8"),
            Danger = C("#D20F39"),
        },
        Dark: new(C("#CBA6F7"), C("#1E1E2E"), C("#313244"), C("#CDD6F4"))
        {
            TitleBarBackground = C("#181825"),
            Danger = C("#F38BA8"),
        });

    public static IReadOnlyList<RiceTheme> All { get; } =
        [Ubuntu, Fedora, Manjaro, OpenSuse, Nord, Solarized, Dracula, Gruvbox, Catppuccin];

    private static Color C(string hex) => Color.Parse(hex);
}
