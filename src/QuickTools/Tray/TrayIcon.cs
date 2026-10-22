using System.Runtime.InteropServices;
using static QuickTools.Tray.NativeMethods;

namespace QuickTools.Tray;

internal sealed class TrayIconClickEventArgs(Point anchor) : EventArgs
{
    /// <summary>The icon's anchor point in screen coordinates, as reported by the shell.</summary>
    public Point Anchor { get; } = anchor;
}

/// <summary>
/// Thin wrapper over Shell_NotifyIcon. Used instead of WinForms NotifyIcon because that class hides
/// the window handle and ID needed by Shell_NotifyIconGetRect, and only supports a right-click menu.
/// </summary>
internal sealed class TrayIcon : IDisposable
{
    private const uint IconId = 1;
    private const int CallbackMessage = WM_APP + 1;

    // Broadcast by Explorer when the taskbar is (re)created; the icon must be re-added or it vanishes.
    private static readonly int s_taskbarCreatedMessage = (int)RegisterWindowMessageW("TaskbarCreated");

    private readonly MessageWindow _window;
    private readonly Icon _icon;
    private readonly string _toolTip;
    private bool _disposed;

    public event EventHandler<TrayIconClickEventArgs>? LeftClick;
    public event EventHandler<TrayIconClickEventArgs>? RightClick;

    public TrayIcon(Icon icon, string toolTip)
    {
        _icon = icon;
        _toolTip = toolTip;
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());

        // When elevated, UIPI would block Explorer's TaskbarCreated broadcast and the icon would be
        // lost for good after an Explorer restart.
        if (s_taskbarCreatedMessage != 0)
        {
            ChangeWindowMessageFilterEx(_window.Handle, (uint)s_taskbarCreatedMessage, MSGFLT_ALLOW, IntPtr.Zero);
        }

        AddToTray();
    }

    /// <summary>Returns the icon's bounds in screen coordinates, or null if the shell can't provide them.</summary>
    public Rectangle? GetBounds()
    {
        var identifier = new NOTIFYICONIDENTIFIER
        {
            cbSize = Marshal.SizeOf<NOTIFYICONIDENTIFIER>(),
            hWnd = _window.Handle,
            uID = IconId,
        };

        if (Shell_NotifyIconGetRect(ref identifier, out RECT rect) != 0)
        {
            return null;
        }

        var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return bounds.Width > 0 && bounds.Height > 0 ? bounds : null;
    }

    /// <summary>
    /// A tray menu only closes on an outside click if the app owns the foreground window,
    /// so this must be called before showing a menu.
    /// </summary>
    public void BringToForeground() => SetForegroundWindow(_window.Handle);

    public void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        var data = CreateData(NIF_INFO);
        data.szInfoTitle = Truncate(title, 63);
        data.szInfo = Truncate(text, 255);
        data.dwInfoFlags = icon switch
        {
            ToolTipIcon.Error => NIIF_ERROR,
            ToolTipIcon.Warning => NIIF_WARNING,
            ToolTipIcon.Info => NIIF_INFO,
            _ => NIIF_NONE,
        };
        Shell_NotifyIconW(NIM_MODIFY, ref data);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var data = CreateData(0);
        Shell_NotifyIconW(NIM_DELETE, ref data);
        _window.DestroyHandle();
    }

    private void AddToTray()
    {
        var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP | NIF_SHOWTIP);
        data.uCallbackMessage = CallbackMessage;
        data.hIcon = _icon.Handle;
        data.szTip = Truncate(_toolTip, 127);

        // ADD fails if Explorer isn't running yet (TaskbarCreated will retry) or if the icon still
        // exists (TaskbarCreated is also sent on DPI changes), in which case MODIFY refreshes it.
        if (!Shell_NotifyIconW(NIM_ADD, ref data) && !Shell_NotifyIconW(NIM_MODIFY, ref data))
        {
            return;
        }

        // Version 4 gives NIN_SELECT / WM_CONTEXTMENU (mouse and keyboard) with anchor coordinates.
        data.uVersionOrTimeout = NOTIFYICON_VERSION_4;
        Shell_NotifyIconW(NIM_SETVERSION, ref data);
    }

    private NOTIFYICONDATAW CreateData(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATAW>(),
        hWnd = _window.Handle,
        uID = IconId,
        uFlags = flags,
        szTip = string.Empty,
        szInfo = string.Empty,
        szInfoTitle = string.Empty,
    };

    private bool ProcessMessage(ref Message m)
    {
        if (m.Msg == CallbackMessage)
        {
            // Version 4: LOWORD(lParam) = event, wParam = anchor x/y as signed 16-bit values.
            int notification = (int)((long)m.LParam & 0xFFFF);
            long wParam = (long)m.WParam;
            var anchor = new Point((short)(wParam & 0xFFFF), (short)((wParam >> 16) & 0xFFFF));

            switch (notification)
            {
                case NIN_SELECT:
                case NIN_KEYSELECT:
                    LeftClick?.Invoke(this, new TrayIconClickEventArgs(anchor));
                    break;
                case WM_CONTEXTMENU:
                    RightClick?.Invoke(this, new TrayIconClickEventArgs(anchor));
                    break;
            }

            return true;
        }

        if (s_taskbarCreatedMessage != 0 && m.Msg == s_taskbarCreatedMessage)
        {
            AddToTray();
            return true;
        }

        return false;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed class MessageWindow(TrayIcon owner) : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            if (!owner.ProcessMessage(ref m))
            {
                base.WndProc(ref m);
            }
        }
    }
}
