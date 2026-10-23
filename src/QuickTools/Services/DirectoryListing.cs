using System.Globalization;

namespace QuickTools.Services;

/// <summary>One file or subfolder in a directory submenu.</summary>
internal sealed record DirectoryItem(string Name, string FullPath, bool IsDirectory)
{
    /// <summary>Like Explorer, never shows the extension of shortcuts.</summary>
    public string DisplayName => !IsDirectory && Path.GetExtension(Name) is ".lnk" or ".url"
        ? Path.GetFileNameWithoutExtension(Name)
        : Name;
}

/// <param name="Items">At most the requested number of items: folders first, then files, each in Explorer's order.</param>
/// <param name="MoreCount">How many further items were left out because of the limit.</param>
/// <param name="Error">Why the directory couldn't be read, or null.</param>
internal sealed record DirectoryContents(IReadOnlyList<DirectoryItem> Items, int MoreCount, string? Error);

/// <summary>Lists a directory for a submenu. Kept free of UI so ordering and failure cases can be unit tested.</summary>
internal static class DirectoryListing
{
    // Digits compare as numbers, so "file2" comes before "file10", as in Explorer.
    private static readonly StringComparer s_nameOrder =
        StringComparer.Create(CultureInfo.CurrentCulture, CompareOptions.IgnoreCase | CompareOptions.NumericOrdering);

    private static readonly EnumerationOptions s_options = new()
    {
        IgnoreInaccessible = true,

        // What Explorer hides by default.
        AttributesToSkip = FileAttributes.Hidden | FileAttributes.System,
    };

    public static DirectoryContents List(string path, int maxItems)
    {
        List<DirectoryItem> all;
        try
        {
            all = new DirectoryInfo(path).EnumerateFileSystemInfos("*", s_options)
                .Select(info => new DirectoryItem(info.Name, info.FullName, info.Attributes.HasFlag(FileAttributes.Directory)))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new DirectoryContents([], 0, ex.Message);
        }

        var items = all
            .OrderByDescending(item => item.IsDirectory)
            .ThenBy(item => item.Name, s_nameOrder)
            .Take(maxItems)
            .ToList();

        return new DirectoryContents(items, all.Count - items.Count, null);
    }
}
