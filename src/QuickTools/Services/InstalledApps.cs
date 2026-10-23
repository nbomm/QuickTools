using System.Runtime.InteropServices;

namespace QuickTools.Services;

/// <summary>An app from Start's All apps list: a desktop shortcut or a Store app.</summary>
internal sealed record InstalledApp(string Name, string ParsingName)
{
    /// <summary>Launches the app through the shell, the same way Start does.</summary>
    public string Target => @"shell:AppsFolder\" + ParsingName;

    /// <summary>The name as menu text: a literal &amp; is doubled so it isn't taken as the keyboard key.</summary>
    public string MenuName => Name.Replace("&", "&&");
}

internal static class InstalledApps
{
    /// <summary>
    /// Lists the apps in shell:AppsFolder, sorted by name. Must be called on an STA thread,
    /// such as the UI thread.
    /// </summary>
    public static IReadOnlyList<InstalledApp> Enumerate()
    {
        var shellType = Type.GetTypeFromProgID("Shell.Application")
            ?? throw new COMException("The Windows shell isn't available.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic folder = shell.NameSpace("shell:AppsFolder")
                ?? throw new COMException("The All apps folder isn't available.");

            var apps = new List<InstalledApp>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (dynamic item in folder.Items())
            {
                string? name = item.Name;
                string? path = item.Path;
                Marshal.FinalReleaseComObject(item);

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path) && seen.Add(path))
                {
                    apps.Add(new InstalledApp(name, path));
                }
            }

            Marshal.FinalReleaseComObject(folder);
            apps.Sort((a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Name, b.Name));
            return apps;
        }
        finally
        {
            Marshal.FinalReleaseComObject(shell);
        }
    }

    /// <summary>Apps whose name contains every word of the query, in any order and ignoring case.</summary>
    public static IEnumerable<InstalledApp> Filter(IEnumerable<InstalledApp> apps, string query)
    {
        string[] words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return apps.Where(app => words.All(word => app.Name.Contains(word, StringComparison.CurrentCultureIgnoreCase)));
    }
}
