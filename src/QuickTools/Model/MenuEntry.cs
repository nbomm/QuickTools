using System.Text.Json.Serialization;

namespace QuickTools.Model;

internal enum EntryKind
{
    Shortcut,
    Folder,
    Separator,
}

internal sealed class MenuEntry
{
    public EntryKind Kind { get; set; } = EntryKind.Shortcut;

    public string Name { get; set; } = string.Empty;

    /// <summary>An exe, document, folder, URL, .lnk or shell: path; anything ShellExecute accepts.</summary>
    public string? Target { get; set; }

    public string? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    /// <summary>Optional icon override; when empty the icon comes from <see cref="Target"/>.</summary>
    public string? IconPath { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool RunAsAdmin { get; set; }

    /// <summary>Submenu contents; only used when <see cref="Kind"/> is <see cref="EntryKind.Folder"/>.</summary>
    public List<MenuEntry>? Children { get; set; }

    public static MenuEntry Shortcut(string name, string target) =>
        new() { Kind = EntryKind.Shortcut, Name = name, Target = target };

    public static MenuEntry Folder(string name, List<MenuEntry>? children = null) =>
        new() { Kind = EntryKind.Folder, Name = name, Children = children ?? [] };

    public static MenuEntry Separator() => new() { Kind = EntryKind.Separator };

    public MenuEntry Clone()
    {
        var copy = (MenuEntry)MemberwiseClone();
        copy.Children = Children?.Select(child => child.Clone()).ToList();
        return copy;
    }
}
