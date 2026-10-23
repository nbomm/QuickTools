using QuickTools.Model;
using QuickTools.Tray;

namespace QuickTools.Tests;

public class HotkeyListenerTests
{
    // Unlikely to be taken by anything else on a developer machine or CI runner.
    private static readonly Hotkey Unusual = new(Keys.F23, HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift);

    [Fact]
    public void HotkeyAlreadyTaken_FailsToRegister_AndFreedHotkeyCanBeRegisteredAgain()
    {
        using var first = new HotkeyListener();
        using var second = new HotkeyListener();

        Assert.True(first.TryRegister(Unusual));
        Assert.False(second.TryRegister(Unusual));

        Assert.True(first.TryRegister(null));
        Assert.True(second.TryRegister(Unusual));
    }

    [Fact]
    public void ReRegisteringTheSameHotkey_Succeeds()
    {
        using var listener = new HotkeyListener();

        Assert.True(listener.TryRegister(Unusual));
        Assert.True(listener.TryRegister(Unusual));
    }
}
