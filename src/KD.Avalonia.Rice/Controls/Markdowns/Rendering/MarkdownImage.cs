using System.Collections.Concurrent;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Material.Icons;
using Material.Icons.Avalonia;

namespace KD.Avalonia.Rice.Controls.Markdowns;

/// <summary>
/// Loads the images of a document: paths relative to the base URI, <c>file:</c>, <c>avares:</c>, <c>data:</c> and
/// <c>http(s):</c> URLs. Bitmaps (PNG, JPEG, BMP, GIF, WebP) are decoded in the background and cached; SVG is not
/// supported.
/// </summary>
internal static class MarkdownImageLoader
{
    private static readonly Lazy<HttpClient> Http = new(() =>
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("KD.Avalonia.Rice-Markdown/1.0");
        return client;
    });

    private static readonly ConcurrentDictionary<string, Task<Bitmap?>> Cache = new();

    public static Uri? Resolve(string url, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && (absolute.Scheme.Length > 1 || !OperatingSystem.IsWindows()))
            return absolute;
        if (baseUri is not null && Uri.TryCreate(baseUri, url, out var relative))
            return relative;
        return Uri.TryCreate(Path.GetFullPath(url), UriKind.Absolute, out var file) ? file : null;
    }

    public static Task<Bitmap?> LoadAsync(string url, Uri? baseUri)
    {
        var uri = url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ? null : Resolve(url, baseUri);
        var key = uri?.AbsoluteUri ?? url;
        var task = Cache.GetOrAdd(key, _ => Task.Run(() => LoadCoreAsync(url, uri)));
        // A failed download is tried again next time.
        task.ContinueWith(t => { if (t.Result is null && uri?.Scheme is "http" or "https") Cache.TryRemove(key, out _); },
            TaskContinuationOptions.OnlyOnRanToCompletion);
        return task;
    }

    private static async Task<Bitmap?> LoadCoreAsync(string url, Uri? uri)
    {
        try
        {
            byte[] bytes;
            if (uri is null)
            {
                var comma = url.IndexOf(',');
                if (comma < 0 || !url[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                    return null;
                bytes = Convert.FromBase64String(url[(comma + 1)..]);
            }
            else if (uri.Scheme is "http" or "https")
                bytes = await Http.Value.GetByteArrayAsync(uri);
            else if (uri.Scheme == "avares")
            {
                await using var asset = AssetLoader.Open(uri);
                using var memory = new MemoryStream();
                await asset.CopyToAsync(memory);
                bytes = memory.ToArray();
            }
            else if (uri.IsFile)
                bytes = await File.ReadAllBytesAsync(uri.LocalPath);
            else
                return null;

            if (LooksLikeText(bytes))
                return null;
            using var stream = new MemoryStream(bytes);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>SVG and HTML error pages start with markup; those are not decoded.</summary>
    private static bool LooksLikeText(byte[] bytes)
    {
        var i = 0;
        if (bytes is [0xEF, 0xBB, 0xBF, ..])
            i = 3;
        while (i < bytes.Length && bytes[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
            i++;
        return i < bytes.Length && bytes[i] == (byte)'<';
    }
}

/// <summary>An image of the document; shows the alt text while loading and when the image cannot be loaded.</summary>
internal sealed class MarkdownImage : Decorator
{
    private readonly MarkdownRenderContext _context;
    private readonly double? _width;
    private readonly double? _height;
    private readonly Image _image = new() { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly };
    private Bitmap? _bitmap;

    public MarkdownImage(MarkdownRenderContext context, string url, string alt, string? title, double? width, double? height)
    {
        _context = context;
        _width = width;
        _height = height;
        ToolTip.SetTip(this, string.IsNullOrEmpty(title) ? null : title);
        Child = Placeholder(alt, failed: false);
        context.Images.Add(this);
        _ = LoadAsync(url, alt);
    }

    private Control Placeholder(string alt, bool failed)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        var icon = new MaterialIcon { Kind = failed ? MaterialIconKind.ImageBrokenVariant : MaterialIconKind.ImageOutline, Width = 14, Height = 14 };
        icon.Bind(MaterialIcon.ForegroundProperty, icon.GetResourceObservable("RiceForegroundMutedBrush"));
        panel.Children.Add(icon);
        if (alt.Length > 0)
        {
            var text = new TextBlock { Text = string.Format(RiceStrings.MarkdownImageFailed, alt), FontSize = _context.FontSize * 0.9 };
            text.Bind(TextBlock.ForegroundProperty, text.GetResourceObservable("RiceForegroundMutedBrush"));
            panel.Children.Add(text);
        }
        return panel;
    }

    private async Task LoadAsync(string url, string alt)
    {
        var bitmap = await MarkdownImageLoader.LoadAsync(url, _context.BaseUri);
        if (bitmap is null)
        {
            Child = Placeholder(alt, failed: true);
            return;
        }
        _bitmap = bitmap;
        _image.Source = bitmap;
        UpdateSize();
        Child = _image;
    }

    /// <summary>Images in text are measured without a width limit, so they are limited to the document width here.</summary>
    public void UpdateSize()
    {
        if (_bitmap is null)
            return;
        var natural = _bitmap.Size;
        var width = _width ?? (_height is { } h && natural.Height > 0 ? natural.Width * h / natural.Height : natural.Width);
        var max = _context.AvailableWidth > 0 ? _context.AvailableWidth : double.PositiveInfinity;
        width = Math.Min(width, max);
        var height = natural.Width > 0 ? width * natural.Height / natural.Width : natural.Height;
        if (_height is { } fixedHeight && _width is not null)
            height = Math.Min(fixedHeight, height);
        _image.Width = width;
        _image.Height = height;
    }
}
