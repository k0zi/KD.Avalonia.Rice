using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>
/// The operating system's dialog icons: the shell's stock icons on Windows, the icon theme's PNG icons on Linux.
/// <c>null</c> where none can be loaded (macOS, SVG only icon themes); dialogs then show a Material icon.
/// </summary>
public static class SystemDialogIcons
{
    private static readonly ConcurrentDictionary<(DialogIcon, int, int), IImage?> Cache = new();

    /// <summary>The icon for <paramref name="size"/> device independent pixels on a screen with <paramref name="renderScaling"/>.</summary>
    public static IImage? Get(DialogIcon icon, int size = 48, double renderScaling = 1)
    {
        if (icon == DialogIcon.None)
            return null;
        var scale = Math.Max(1, (int)Math.Ceiling(renderScaling - 0.01));
        return Cache.GetOrAdd((icon, size, scale), key => Load(key.Item1, key.Item2, key.Item3));
    }

    private static IImage? Load(DialogIcon icon, int size, int scale)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return WindowsDialogIcons.Load(icon, size * scale);
            if (OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
                return LinuxDialogIcons.Find(icon, size, scale) is { } path ? new Bitmap(path) : null;
        }
        catch (Exception)
        {
            // A broken icon file: fall back to the Material icon.
        }
        return null;
    }
}
