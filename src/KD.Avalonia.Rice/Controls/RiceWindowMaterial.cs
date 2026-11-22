namespace KD.Avalonia.Rice.Controls;

/// <summary>What the background of a <see cref="RiceWindow"/> is made of.</summary>
public enum RiceWindowMaterial
{
    /// <summary>An opaque background in the theme's colors.</summary>
    Solid,

    /// <summary>
    /// Soft color blobs of the theme's accent over the background (see <see cref="RiceGradientBackground"/>); the large
    /// surfaces of the Rice controls turn translucent over it. Works on every platform.
    /// </summary>
    Gradient,

    /// <summary>
    /// Frosted glass: the desktop shows through blurred (Mica / Acrylic on Windows, vibrancy on macOS, blur on KDE).
    /// Falls back to <see cref="Solid"/> where the platform cannot blur.
    /// </summary>
    Glass,
}
