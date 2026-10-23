using QuickTools.Model;

namespace QuickTools.Tests;

public class HotkeyTests
{
    // HotkeyModifiers is internal, so theories pass its MOD_* values (Alt = 1, Ctrl = 2, Shift = 4) as ints.
    private const int Alt = 1, Ctrl = 2, Shift = 4;

    [Theory]
    [InlineData("Ctrl+Alt+Q", Keys.Q, Ctrl | Alt)]
    [InlineData("ctrl + shift + f5", Keys.F5, Ctrl | Shift)]
    [InlineData("Control+Alt+Shift+Z", Keys.Z, Ctrl | Alt | Shift)]
    [InlineData("Alt+1", Keys.D1, Alt)]
    [InlineData("Ctrl+Space", Keys.Space, Ctrl)]
    [InlineData("F12", Keys.F12, 0)]
    public void TryParse_AcceptsValidHotkeys(string text, Keys key, int modifiers)
    {
        Assert.True(Hotkey.TryParse(text, out var hotkey));
        Assert.Equal(new Hotkey(key, (HotkeyModifiers)modifiers), hotkey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Q")]              // a bare letter would be swallowed everywhere
    [InlineData("Ctrl")]           // no key
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Q+W")]       // two keys
    [InlineData("Ctrl++")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+Bogus")]
    [InlineData("Ctrl+999")]       // a raw number, not a key name
    [InlineData("Ctrl+KeyCode")]   // a mask in the Keys enum, not a key
    [InlineData("Ctrl+ShiftKey")]
    [InlineData("Ctrl+LButton")]
    [InlineData("Ctrl+A, B")]
    public void TryParse_RejectsInvalidText(string? text)
    {
        Assert.False(Hotkey.TryParse(text, out _));
    }

    [Theory]
    [InlineData(Keys.Q, Ctrl | Alt, "Ctrl+Alt+Q")]
    [InlineData(Keys.F5, Shift | Ctrl, "Ctrl+Shift+F5")]
    [InlineData(Keys.D7, Alt, "Alt+7")]
    [InlineData(Keys.F1, 0, "F1")]
    public void ToString_UsesStableOrderAndNames(Keys key, int modifiers, string expected)
    {
        Assert.Equal(expected, new Hotkey(key, (HotkeyModifiers)modifiers).ToString());
    }

    [Theory]
    [InlineData(Keys.Q, Ctrl | Alt)]
    [InlineData(Keys.D0, Ctrl)]
    [InlineData(Keys.NumPad5, Alt)]
    [InlineData(Keys.Oemtilde, Ctrl)]
    [InlineData(Keys.PageDown, Ctrl | Shift)]
    [InlineData(Keys.F24, 0)]
    public void ToString_ThenTryParse_RoundTrips(Keys key, int modifiers)
    {
        var original = new Hotkey(key, (HotkeyModifiers)modifiers);

        Assert.True(Hotkey.TryParse(original.ToString(), out var parsed));
        Assert.Equal(original, parsed);
    }
}
