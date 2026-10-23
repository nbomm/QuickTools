using QuickTools.Model;
using static QuickTools.Tray.NativeMethods;

namespace QuickTools.Tray;

/// <summary>Registers one system-wide hotkey with RegisterHotKey and raises <see cref="Pressed"/> when it is used.</summary>
internal sealed class HotkeyListener : IDisposable
{
    private const int HotkeyId = 1;

    private readonly MessageWindow _window;
    private bool _registered;

    public event EventHandler? Pressed;

    public HotkeyListener()
    {
        _window = new MessageWindow(this);
        _window.CreateHandle(new CreateParams());
    }

    /// <summary>
    /// Replaces the current hotkey; null just removes it. Returns false if the hotkey couldn't be registered,
    /// typically because another program or Windows already uses it, in which case there is no hotkey.
    /// </summary>
    public bool TryRegister(Hotkey? hotkey)
    {
        if (_registered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }

        if (hotkey is not { } value)
        {
            return true;
        }

        // NOREPEAT: holding the keys down opens the menu once, not once per auto-repeat.
        _registered = RegisterHotKey(_window.Handle, HotkeyId, (uint)value.Modifiers | MOD_NOREPEAT, (uint)value.Key);
        return _registered;
    }

    public void Dispose()
    {
        if (_registered)
        {
            UnregisterHotKey(_window.Handle, HotkeyId);
            _registered = false;
        }

        _window.DestroyHandle();
    }

    private sealed class MessageWindow(HotkeyListener owner) : NativeWindow
    {
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && (int)m.WParam == HotkeyId)
            {
                owner.Pressed?.Invoke(owner, EventArgs.Empty);
                return;
            }

            base.WndProc(ref m);
        }
    }
}
