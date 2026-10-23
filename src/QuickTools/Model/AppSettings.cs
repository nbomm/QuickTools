namespace QuickTools.Model;

internal sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// When true, right-click shows the left-click menu. <see cref="RightMenu"/> is kept rather than
    /// overwritten, so turning mirroring off brings the separate right-click menu back.
    /// </summary>
    public bool MirrorMenus { get; set; } = true;

    public const string DefaultHotkey = "Ctrl+Alt+Q";

    /// <summary>
    /// Global hotkey, as text such as "Ctrl+Alt+Q" (see <see cref="Hotkey"/>), that opens the left-click menu
    /// at the mouse pointer. Empty means no hotkey. A settings file that doesn't mention it gets
    /// <see cref="DefaultHotkey"/>, so existing users get the feature too; an empty value turns it off.
    /// </summary>
    public string Hotkey { get; set; } = DefaultHotkey;

    public List<MenuEntry> LeftMenu { get; set; } = [];

    public List<MenuEntry> RightMenu { get; set; } = [];

    public static AppSettings CreateDefault() => new()
    {
        MirrorMenus = true,
        LeftMenu = CreateDefaultEntries(),
        RightMenu = CreateDefaultEntries(),
    };

    // Bare names are resolved by the shell (PATH / App Paths), so they work on any machine.
    private static List<MenuEntry> CreateDefaultEntries() =>
    [
        MenuEntry.Shortcut("Notepad", "notepad.exe"),
        MenuEntry.Shortcut("File Explorer", "explorer.exe"),
        MenuEntry.Shortcut("Microsoft Edge", "msedge.exe"),
    ];
}
