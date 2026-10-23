using QuickTools.Services;

namespace QuickTools.Tests;

public class ShellIntegrationTests
{
    [Fact]
    public void Command_QuotesTheExeAndExplorersPlaceholder() =>
        Assert.Equal("\"C:\\Program Files\\QuickTools\\QuickTools.exe\" --pin \"%1\"",
            ShellIntegration.CommandFor(@"C:\Program Files\QuickTools\QuickTools.exe"));

    [Fact]
    public void MenuText_IsWhatTheUserSees() =>
        Assert.Equal("Pin to QuickTools", ShellIntegration.MenuText);
}
