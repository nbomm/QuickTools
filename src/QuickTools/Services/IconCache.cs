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
    private Image? _shieldIcon;

    public Image FolderIcon => _folderIcon ??= StockIcon(StockIconId.Folder);

    public Image SettingsIcon => _settingsIcon ??= StockIcon(StockIconId.Settings);

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

        return null;
    }

    private static Image? ShellIcon(string path, uint extraFlags, uint attributes = FILE_ATTRIBUTE_NORMAL)
    {
        var info = new SHFILEINFOW();

        // Large (32px) icons, scaled down by the menu, stay sharp on high-DPI displays.
        IntPtr result = SHGetFileInfoW(path, attributes, ref info, (uint)Marshal.SizeOf<SHFILEINFOW>(),
            SHGFI_ICON | SHGFI_LARGEICON | extraFlags);

        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            using var icon = Icon.FromHandle(info.hIcon);
            return icon.ToBitmap();
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }

    private static Image StockIcon(StockIconId id)
    {
        using var icon = SystemIcons.GetStockIcon(id);
        return icon.ToBitmap();
    }
}
