using System.Reflection;

namespace QuickTools;

internal static class AppVersion
{
    /// <summary>The running build's version, e.g. "1.0.2 (995c07a)".</summary>
    public static string Current => Format(InformationalVersion);

    /// <summary>The running build's version number without the commit, e.g. "1.0.2".</summary>
    public static string Number => NumberOf(InformationalVersion);

    /// <summary>The copyright line from the project's &lt;Copyright&gt; property, if set.</summary>
    public static string? Copyright =>
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;

    private static string? InformationalVersion =>
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>The version part of an informational version, dropping any "+&lt;commit&gt;" suffix.</summary>
    public static string NumberOf(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        int plus = informationalVersion.IndexOf('+');
        return plus < 0 ? informationalVersion : informationalVersion[..plus];
    }

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
