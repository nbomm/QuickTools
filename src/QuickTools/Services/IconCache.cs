using System.Runtime.InteropServices;
using QuickTools.Model;
using static QuickTools.Tray.NativeMethods;

namespace QuickTools.Services;

/// <summary>Shell icons for menu entries, cached by source path.</summary>
internal sealed class IconCache : IDisposable
{
    private readonly Dictionary<string, Image?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private Image? _folderIcon;
    private Image? _settingsIcon;
    private Image? _exitIcon;
    private Image? _shieldIcon;

    public Image FolderIcon => _folderIcon ??= StockIcon(StockIconId.Folder);

    public Image SettingsIcon => _settingsIcon ??= GlyphIcon('');

    public Image ExitIcon => _exitIcon ??= GlyphIcon('', StockIconId.Error);

    private Image ShieldIcon => _shieldIcon ??= StockIcon(StockIconId.Shield);

    /// <summary>Identifies the image <see cref="Get"/> returns for an entry; equal keys mean identical images.</summary>
    public static string CacheKey(MenuEntry entry)
    {
        string source = (string.IsNullOrWhiteSpace(entry.IconPath) ? entry.Target : entry.IconPath) ?? string.Empty;
        return entry.RunAsAdmin ? "admin|" + source : source;
    }

    /// <summary>
    /// Returns the entry's icon (from <see cref="MenuEntry.IconPath"/> or its target), badged with the
    /// UAC shield when it runs as administrator, or null if there is no icon.
    /// </summary>
    public Image? Get(MenuEntry entry)
    {
        string key = CacheKey(entry);
        if (key.Length == 0)
        {
            return null;
        }

        if (!_cache.TryGetValue(key, out var image))
        {
            string? source = string.IsNullOrWhiteSpace(entry.IconPath) ? entry.Target : entry.IconPath;
            image = string.IsNullOrWhiteSpace(source) ? null : Load(source);
            if (entry.RunAsAdmin)
            {
                image = AddShield(image);
            }

            _cache[key] = image;
        }

        return image;
    }

    // These show the file's own icon, so each needs its own lookup; every other type shares its extension's icon.
    private static readonly HashSet<string> s_ownIconExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".lnk", ".ico", ".url", ".scr", ".appref-ms",
    };

    /// <summary>
    /// The icon for a file in a folder submenu. Most types are looked up by extension, which doesn't touch the disk,
    /// so listing a big folder stays fast.
    /// </summary>
    public Image? GetFileIcon(string path)
    {
        string extension = Path.GetExtension(path);
        bool ownIcon = s_ownIconExtensions.Contains(extension);
        string key = ownIcon ? path : "ext|" + extension;

        if (!_cache.TryGetValue(key, out var image))
        {
            image = ownIcon ? ShellIcon(path, 0) : ShellIcon(extension.Length > 0 ? extension : "file", SHGFI_USEFILEATTRIBUTES);
            _cache[key] = image;
        }

        return image;
    }

    public void Clear()
    {
        foreach (var image in _cache.Values)
        {
            image?.Dispose();
        }

        _cache.Clear();
    }

    public void Dispose()
    {
        Clear();
        _folderIcon?.Dispose();
        _settingsIcon?.Dispose();
        _exitIcon?.Dispose();
        _shieldIcon?.Dispose();
    }

    /// <summary>
    /// Draws the shield over the icon's bottom-right corner, the way Explorer badges elevated programs.
    /// Takes ownership of <paramref name="icon"/>.
    /// </summary>
    private Image AddShield(Image? icon)
    {
        int size = icon?.Width ?? ShieldIcon.Width;
        int badge = size * 3 / 5;
        var result = new Bitmap(size, size);

        using (var g = Graphics.FromImage(result))
        {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            if (icon is null)
            {
                g.DrawImage(ShieldIcon, 0, 0, size, size);
            }
            else
            {
                g.DrawImage(icon, 0, 0, size, size);
                g.DrawImage(ShieldIcon, size - badge, size - badge, badge, badge);
            }
        }

        icon?.Dispose();
        return result;
    }

    private static Image? Load(string source)
    {
        if (PathResolver.Resolve(source) is { } path)
        {
            // For network paths, ask for the generic icon of the file type. Only the extension is passed:
            // given the full path, the shell still contacts the server even with SHGFI_USEFILEATTRIBUTES.
            if (PathResolver.IsNetworkPath(path))
            {
                string extension = Path.GetExtension(path);
                return extension.Length > 0
                    ? ShellIcon(extension, SHGFI_USEFILEATTRIBUTES)
                    : ShellIcon("folder", SHGFI_USEFILEATTRIBUTES, FILE_ATTRIBUTE_DIRECTORY);
            }

            return ShellIcon(path, 0);
        }

        // Web links get the generic Internet Shortcut icon.
        string normalized = PathResolver.Normalize(source);
        if (normalized.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return ShellIcon(".url", SHGFI_USEFILEATTRIBUTES);
        }

        // shell:Downloads, shell:AppsFolder\<app id> and the like have no file path, only a shell item.
        if (normalized.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            return ShellItemIcon(normalized);
        }

        return null;
    }

    private static Image? ShellItemIcon(string parsingName)
    {
        if (SHParseDisplayName(parsingName, IntPtr.Zero, out IntPtr pidl, 0, out _) < 0)
        {
            return null;
        }

        try
        {
            var info = new SHFILEINFOW();
            IntPtr result = SHGetFileInfoW(pidl, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFOW>(),
                SHGFI_PIDL | SHGFI_ICON | SHGFI_LARGEICON);
            return result == IntPtr.Zero ? null : TakeIcon(info.hIcon);
        }
        finally
        {
            Marshal.FreeCoTaskMem(pidl);
        }
    }

    private static Image? ShellIcon(string path, uint extraFlags, uint attributes = FILE_ATTRIBUTE_NORMAL)
    {
        var info = new SHFILEINFOW();

        // Large (32px) icons, scaled down by the menu, stay sharp on high-DPI displays.
        IntPtr result = SHGetFileInfoW(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFOW>(),
            SHGFI_ICON | SHGFI_LARGEICON | extraFlags);

        return result == IntPtr.Zero ? null : TakeIcon(info.hIcon);
    }

    /// <summary>Converts a shell icon to a bitmap (keeping its alpha) and destroys the handle.</summary>
    private static Image? TakeIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = Icon.FromHandle(hIcon);
            return icon.ToBitmap();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }

    /// <summary>
    /// Draws a glyph from the Segoe icon font (Windows 11 or 10), or falls back to a stock icon if neither exists.
    /// The stock Settings icon is blank on some systems, so the gear is drawn rather than requested.
    /// </summary>
    private static Image GlyphIcon(char glyph, StockIconId fallback = StockIconId.Settings)
    {
        const int size = 32;
        string? family = new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" }
            .FirstOrDefault(name => FontFamily.Families.Any(f => f.Name == name));
        if (family is null)
        {
            return StockIcon(fallback);
        }

        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        using var font = new Font(family, size * 0.7f, FontStyle.Regular, GraphicsUnit.Pixel);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        g.DrawString(glyph.ToString(), font, SystemBrushes.ControlText, new RectangleF(0, 0, size, size), format);
        return bitmap;
    }

    private static Image StockIcon(StockIconId id)
    {
        using var icon = SystemIcons.GetStockIcon(id);
        return icon.ToBitmap();
    }
}
