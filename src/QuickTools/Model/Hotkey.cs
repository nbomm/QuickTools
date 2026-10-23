namespace QuickTools.Model;

/// <summary>Values match the MOD_* flags of RegisterHotKey.</summary>
[Flags]
internal enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
}

/// <summary>
/// A global hotkey. Stored in settings as text such as "Ctrl+Alt+Q"; <see cref="TryParse"/> and
/// <see cref="ToString"/> are inverses, and an empty setting means "no hotkey".
/// </summary>
internal readonly record struct Hotkey(Keys Key, HotkeyModifiers Modifiers)
{
    /// <summary>
    /// A hotkey needs a modifier, or it would swallow that key everywhere, except for the function keys,
    /// which are not typed text.
    /// </summary>
    public static bool IsValid(Keys key, HotkeyModifiers modifiers) =>
        IsRegisterableKey(key) && (modifiers != HotkeyModifiers.None || key is >= Keys.F1 and <= Keys.F24);

    public static bool TryParse(string? text, out Hotkey hotkey)
    {
        hotkey = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var modifiers = HotkeyModifiers.None;
        Keys? key = null;
        foreach (string part in text.Split('+', StringSplitOptions.TrimEntries))
        {
            switch (part.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    modifiers |= HotkeyModifiers.Control;
                    continue;
                case "alt":
                    modifiers |= HotkeyModifiers.Alt;
                    continue;
                case "shift":
                    modifiers |= HotkeyModifiers.Shift;
                    continue;
            }

            if (key is not null || !TryParseKey(part, out Keys parsed))
            {
                return false;
            }

            key = parsed;
        }

        if (key is not { } finalKey || !IsValid(finalKey, modifiers))
        {
            return false;
        }

        hotkey = new Hotkey(finalKey, modifiers);
        return true;
    }

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (Modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        // Keys.D5 is the digit row's 5; "5" reads better.
        parts.Add(Key is >= Keys.D0 and <= Keys.D9 ? ((char)('0' + (Key - Keys.D0))).ToString() : Key.ToString());
        return string.Join('+', parts);
    }

    private static bool TryParseKey(string name, out Keys key)
    {
        key = default;
        if (name.Length == 1 && char.IsAsciiDigit(name[0]))
        {
            name = "D" + name;
        }

        // Enum.TryParse also reads numbers and comma lists, which would let junk through as raw key codes.
        if (name.Length == 0 || char.IsAsciiDigit(name[0]) || !name.All(char.IsAsciiLetterOrDigit))
        {
            return false;
        }

        return Enum.TryParse(name, ignoreCase: true, out key) && Enum.IsDefined(key);
    }

    /// <summary>Real keys only: not the mouse buttons, the modifier keys themselves, or the masks in <see cref="Keys"/>.</summary>
    private static bool IsRegisterableKey(Keys key) =>
        key is > Keys.XButton2 and <= (Keys)0xFE
        && key is not (Keys.ShiftKey or Keys.ControlKey or Keys.Menu or Keys.LShiftKey or Keys.RShiftKey
            or Keys.LControlKey or Keys.RControlKey or Keys.LMenu or Keys.RMenu or Keys.LWin or Keys.RWin);
}
