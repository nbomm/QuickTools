using Microsoft.Win32;

namespace QuickTools.Services;

/// <summary>
/// Resolves a shortcut target to a full file system path the way the shell would (PATH, App Paths).
/// Only needed for icons and default working directories; launching itself is left to ShellExecute.
/// </summary>
internal static class PathResolver
{
    private const string AppPathsKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";

    public static string Normalize(string target) =>
        Environment.ExpandEnvironmentVariables(target.Trim().Trim('"'));

    /// <summary>True for URI-style targets such as https://…, shell:… or ms-settings:.</summary>
    public static bool IsUri(string target)
    {
        int colon = target.IndexOf(':');

        // A one-letter scheme is a drive letter (C:\...), not a URI.
        return colon > 1 && target[..colon].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.');
    }

    /// <summary>
    /// True for UNC paths and mapped network drives. Touching these can block for a long time when the
    /// server is unreachable, and sends the user's credentials to the server, so they are never probed.
    /// </summary>
    public static bool IsNetworkPath(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // \\?\C:\... and \\.\device paths are local; any other \\ prefix is a UNC share.
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return true;
        }

        if (path.Length >= 2 && path[1] == ':' && char.IsAsciiLetter(path[0]))
        {
            try
            {
                // GetDriveType doesn't touch the network.
                return new DriveInfo(path[..1]).DriveType == DriveType.Network;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the full path for a target, or null if it can't be found. Network paths are returned
    /// as-is without checking that they exist (see <see cref="IsNetworkPath"/>).
    /// </summary>
    public static string? Resolve(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        string path = Normalize(target);

        if (IsUri(path))
        {
            return null;
        }

        if (Path.IsPathRooted(path))
        {
            if (IsNetworkPath(path))
            {
                return path;
            }

            return File.Exists(path) || Directory.Exists(path) ? path : null;
        }

        if (path.IndexOfAny(['\\', '/']) < 0 && FromAppPaths(path) is { } registered)
        {
            return registered;
        }

        string[] candidates = Path.HasExtension(path) ? [path] : [path, path + ".exe"];
        string searchPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (string directory in searchPath.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string candidate in candidates)
            {
                try
                {
                    string full = Path.Combine(Environment.ExpandEnvironmentVariables(directory), candidate);
                    if (!IsNetworkPath(full) && File.Exists(full))
                    {
                        return full;
                    }
                }
                catch (ArgumentException)
                {
                    // Malformed PATH entry; skip it.
                }
            }
        }

        return null;
    }

    private static string? FromAppPaths(string name)
    {
        string exe = Path.HasExtension(name) ? name : name + ".exe";

        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            using var key = hive.OpenSubKey(AppPathsKey + exe);
            if (key?.GetValue(null) is string value)
            {
                string path = Normalize(value);
                if (IsNetworkPath(path) || File.Exists(path))
                {
                    return path;
                }
            }
        }

        return null;
    }
}
