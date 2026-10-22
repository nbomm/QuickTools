using System.Security.Principal;
using QuickTools.Menus;
using QuickTools.Model;
using QuickTools.Services;
using QuickTools.Tray;
using QuickTools.UI;

namespace QuickTools;

/// <summary>Owns the tray icon and wires clicks, menus, launching and the settings window together.</summary>
internal sealed class TrayAppContext : ApplicationContext
{
    private readonly SettingsStore _store = new(SettingsStore.DefaultPath);
    private readonly IconCache _icons = new();
    private readonly Launcher _launcher = new();
    private readonly Icon _appIcon = AppIcon.Create();
    private readonly MenuBuilder _menuBuilder;
    private readonly TrayIcon _tray;

    private AppSettings _settings;
    private ContextMenuStrip _leftMenu = null!;
    private ContextMenuStrip _rightMenu = null!;
    private SettingsForm? _settingsForm;

    public TrayAppContext()
    {
        _settings = _store.Load();
        _menuBuilder = new MenuBuilder(_icons, Launch, ShowSettings, ExitThread);
        RebuildMenus();

        _tray = new TrayIcon(_appIcon, "QuickTools");
        _tray.LeftClick += (_, e) => ShowMenu(_leftMenu, e.Anchor);
        _tray.RightClick += (_, e) => ShowMenu(_rightMenu, e.Anchor);

        if (_store.LastLoadError is { } error)
        {
            _tray.ShowBalloon("QuickTools", error, ToolTipIcon.Warning);
        }
        else if (IsElevated())
        {
            _tray.ShowBalloon("QuickTools is running as administrator",
                "Everything it launches will also run as administrator, without a UAC prompt.", ToolTipIcon.Warning);
        }
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    protected override void ExitThreadCore()
    {
        _settingsForm?.Close();
        _tray.Dispose();
        DisposeMenus();
        _icons.Dispose();
        _appIcon.Dispose();
        base.ExitThreadCore();
    }

    private void ShowMenu(ContextMenuStrip menu, Point anchor)
    {
        // Enter on a focused tray icon can send NIN_KEYSELECT twice; ignore repeats while a menu is up.
        if (_leftMenu.Visible || _rightMenu.Visible)
        {
            return;
        }

        // Exact icon rect first, then the shell's anchor point, then the cursor.
        Rectangle iconBounds = _tray.GetBounds()
            ?? new Rectangle(anchor == Point.Empty ? Cursor.Position : anchor, new Size(1, 1));

        Rectangle workArea = Screen.FromRectangle(iconBounds).WorkingArea;
        Point location = MenuPlacement.Calculate(iconBounds, menu.GetPreferredSize(Size.Empty), workArea);

        _tray.BringToForeground();
        menu.Show(location);
    }

    private void Launch(MenuEntry entry)
    {
        if (!_launcher.TryLaunch(entry, out string? error))
        {
            _tray.ShowBalloon($"Couldn't open {entry.Name}", error ?? "Unknown error.", ToolTipIcon.Error);
        }
    }

    private void ShowSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
            {
                _settingsForm.WindowState = FormWindowState.Normal;
            }

            _settingsForm.Activate();
            return;
        }

        var form = new SettingsForm(_settings, StartupManager.IsEnabled(), _launcher, _appIcon, ApplySettings);
        form.FormClosed += (_, _) =>
        {
            if (_settingsForm == form)
            {
                _settingsForm = null;
            }
        };

        _settingsForm = form;
        form.Show();
        form.Activate();
    }

    /// <param name="startWithWindows">The new startup setting, or null if the user didn't change it.</param>
    private bool ApplySettings(AppSettings settings, bool? startWithWindows)
    {
        try
        {
            _store.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Owned by the Settings window; an ownerless message box would get a taskbar button.
            MessageBox.Show(_settingsForm, $"Couldn't save settings to {_store.FilePath}.\n\n{ex.Message}", "QuickTools",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        // The menus are saved at this point, so apply them even if the startup setting fails.
        _settings = settings;
        RebuildMenus();

        if (startWithWindows is { } enabled)
        {
            try
            {
                StartupManager.SetEnabled(enabled);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                MessageBox.Show(_settingsForm, $"Your menus were saved, but the \"Start with Windows\" setting couldn't be changed.\n\n{ex.Message}",
                    "QuickTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        return true;
    }

    private void RebuildMenus()
    {
        DisposeMenus();
        _icons.Clear();

        _leftMenu = _menuBuilder.Build(_settings.LeftMenu);
        _rightMenu = _settings.MirrorMenus ? _leftMenu : _menuBuilder.Build(_settings.RightMenu);
    }

    private void DisposeMenus()
    {
        if (_rightMenu != _leftMenu)
        {
            _rightMenu?.Dispose();
        }

        _leftMenu?.Dispose();
    }
}
