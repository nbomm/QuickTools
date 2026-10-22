using System.Reflection;

namespace QuickTools;

internal static class AppVersion
{
    /// <summary>The running build's version, e.g. "1.0.2 (995c07a)".</summary>
    public static string Current => Format(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// The SDK stamps the informational version as "&lt;version&gt;+&lt;full commit hash&gt;"; this shortens the
    /// hash to the 7 characters git usually shows.
    /// </summary>
    public static string Format(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        int plus = informationalVersion.IndexOf('+');
        if (plus < 0 || plus == informationalVersion.Length - 1)
        {
            return informationalVersion.TrimEnd('+');
        }

        string commit = informationalVersion[(plus + 1)..];
        return $"{informationalVersion[..plus]} ({commit[..Math.Min(7, commit.Length)]})";
    }
}
