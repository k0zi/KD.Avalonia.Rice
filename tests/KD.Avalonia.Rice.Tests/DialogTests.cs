using KD.Avalonia.Rice.Dialogs;

namespace KD.Avalonia.Rice.Tests;

public class DialogTests
{
    [Theory]
    [InlineData(DialogButtons.Ok, new[] { DialogResult.Ok }, DialogResult.Ok)]
    [InlineData(DialogButtons.OkCancel, new[] { DialogResult.Ok, DialogResult.Cancel }, DialogResult.Cancel)]
    [InlineData(DialogButtons.YesNo, new[] { DialogResult.Yes, DialogResult.No }, DialogResult.No)]
    [InlineData(DialogButtons.YesNoCancel, new[] { DialogResult.Yes, DialogResult.No, DialogResult.Cancel }, DialogResult.Cancel)]
    [InlineData(DialogButtons.RetryCancel, new[] { DialogResult.Retry, DialogResult.Cancel }, DialogResult.Cancel)]
    [InlineData(DialogButtons.AbortRetryIgnore, new[] { DialogResult.Abort, DialogResult.Retry, DialogResult.Ignore }, null)]
    [InlineData(DialogButtons.Close, new[] { DialogResult.Close }, DialogResult.Close)]
    public void Button_sets_have_their_results_default_and_cancel(DialogButtons set, DialogResult[] results, DialogResult? cancel)
    {
        var buttons = set.ToButtons();

        Assert.Equal(results, buttons.Select(b => (DialogResult)b.Result!));
        Assert.True(buttons[0].IsDefault && buttons[0].IsAccent);
        Assert.All(buttons.Skip(1), b => Assert.False(b.IsDefault || b.IsAccent));
        Assert.Equal(cancel, buttons.SingleOrDefault(b => b.IsCancel)?.Result as DialogResult?);
    }

    [Fact]
    public void Standard_buttons_have_localized_captions()
    {
        Assert.Equal(RiceStrings.Yes, DialogButton.For(DialogResult.Yes).Text);
        Assert.True(DialogButton.For(DialogResult.Cancel).IsCancel);
        Assert.True(DialogButton.For(DialogResult.Ok).ClosesDialog);
    }

    [Fact]
    public void Linux_lookup_picks_the_closest_png_through_inherited_themes()
    {
        using var root = new TempDirectory();
        root.Write("child/index.theme", """
            [Icon Theme]
            Name=Child
            Inherits=parent
            Directories=48x48/status

            [48x48/status]
            Size=48
            """);
        root.Write("child/48x48/status/dialog-error.svg", "");
        root.Write("parent/index.theme", """
            [Icon Theme]
            Name=Parent
            Directories=16x16/status,32x32/status,48x48@2x/status,256x256/status

            [16x16/status]
            Size=16

            [32x32/status]
            Size=32

            [48x48@2x/status]
            Size=48
            Scale=2

            [256x256/status]
            Size=256
            """);
        foreach (var dir in new[] { "16x16", "32x32", "48x48@2x", "256x256" })
            root.Write($"parent/{dir}/status/dialog-error.png", "");
        root.Write("hicolor/index.theme", "[Icon Theme]\nName=Hicolor\nDirectories=48x48/status\n\n[48x48/status]\nSize=48\n");
        root.Write("hicolor/48x48/status/dialog-warning.png", "");

        string[] roots = [root.Path];
        var error = LinuxDialogIcons.IconNames(DialogIcon.Error);

        // The SVG of the child theme is skipped. Size and scale match exactly, else the closest not smaller in pixels wins.
        Assert.EndsWith("parent/48x48@2x/status/dialog-error.png", LinuxDialogIcons.FindIcon(error, "child", roots, 48, 2));
        Assert.EndsWith("parent/32x32/status/dialog-error.png", LinuxDialogIcons.FindIcon(error, "child", roots, 32, 1));
        Assert.EndsWith("parent/48x48@2x/status/dialog-error.png", LinuxDialogIcons.FindIcon(error, "child", roots, 48, 1));
        Assert.EndsWith("parent/256x256/status/dialog-error.png", LinuxDialogIcons.FindIcon(error, "child", roots, 100, 2));
        // hicolor is searched after every inherited theme.
        Assert.EndsWith("hicolor/48x48/status/dialog-warning.png",
            LinuxDialogIcons.FindIcon(LinuxDialogIcons.IconNames(DialogIcon.Warning), "child", roots, 48, 1));
        Assert.Null(LinuxDialogIcons.FindIcon(LinuxDialogIcons.IconNames(DialogIcon.Question), "child", roots, 48, 1));
        Assert.Null(LinuxDialogIcons.FindIcon(error, "missing-theme", [System.IO.Path.Combine(root.Path, "none")], 48, 1));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("rice-icons-").FullName;

        public void Write(string relativePath, string content)
        {
            var file = System.IO.Path.Combine(Path, relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
            File.WriteAllText(file, content);
        }

        public void Dispose() => Directory.Delete(Path, true);
    }
}
