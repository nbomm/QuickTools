using Microsoft.Win32;

namespace QuickTools.Services;

/// <summary>
/// "Start with Windows" via the per-user Run key. The registry is the source of truth, so it is
/// never duplicated in the settings file where the two could disagree.
/// </summary>
internal static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "QuickTools";

    private static string Command => $"\"{ExecutablePath}\"";

    // Started as "dotnet QuickTools.dll", ProcessPath is dotnet.exe; register the app's own launcher instead.
    private static string? ExecutablePath
    {
        get
        {
            string appHost = Path.Combine(AppContext.BaseDirectory, "QuickTools.exe");
            return File.Exists(appHost) ? appHost : Environment.ProcessPath;
        }
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) is string value &&
            string.Equals(value, Command, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
        {
            key.SetValue(ValueName, Command);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }
}
