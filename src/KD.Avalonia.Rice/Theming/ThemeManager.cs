using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using KD.Avalonia.Rice.Options;

namespace KD.Avalonia.Rice.Theming;

/// <summary>
/// Owns the active <see cref="RiceTheme"/> and the light/dark variant of the application.
/// The theme choice is persisted when the app supports more than one theme;
/// the light/dark choice always starts from the system default and is never persisted.
/// </summary>
public sealed class ThemeManager : INotifyPropertyChanged
{
    private static readonly string[] AccentForegroundKeys =
    [
        "AccentButtonForeground", "AccentButtonForegroundPointerOver", "AccentButtonForegroundPressed",
        "ToggleButtonForegroundChecked", "ToggleButtonForegroundCheckedPointerOver", "ToggleButtonForegroundCheckedPressed",
        "CheckBoxCheckGlyphForegroundChecked", "CheckBoxCheckGlyphForegroundCheckedPointerOver", "CheckBoxCheckGlyphForegroundCheckedPressed",
        "RadioButtonCheckGlyphFill", "RadioButtonCheckGlyphFillPointerOver", "RadioButtonCheckGlyphFillPressed",
        "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPointerOver", "ToggleSwitchKnobFillOnPressed",
    ];

    private readonly ResourceDictionary _brushes = new();
    private readonly ColorPaletteResources _lightPalette = new();
    private readonly ColorPaletteResources _darkPalette = new();
    private ThemeSettingsStore? _store;
    private RiceTheme _currentTheme;

    public static ThemeManager Instance { get; } = new();

    private ThemeManager()
    {
        _brushes.ThemeDictionaries[ThemeVariant.Light] = new ResourceDictionary();
        _brushes.ThemeDictionaries[ThemeVariant.Dark] = new ResourceDictionary();
        _currentTheme = Options.SupportedThemes[0];
        Apply();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public RiceOptions Options { get; private set; } = new();

    public IReadOnlyList<RiceTheme> SupportedThemes => Options.SupportedThemes;

    /// <summary>True when the user can choose between more than one theme.</summary>
    public bool HasThemeChoice => SupportedThemes.Count > 1;

    public RiceTheme CurrentTheme
    {
        get => _currentTheme;
        set => SetTheme(value);
    }

    public bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    /// <summary>Applies the given options and restores the saved theme. Called by <c>UseRice</c>.</summary>
    public void Configure(RiceOptions options)
    {
        if (options.SupportedThemes.Count == 0)
            throw new ArgumentException("At least one supported theme is required.", nameof(options));

        Options = options;
        _store = new ThemeSettingsStore(options.AppInfo.AppId);

        var savedId = HasThemeChoice ? _store.LoadThemeId() : null;
        _currentTheme = Find(savedId) ?? Find(options.DefaultThemeId) ?? SupportedThemes[0];
        Apply();
        OnPropertyChanged(null);
    }

    public void SetTheme(string themeId) =>
        SetTheme(Find(themeId) ?? throw new ArgumentException($"Theme '{themeId}' is not supported.", nameof(themeId)));

    public void SetTheme(RiceTheme theme)
    {
        if (!SupportedThemes.Contains(theme))
            throw new ArgumentException($"Theme '{theme.Id}' is not supported.", nameof(theme));
        if (theme == _currentTheme)
            return;

        _currentTheme = theme;
        Apply();
        if (HasThemeChoice)
            _store?.SaveThemeId(theme.Id);
        OnPropertyChanged(nameof(CurrentTheme));
    }

    /// <summary>Switches between light and dark for this session only.</summary>
    public void ToggleVariant()
    {
        if (Application.Current is not { } app)
            return;
        app.RequestedThemeVariant = IsDark ? ThemeVariant.Light : ThemeVariant.Dark;
        OnPropertyChanged(nameof(IsDark));
    }

    /// <summary>Hooks the brushes and the Fluent palettes into <see cref="RiceStyles"/>.</summary>
    internal void Attach(IResourceDictionary resources, FluentTheme fluent)
    {
        resources.MergedDictionaries.Add(_brushes);
        fluent.Palettes[ThemeVariant.Light] = _lightPalette;
        fluent.Palettes[ThemeVariant.Dark] = _darkPalette;
    }

    private RiceTheme? Find(string? id) =>
        id is null ? null : SupportedThemes.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    private void Apply()
    {
        ApplyBrushes((ResourceDictionary)_brushes.ThemeDictionaries[ThemeVariant.Light], _currentTheme.Light);
        ApplyBrushes((ResourceDictionary)_brushes.ThemeDictionaries[ThemeVariant.Dark], _currentTheme.Dark);
        ApplyFluentPalette(_lightPalette, _currentTheme.Light, isDark: false);
        ApplyFluentPalette(_darkPalette, _currentTheme.Dark, isDark: true);
    }

    private static void ApplyBrushes(ResourceDictionary target, RicePalette p)
    {
        target["RiceAccentBrush"] = new SolidColorBrush(p.Accent);
        target["RiceAccentForegroundBrush"] = new SolidColorBrush(p.AccentForeground);
        target["RiceBackgroundBrush"] = new SolidColorBrush(p.Background);
        target["RiceSurfaceBrush"] = new SolidColorBrush(p.Surface);
        target["RiceSurfaceAltBrush"] = new SolidColorBrush(p.SurfaceAlt);
        target["RiceForegroundBrush"] = new SolidColorBrush(p.Foreground);
        target["RiceForegroundMutedBrush"] = new SolidColorBrush(p.ForegroundMuted);
        target["RiceBorderBrush"] = new SolidColorBrush(p.Border);
        target["RiceTitleBarBackgroundBrush"] = new SolidColorBrush(p.TitleBarBackground);
        target["RiceTitleBarForegroundBrush"] = new SolidColorBrush(p.TitleBarForeground);
        target["RiceTitleBarHoverBrush"] = new SolidColorBrush(ColorMath.Blend(p.TitleBarBackground, p.TitleBarForeground, 0.12));
        target["RiceDangerBrush"] = new SolidColorBrush(p.Danger);

        // Fluent control brushes that are not derived from ColorPaletteResources.
        var onAccent = new SolidColorBrush(p.AccentForeground);
        foreach (var key in AccentForegroundKeys)
            target[key] = onAccent;

        var input = p.IsDark ? ColorMath.Blend(p.Background, p.Foreground, 0.04) : ColorMath.Blend(p.Surface, Colors.White, 0.6);
        var inputHover = ColorMath.Blend(input, p.Foreground, 0.05);
        var inputFocused = p.IsDark ? ColorMath.Blend(p.Background, Colors.Black, 0.1) : Colors.White;
        SetBrushes(target, input, "TextControlBackground", "ComboBoxBackground", "ComboBoxBackgroundUnfocused");
        SetBrushes(target, inputHover, "TextControlBackgroundPointerOver", "ComboBoxBackgroundPointerOver");
        SetBrushes(target, inputFocused, "TextControlBackgroundFocused", "ComboBoxBackgroundPressed");

        SetBrushes(target, ColorMath.Blend(p.Surface, p.Foreground, 0.10), "ButtonBackground");
        SetBrushes(target, ColorMath.Blend(p.Surface, p.Foreground, 0.16), "ButtonBackgroundPointerOver");
        SetBrushes(target, ColorMath.Blend(p.Surface, p.Foreground, 0.06), "ButtonBackgroundPressed");
    }

    private static void SetBrushes(ResourceDictionary target, Color color, params string[] keys)
    {
        var brush = new SolidColorBrush(color);
        foreach (var key in keys)
            target[key] = brush;
    }

    // Maps the palette onto Fluent's color slots, so stock controls follow the theme too.
    // The blend factors mirror the gray ramps of Fluent's default light and dark palettes.
    private static void ApplyFluentPalette(ColorPaletteResources target, RicePalette p, bool isDark)
    {
        Color Mix(double light, double dark) => ColorMath.Blend(p.Background, p.Foreground, isDark ? dark : light);

        target.Accent = p.Accent;
        target.RegionColor = p.Background;
        target.ErrorText = p.Danger;

        target.AltHigh = p.Background;
        target.AltMediumHigh = p.Background;
        target.AltMedium = p.Background;
        target.AltMediumLow = p.Background;
        target.AltLow = p.Background;

        target.BaseHigh = p.Foreground;
        target.BaseMediumHigh = Mix(0.64, 0.70);
        target.BaseMedium = Mix(0.46, 0.60);
        target.BaseMediumLow = Mix(0.55, 0.40);
        target.BaseLow = Mix(0.20, 0.20);

        target.ChromeAltLow = Mix(0.64, 0.70);
        target.ChromeBlackHigh = Colors.Black;
        target.ChromeBlackMedium = isDark ? Colors.Black : Mix(0.64, 0);
        target.ChromeBlackMediumLow = isDark ? Colors.Black : Mix(0.46, 0);
        target.ChromeBlackLow = Mix(0.20, 0.70);
        target.ChromeDisabledHigh = Mix(0.20, 0.20);
        target.ChromeDisabledLow = Mix(0.46, 0.60);
        target.ChromeGray = Mix(0.55, 0.50);
        target.ChromeHigh = Mix(0.20, 0.50);
        target.ChromeMedium = ColorMath.Blend(p.Surface, p.Foreground, isDark ? 0.05 : 0.08);
        target.ChromeMediumLow = p.Surface;
        target.ChromeLow = ColorMath.Blend(p.Surface, p.Foreground, isDark ? 0.03 : 0.05);
        target.ChromeWhite = Colors.White;

        target.ListLow = ColorMath.Blend(p.Surface, p.Foreground, 0.08);
        target.ListMedium = Mix(0.20, 0.20);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
