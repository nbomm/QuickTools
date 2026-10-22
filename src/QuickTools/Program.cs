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
    private static void Main()
    {
        // A second instance would add a second tray icon, so it just exits.
        using var mutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out bool createdNew);
        if (!createdNew)
        {
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
            MessageBox.Show(Form.ActiveForm ?? s_hiddenOwner.Value, e.Exception.Message, "QuickTools error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);

        Application.Run(new TrayAppContext());
    }
}
