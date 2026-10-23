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
    private readonly HotkeyListener _hotkeys = new();

    private AppSettings _settings;
    private ContextMenuStrip _leftMenu = null!;
    private ContextMenuStrip _rightMenu = null!;
    private SettingsForm? _settingsForm;
    private PinChannel.Server? _pinServer;
    private readonly Queue<string> _pendingPins = new();
    private bool _pinning;

    /// <param name="pinPath">A file or folder to offer to pin right after startup, if started by "Pin to QuickTools".</param>
    public TrayAppContext(string? pinPath = null)
    {
        _settings = _store.Load();
        _menuBuilder = new MenuBuilder(_icons, Launch, ShowSettings, ExitThread);
        RebuildMenus();

        _tray = new TrayIcon(_appIcon, "QuickTools");
        _tray.LeftClick += (_, e) => ShowMenu(_leftMenu, e.Anchor);
        _tray.RightClick += (_, e) => ShowMenu(_rightMenu, e.Anchor);
        _hotkeys.Pressed += (_, _) => ShowMenuAtCursor(_leftMenu);
        string? hotkeyError = RegisterHotkey();

        // Paths arrive on a pipe thread; hand them to the UI thread, where the menus and dialogs live.
        var ui = SynchronizationContext.Current!;
        _pinServer = new PinChannel.Server(path => ui.Post(_ => Pin(path), null));
        if (pinPath is not null)
        {
            ui.Post(_ => Pin(pinPath), null);
        }
        else if (_store.IsFirstRun)
        {
            // Nothing else tells a new user where to start: the app is only a tray icon, which may be hidden in the overflow.
            ui.Post(_ => ShowSettings(), null);
        }

        if (_store.LastLoadError is { } error)
        {
            _tray.ShowBalloon("QuickTools", error, ToolTipIcon.Warning);
        }
        else if (hotkeyError is not null)
        {
            _tray.ShowBalloon("QuickTools hotkey", hotkeyError, ToolTipIcon.Warning);
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
        _pinServer?.Dispose();
        _hotkeys.Dispose();
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

    /// <summary>Opens the menu at the mouse pointer, for the hotkey.</summary>
    private void ShowMenuAtCursor(ContextMenuStrip menu)
    {
        if (_leftMenu.Visible || _rightMenu.Visible)
        {
            return;
        }

        Point cursor = Cursor.Position;
        Rectangle workArea = Screen.FromPoint(cursor).WorkingArea;
        Point location = MenuPlacement.CalculateAtPoint(cursor, menu.GetPreferredSize(Size.Empty), workArea);

        _tray.BringToForeground();
        menu.Show(location);
    }

    /// <summary>Applies the hotkey setting. Returns a message for the user if it couldn't be registered, or null.</summary>
    private string? RegisterHotkey()
    {
        if (string.IsNullOrWhiteSpace(_settings.Hotkey))
        {
            _hotkeys.TryRegister(null);
            return null;
        }

        if (!Hotkey.TryParse(_settings.Hotkey, out Hotkey hotkey))
        {
            _hotkeys.TryRegister(null);
            return $"\"{_settings.Hotkey}\" isn't a valid hotkey, so it is turned off.";
        }

        return _hotkeys.TryRegister(hotkey)
            ? null
            : $"{hotkey} is already used by Windows or another program, so it can't open the menu.";
    }

    private void Launch(MenuEntry entry)
    {
        if (!_launcher.TryLaunch(entry, out string? error))
        {
            _tray.ShowBalloon($"Couldn't open {entry.Name}", error ?? "Unknown error.", ToolTipIcon.Error);
        }
    }

    /// <summary>Queues a path from "Pin to QuickTools". Several can arrive at once (multi-select), and a
    /// dialog pumps messages while open, so they are handled one at a time.</summary>
    private void Pin(string path)
    {
        _pendingPins.Enqueue(path);
        if (_pinning)
        {
            return;
        }

        _pinning = true;
        try
        {
            while (_pendingPins.TryDequeue(out string? next))
            {
                PinOne(next);
            }
        }
        finally
        {
            _pinning = false;
        }
    }

    private void PinOne(string path)
    {
        // The Settings window holds its own copy of the menus and would overwrite a pin when OK is clicked.
        if (_settingsForm is { IsDisposed: false })
        {
            _tray.ShowBalloon("Couldn't pin", "Close the Settings window first, then pin again.", ToolTipIcon.Warning);
            return;
        }

        bool isDirectory = Directory.Exists(path);
        if (!isDirectory && !File.Exists(path))
        {
            _tray.ShowBalloon("Couldn't pin", $"{path} doesn't exist.", ToolTipIcon.Warning);
            return;
        }

        // Names are menu text, where & marks a keyboard key.
        string name = (isDirectory ? Path.GetFileName(path.TrimEnd('\\')) : Path.GetFileNameWithoutExtension(path)).Replace("&", "&&");
        var entry = MenuEntry.Shortcut(name.Length > 0 ? name : path, path);

        using var editor = new EntryEditorForm(entry, isNew: true, _icons) { TopMost = true };
        _tray.BringToForeground();
        if (editor.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        var settings = _settings;
        settings.LeftMenu.Add(entry);
        if (!settings.MirrorMenus)
        {
            settings.RightMenu.Add(entry.Clone());
        }

        try
        {
            _store.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _tray.ShowBalloon("Couldn't save settings", ex.Message, ToolTipIcon.Error);
        }

        RebuildMenus();
        _tray.ShowBalloon("Pinned to QuickTools", entry.Name.Replace("&&", "&"), ToolTipIcon.Info);
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

        var form = new SettingsForm(_settings, StartupManager.IsEnabled(), ShellIntegration.IsEnabled(), _launcher, _appIcon, ApplySettings);
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
    /// <param name="pinMenu">The new "Pin to QuickTools" Explorer item setting, or null if the user didn't change it.</param>
    private bool ApplySettings(AppSettings settings, bool? startWithWindows, bool? pinMenu)
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

        if (RegisterHotkey() is { } hotkeyError)
        {
            MessageBox.Show(_settingsForm, $"Your settings were saved, but the hotkey doesn't work: {hotkeyError}",
                "QuickTools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

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

        if (pinMenu is { } pinEnabled)
        {
            try
            {
                ShellIntegration.SetEnabled(pinEnabled);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                MessageBox.Show(_settingsForm, $"Your menus were saved, but the \"{ShellIntegration.MenuText}\" Explorer item couldn't be changed.\n\n{ex.Message}",
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
