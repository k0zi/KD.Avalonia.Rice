using Avalonia.Controls;
using KD.Avalonia.Rice.Controls;

namespace KD.Avalonia.Rice.Demo;

public partial class CommandPalettePage : UserControl
{
    private static readonly (string Name, string Family)[] Distros =
    [
        ("Ubuntu", "Debian"), ("Debian", "Debian"), ("Linux Mint", "Debian"), ("Pop!_OS", "Debian"),
        ("Fedora", "Red Hat"), ("Rocky Linux", "Red Hat"), ("AlmaLinux", "Red Hat"),
        ("openSUSE Tumbleweed", "SUSE"), ("openSUSE Leap", "SUSE"),
        ("Arch Linux", "Arch"), ("Manjaro", "Arch"), ("EndeavourOS", "Arch"),
        ("NixOS", "Independent"), ("Gentoo", "Independent"), ("Void Linux", "Independent"),
    ];

    public CommandPalettePage()
    {
        InitializeComponent();
        OpenButton.Click += (_, _) => (TopLevel.GetTopLevel(this) as RiceWindow)?.ShowCommandPalette();
        OpenWithQueryButton.Click += (_, _) => (TopLevel.GetTopLevel(this) as RiceWindow)?.ShowCommandPalette("theme");

        EmbeddedPalette.Commands = Distros
            .Select(d => new RiceCommandItem(d.Name, () => { }) { Category = d.Family })
            .ToList();
        // Embedded, the palette stays open: Enter / click only report the choice.
        EmbeddedPalette.CommandInvoked += (_, command) => EmbeddedResult.Text = $"Picked: {command.Title}";
    }
}
