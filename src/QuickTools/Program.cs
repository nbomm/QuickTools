using QuickTools.Services;

namespace QuickTools;

internal static class Program
{
    private const string SingleInstanceMutexName = @"Local\QuickTools-5E0C9B7A-3F1D-4C2E-9A61-7B8D2E4F1C03";

    // A message box with no owner gets its own taskbar button; owning it by a hidden window prevents that.
    private static readonly Lazy<IWin32Window> s_hiddenOwner = new(() =>
    {
        var window = new NativeWindow();
        window.CreateHandle(new CreateParams());
        return window;
    });

    [STAThread]
    private static int Main(string[] args)
    {
        // Before anything shows a window (the message box fallback for command-line output).
        ApplicationConfiguration.Initialize();

        // Handled before the single-instance check, so --help and --version also work while QuickTools runs.
        var command = CommandLine.Parse(args);
        switch (command.Action)
        {
            case CommandLineAction.ShowHelp:
                CommandLineOutput.Write(HelpText(), isError: false, s_hiddenOwner.Value);
                return CommandLine.ExitSuccess;

            case CommandLineAction.ShowVersion:
                CommandLineOutput.Write(CommandLine.VersionText(AppVersion.Current), isError: false, s_hiddenOwner.Value);
                return CommandLine.ExitSuccess;

            case CommandLineAction.Error:
                CommandLineOutput.Write($"{command.Error}\n\n{HelpText()}", isError: true, s_hiddenOwner.Value);
                return CommandLine.ExitUsageError;
        }

        // A second instance would add a second tray icon, so it just exits.
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            if (command.Action == CommandLineAction.Pin && !PinChannel.TrySend(command.Path!))
            {
                MessageBox.Show(s_hiddenOwner.Value, "QuickTools didn't respond, so the item wasn't pinned.", "QuickTools",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return CommandLine.ExitUsageError;
            }

            return CommandLine.ExitSuccess;
        }

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(Form.ActiveForm ?? s_hiddenOwner.Value, e.Exception.Message, "QuickTools error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        // Started by "Pin to QuickTools" with nothing running: become the tray instance, then offer the pin.
        Application.Run(new TrayAppContext(command.Action == CommandLineAction.Pin ? command.Path : null));
        return CommandLine.ExitSuccess;
    }

    private static string HelpText() => CommandLine.HelpText(AppVersion.Current, SettingsStore.DefaultPath);
}
