using QuickTools.Tray;

namespace QuickTools;

/// <summary>
/// All command-line output (help, version and errors) goes to stderr. QuickTools is a GUI (WinExe) app,
/// so it has no console of its own: a redirected stderr (pipe or file) is written directly, otherwise
/// the console of the terminal that started it, and with neither (started from Explorer or a shortcut)
/// the text is shown in a message box.
/// </summary>
internal static class CommandLineOutput
{
    private const uint AttachParentProcess = unchecked((uint)-1);

    /// <param name="isError">Only chooses the message box icon; the stream is always stderr.</param>
    public static void Write(string text, bool isError, IWin32Window messageBoxOwner)
    {
        bool redirected = Console.IsErrorRedirected;

        if (redirected || NativeMethods.AttachConsole(AttachParentProcess))
        {
            // A terminal doesn't wait for a GUI app, so its prompt is already printed; start on a new line.
            if (!redirected)
            {
                Console.Error.WriteLine();
            }

            Console.Error.WriteLine(text);
            Console.Error.Flush();
            return;
        }

        MessageBox.Show(messageBoxOwner, text, "QuickTools", MessageBoxButtons.OK,
            isError ? MessageBoxIcon.Error : MessageBoxIcon.Information);
    }
}
