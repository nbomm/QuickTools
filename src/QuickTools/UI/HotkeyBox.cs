using System.ComponentModel;
using QuickTools.Model;

namespace QuickTools.UI;

/// <summary>A read-only text box that records the key combination pressed in it instead of typing text.</summary>
internal sealed class HotkeyBox : TextBox
{
    private Hotkey? _value;

    public HotkeyBox()
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        // A read-only box is grey by default, which would look disabled.
        BackColor = SystemColors.Window;
        PlaceholderText = "None (disabled)";
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Hotkey? Value
    {
        get => _value;
        set
        {
            _value = value;
            Text = value?.ToString() ?? string.Empty;
        }
    }

    // Handled here rather than in OnKeyDown so that Alt combinations don't trigger the form's mnemonics first.
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        var modifiers = HotkeyModifiers.None;
        if (keyData.HasFlag(Keys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (keyData.HasFlag(Keys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (keyData.HasFlag(Keys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        // Tab and Escape keep moving focus and closing the window.
        if (key is Keys.Tab or Keys.Escape)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (modifiers == HotkeyModifiers.None && key is Keys.Back or Keys.Delete)
        {
            Value = null;
            return true;
        }

        if (Hotkey.IsValid(key, modifiers))
        {
            Value = new Hotkey(key, modifiers);
            return true;
        }

        // Modifier keys on their own are the start of a combination; an invalid key is ignored.
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
