using QuickTools.Tray;
using static QuickTools.Tray.NativeMethods;

namespace QuickTools;

/// <summary>
/// Command-line output (help, version and errors); nothing is ever written to stdout. QuickTools is a GUI
/// (WinExe) app, so it has no console of its own:
/// <list type="bullet">
/// <item>stderr redirected to a file or pipe is written directly;</item>
/// <item>otherwise the text goes to the console of the terminal that started it;</item>
/// <item>with neither (started from Explorer or a shortcut) it is shown in a message box.</item>
/// </list>
/// </summary>
internal static class CommandLineOutput
{
    private const uint AttachParentProcess = unchecked((uint)-1);

    /// <param name="isError">Only chooses the message box icon.</param>
    public static void Write(string text, bool isError, IWin32Window messageBoxOwner)
    {
        // Not Console.IsErrorRedirected: a GUI app started from a terminal has no stderr handle at all,
        // and .NET reports a missing handle as "redirected", so the text would go nowhere.
        if (IsFileOrPipe(GetStdHandle(STD_ERROR_HANDLE)))
        {
            Console.Error.WriteLine(text);
            Console.Error.Flush();
            return;
        }

        if (AttachConsole(AttachParentProcess) && WriteToConsole(text))
        {
            return;
        }

        MessageBox.Show(messageBoxOwner, text, "QuickTools", MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
    }

    private static bool IsFileOrPipe(IntPtr handle) =>
        handle != IntPtr.Zero && handle != new IntPtr(-1) && GetFileType(handle) is FILE_TYPE_DISK or FILE_TYPE_PIPE;

    /// <summary>
    /// Writes to the attached console itself ("CONOUT$"). Attaching doesn't connect a GUI app's standard
    /// streams to the console, so writing through Console.Error would still go nowhere.
    /// </summary>
    private static bool WriteToConsole(string text)
    {
        using var console = CreateFileW("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
        if (console.IsInvalid)
        {
            return false;
        }

        // A terminal doesn't wait for a GUI app, so its prompt is already printed; start on a new line.
        string output = Environment.NewLine + text.ReplaceLineEndings(Environment.NewLine) + Environment.NewLine;
        return WriteConsoleW(console, output, (uint)output.Length, out _, IntPtr.Zero);
    }
}
