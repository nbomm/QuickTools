using System.ComponentModel;
using System.Diagnostics;
using QuickTools.Model;

namespace QuickTools.Services;

internal sealed class Launcher
{
    private const int ErrorCancelled = 1223;

    /// <summary>
    /// Launches through ShellExecute, which handles exes, documents, folders, URLs, .lnk and shell: paths alike.
    /// </summary>
    public bool TryLaunch(MenuEntry entry, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(entry.Target))
        {
            error = "This shortcut has no target.";
            return false;
        }

        string target = PathResolver.Normalize(entry.Target);
        var startInfo = new ProcessStartInfo(target)
        {
            UseShellExecute = true,
            Arguments = string.IsNullOrWhiteSpace(entry.Arguments) ? string.Empty : Environment.ExpandEnvironmentVariables(entry.Arguments),
        };

        if (GetWorkingDirectory(entry, target) is { } workingDirectory)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        if (entry.RunAsAdmin)
        {
            startInfo.Verb = "runas";
        }

        try
        {
            using var process = Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ErrorCancelled)
        {
            // The user declined the UAC prompt; that's a choice, not an error.
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? GetWorkingDirectory(MenuEntry entry, string target)
    {
        if (!string.IsNullOrWhiteSpace(entry.WorkingDirectory))
        {
            return PathResolver.Normalize(entry.WorkingDirectory);
        }

        // Like Explorer, default to the program's own folder. A full path needs no lookup, which also
        // avoids probing network paths before ShellExecute does.
        if (Path.IsPathRooted(target))
        {
            return Path.GetDirectoryName(target);
        }

        // Bare names resolve only to files (via App Paths or PATH).
        return PathResolver.Resolve(target) is { } resolved ? Path.GetDirectoryName(resolved) : null;
    }
}
