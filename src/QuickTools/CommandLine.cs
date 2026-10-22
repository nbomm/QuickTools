using QuickTools.Services;

namespace QuickTools;

internal enum CommandLineAction
{
    Run,
    ShowHelp,
    ShowVersion,
    Error,
}

internal readonly record struct ParsedCommandLine(CommandLineAction Action, string? Error = null);

/// <summary>Parses the command line and produces the help and version text. No I/O, so it is unit tested.</summary>
internal static class CommandLine
{
    public const int ExitSuccess = 0;
    public const int ExitUsageError = 2;

    /// <summary>
    /// Options are case-insensitive, as is usual on Windows. Exactly one option is accepted; with none,
    /// QuickTools starts normally.
    /// </summary>
    public static ParsedCommandLine Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new(CommandLineAction.Run);
        }

        if (args.Count > 1)
        {
            return new(CommandLineAction.Error, $"Too many arguments: {string.Join(' ', args)}");
        }

        return args[0].ToLowerInvariant() switch
        {
            "-h" or "--help" or "-?" or "/?" => new(CommandLineAction.ShowHelp),
            "-v" or "--version" => new(CommandLineAction.ShowVersion),
            _ => new(CommandLineAction.Error, $"Unknown option: {args[0]}"),
        };
    }

    public static string VersionText(string version) => $"QuickTools {version}";

    public static string HelpText(string version, string settingsPath) => $"""
        {VersionText(version)}
        A Windows tray launcher with customizable left-click and right-click menus.

        Usage: QuickTools.exe [option]

        Options:
          -h, --help, /?   Show this help and exit.
          -v, --version    Show the version and exit.

        With no option, QuickTools starts in the notification area (system tray).
        Only one instance runs per user session; starting it again does nothing.

        Settings file: {settingsPath}
        """;
}
