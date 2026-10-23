namespace QuickTools.Tests;

public class CommandLineTests
{
    [Fact]
    public void NoArguments_Runs() =>
        Assert.Equal(CommandLineAction.Run, CommandLine.Parse([]).Action);

    [Theory]
    [InlineData("-h")]
    [InlineData("--help")]
    [InlineData("/?")]
    [InlineData("-?")]
    [InlineData("--HELP")]
    [InlineData("-H")]
    public void HelpOptions(string arg) =>
        Assert.Equal(CommandLineAction.ShowHelp, CommandLine.Parse([arg]).Action);

    [Theory]
    [InlineData("-v")]
    [InlineData("--version")]
    [InlineData("-V")]
    [InlineData("--Version")]
    public void VersionOptions(string arg) =>
        Assert.Equal(CommandLineAction.ShowVersion, CommandLine.Parse([arg]).Action);

    [Theory]
    [InlineData("--verbose")]
    [InlineData("help")]
    [InlineData("-x")]
    [InlineData("")]
    public void UnknownOption_IsAnErrorNamingIt(string arg)
    {
        var result = CommandLine.Parse([arg]);

        Assert.Equal(CommandLineAction.Error, result.Action);
        Assert.Equal($"Unknown option: {arg}", result.Error);
    }

    [Fact]
    public void MultipleArguments_IsAnError()
    {
        var result = CommandLine.Parse(["-h", "-v"]);

        Assert.Equal(CommandLineAction.Error, result.Action);
        Assert.Equal("Too many arguments: -h -v", result.Error);
    }

    [Fact]
    public void Pin_CarriesThePath()
    {
        var result = CommandLine.Parse(["--pin", @"C:\Tools\My App.exe"]);

        Assert.Equal(CommandLineAction.Pin, result.Action);
        Assert.Equal(@"C:\Tools\My App.exe", result.Path);
    }

    [Theory]
    [InlineData("--PIN")]
    [InlineData("--Pin")]
    public void Pin_IsCaseInsensitive(string option) =>
        Assert.Equal(CommandLineAction.Pin, CommandLine.Parse([option, @"C:\x"]).Action);

    [Fact]
    public void Pin_WithoutExactlyOnePath_IsAnError()
    {
        Assert.Equal(CommandLineAction.Error, CommandLine.Parse(["--pin"]).Action);
        Assert.Equal(CommandLineAction.Error, CommandLine.Parse(["--pin", " "]).Action);
        Assert.Equal(CommandLineAction.Error, CommandLine.Parse(["--pin", "a", "b"]).Action);
    }

    [Fact]
    public void HelpText_ListsEveryOptionAndTheSettingsPath()
    {
        string help = CommandLine.HelpText("1.2.3 (abcdef1)", @"C:\Users\me\AppData\Roaming\QuickTools\settings.json");

        Assert.StartsWith("QuickTools 1.2.3 (abcdef1)", help);
        Assert.Contains("-h, --help, /?", help);
        Assert.Contains("-v, --version", help);
        Assert.Contains("--pin <path>", help);
        Assert.Contains(@"C:\Users\me\AppData\Roaming\QuickTools\settings.json", help);
    }

    [Theory]
    [InlineData("1.0.2+995c07a5d1beaba1266d18a380a3aa1d80341b82", "1.0.2 (995c07a)")]
    [InlineData("1.0.2-ci+abc", "1.0.2-ci (abc)")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData("1.0.0+", "1.0.0")]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    public void AppVersion_Format(string? informational, string expected) =>
        Assert.Equal(expected, AppVersion.Format(informational));

    [Theory]
    [InlineData("1.0.3+ffe81c0a1b2c3d4e5f60718293a4b5c6d7e8f901", "1.0.3")]
    [InlineData("1.0.3-ci+abc", "1.0.3-ci")]
    [InlineData("1.0.0", "1.0.0")]
    [InlineData(null, "unknown")]
    public void AppVersion_NumberOf(string? informational, string expected) =>
        Assert.Equal(expected, AppVersion.NumberOf(informational));
}
