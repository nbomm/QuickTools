using Microsoft.Win32;

namespace QuickTools.Services;

/// <summary>
/// The "Pin to QuickTools" item in File Explorer's right-click menu, for files and folders. It is a
/// static shell verb under the per-user classes key, so it needs no admin rights and no COM shell extension.
/// On Windows 11 it appears under "Show more options". The registry is the source of truth, as for
/// <see cref="StartupManager"/>.
/// </summary>
internal static class ShellIntegration
{
    public const string MenuText = "Pin to QuickTools";

    private const string VerbName = "QuickTools.Pin";

    // "*" is every file; Directory is every folder.
    private static readonly string[] s_verbKeys =
    [
        @"Software\Classes\*\shell\" + VerbName,
        @"Software\Classes\Directory\shell\" + VerbName,
    ];

    private static string? ExecutablePath
    {
        get
        {
            string appHost = Path.Combine(AppContext.BaseDirectory, "QuickTools.exe");
            return File.Exists(appHost) ? appHost : Environment.ProcessPath;
        }
    }

    /// <summary>The verb's command line; Explorer replaces %1 with the clicked file or folder.</summary>
    public static string CommandFor(string exePath) => $"\"{exePath}\" --pin \"%1\"";

    public static bool IsEnabled()
    {
        if (ExecutablePath is not { } exe)
        {
            return false;
        }

        string expected = CommandFor(exe);
        return s_verbKeys.All(keyPath =>
        {
            using var key = Registry.CurrentUser.OpenSubKey(keyPath + @"\command");
            return key?.GetValue(null) is string value && string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        });
    }

    public static void SetEnabled(bool enabled)
    {
        foreach (string keyPath in s_verbKeys)
        {
            if (!enabled)
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                continue;
            }

            string exe = ExecutablePath ?? throw new InvalidOperationException("Can't find the QuickTools executable.");
            using var key = Registry.CurrentUser.CreateSubKey(keyPath);
            key.SetValue(null, MenuText);
            key.SetValue("Icon", $"\"{exe}\",0");
            using var command = key.CreateSubKey("command");
            command.SetValue(null, CommandFor(exe));
        }
    }
}
