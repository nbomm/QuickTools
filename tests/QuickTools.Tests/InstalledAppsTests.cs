using QuickTools.Services;

namespace QuickTools.Tests;

public class InstalledAppsTests
{
    private static readonly InstalledApp[] Apps =
    [
        new("Calculator", "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"),
        new("Notepad", "Microsoft.WindowsNotepad_8wekyb3d8bbwe!App"),
        new("Visual Studio Code", "Microsoft.VisualStudioCode"),
    ];

    [Theory]
    [InlineData("", new[] { "Calculator", "Notepad", "Visual Studio Code" })]
    [InlineData("   ", new[] { "Calculator", "Notepad", "Visual Studio Code" })]
    [InlineData("CALC", new[] { "Calculator" })]
    [InlineData("code studio", new[] { "Visual Studio Code" })]
    [InlineData("o", new[] { "Calculator", "Notepad", "Visual Studio Code" })]
    [InlineData("note code", new string[0])]
    [InlineData("paint", new string[0])]
    public void Filter_MatchesEveryWordIgnoringCase(string query, string[] expected) =>
        Assert.Equal(expected, InstalledApps.Filter(Apps, query).Select(app => app.Name));

    [Fact]
    public void Target_IsAnAppsFolderShellPath() =>
        Assert.Equal(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App", Apps[0].Target);

    [Theory]
    [InlineData("Notepad", "Notepad")]
    [InlineData("AT&T", "AT&&T")]
    public void MenuName_EscapesAmpersands(string name, string expected) =>
        Assert.Equal(expected, new InstalledApp(name, "id").MenuName);
}
