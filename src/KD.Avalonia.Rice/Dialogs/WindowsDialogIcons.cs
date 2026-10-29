using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace KD.Avalonia.Rice.Dialogs;

/// <summary>Loads the shell's stock question / information / warning / error icons.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WindowsDialogIcons
{
    private const uint SIID_HELP = 23;
    private const uint SIID_WARNING = 78;
    private const uint SIID_INFO = 79;
    private const uint SIID_ERROR = 80;
    private const uint SHGSI_ICONLOCATION = 0;
    private const uint DIB_RGB_COLORS = 0;

    public static Bitmap? Load(DialogIcon icon, int pixelSize)
    {
        uint? id = icon switch
        {
            DialogIcon.Question => SIID_HELP,
            DialogIcon.Information => SIID_INFO,
            DialogIcon.Warning => SIID_WARNING,
            DialogIcon.Error => SIID_ERROR,
            _ => null,
        };
        if (id is null)
            return null;
        try
        {
            var info = new SHSTOCKICONINFO { cbSize = (uint)sizeof(SHSTOCKICONINFO) };
            if (SHGetStockIconInfo(id.Value, SHGSI_ICONLOCATION, ref info) != 0)
                return null;
            var path = new string(info.szPath);
            // Low word: size of the large icon, high word: size of the small one (not used).
            if (SHDefExtractIconW(path, info.iIcon, 0, out var large, out var small, (uint)pixelSize | (16u << 16)) != 0)
                return null;
            if (small != 0)
                DestroyIcon(small);
            try
            {
                return ToBitmap(large);
            }
            finally
            {
                DestroyIcon(large);
            }
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Bitmap? ToBitmap(nint icon)
    {
        if (!GetIconInfo(icon, out var iconInfo))
            return null;
        try
        {
            BITMAP bitmap;
            if (iconInfo.hbmColor == 0 || GetObjectW(iconInfo.hbmColor, sizeof(BITMAP), &bitmap) == 0)
                return null;
            int width = bitmap.bmWidth, height = bitmap.bmHeight;
            var header = new BITMAPINFO
            {
                biSize = (uint)sizeof(BITMAPINFOHEADER),
                biWidth = width,
                biHeight = -height, // top-down rows
                biPlanes = 1,
                biBitCount = 32,
            };
            var pixels = new byte[width * height * 4];
            var dc = GetDC(0);
            try
            {
                fixed (byte* p = pixels)
                {
                    if (GetDIBits(dc, iconInfo.hbmColor, 0, (uint)height, p, &header, DIB_RGB_COLORS) == 0)
                        return null;
                }
            }
            finally
            {
                ReleaseDC(0, dc);
            }

            // Icons without an alpha channel would come out fully transparent.
            var hasAlpha = false;
            for (var i = 3; i < pixels.Length && !hasAlpha; i += 4)
                hasAlpha = pixels[i] != 0;
            if (!hasAlpha)
            {
                for (var i = 3; i < pixels.Length; i += 4)
                    pixels[i] = 255;
            }

            fixed (byte* p = pixels)
            {
                return new Bitmap(PixelFormat.Bgra8888, AlphaFormat.Unpremul, (nint)p,
                    new PixelSize(width, height), new Vector(96, 96), width * 4);
            }
        }
        finally
        {
            if (iconInfo.hbmColor != 0)
                DeleteObject(iconInfo.hbmColor);
            if (iconInfo.hbmMask != 0)
                DeleteObject(iconInfo.hbmMask);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SHSTOCKICONINFO
    {
        public uint cbSize;
        public nint hIcon;
        public int iSysImageIndex;
        public int iIcon;
        public fixed char szPath[260];
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public int fIcon;
        public uint xHotspot;
        public uint yHotspot;
        public nint hbmMask;
        public nint hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public nint bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    // BITMAPINFOHEADER followed by a one entry color table, as BITMAPINFO is declared.
    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
        public uint bmiColors;
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetStockIconInfo(uint siid, uint uFlags, ref SHSTOCKICONINFO psii);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHDefExtractIconW(string pszIconFile, int iIndex, uint uFlags,
        out nint phiconLarge, out nint phiconSmall, uint nIconSize);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetIconInfo(nint hIcon, out ICONINFO piconinfo);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint hIcon);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hWnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hWnd, nint hDC);

    [LibraryImport("gdi32.dll")]
    private static partial int GetObjectW(nint h, int c, void* pv);

    [LibraryImport("gdi32.dll")]
    private static partial int GetDIBits(nint hdc, nint hbm, uint start, uint cLines, void* lpvBits, BITMAPINFO* lpbmi, uint usage);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint ho);
}
