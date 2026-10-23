using QuickTools.Services;

namespace QuickTools.Tests;

public class PathResolverTests
{
    [Theory]
    [InlineData(@"\\server\share\app.exe", true)]
    [InlineData(@"\\?\UNC\server\share\app.exe", true)]
    [InlineData(@"\\?\C:\Windows\notepad.exe", false)]
    [InlineData(@"\\.\PhysicalDrive0", false)]
    [InlineData(@"C:\Windows\notepad.exe", false)]
    [InlineData("notepad.exe", false)]
    [InlineData("https://example.com", false)]
    public void IsNetworkPath(string path, bool expected) =>
        Assert.Equal(expected, PathResolver.IsNetworkPath(path));

    [Fact]
    public void Resolve_ReturnsUncPathWithoutCheckingItExists()
    {
        // 192.0.2.0/24 is reserved for documentation, so probing it would hang; Resolve must not try.
        const string path = @"\\192.0.2.1\share\app.exe";

        Assert.Equal(path, PathResolver.Resolve(path));
    }

    [Fact]
    public void Resolve_FindsBareNameOnPath() =>
        Assert.Equal(
            Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            PathResolver.Resolve("cmd"),
            StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("ms-settings:display")]
    [InlineData("shell:AppsFolder")]
    [InlineData(@"shell:AppsFolder\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App")]
    public void Resolve_ReturnsNullForUris(string target) => Assert.Null(PathResolver.Resolve(target));

    [Theory]
    [InlineData(@"%SystemRoot%", true)]
    [InlineData(@"""C:\Windows""", true)]
    [InlineData(@"C:\Windows\notepad.exe", false)]    // a file
    [InlineData(@"C:\no\such\folder", false)]
    [InlineData("windows", false)]                    // bare names only resolve to programs
    [InlineData("shell:Downloads", false)]
    [InlineData("https://example.com", false)]
    [InlineData(@"\\192.0.2.1\share", false)]        // network paths are never probed
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsLocalDirectory(string? target, bool expected) =>
        Assert.Equal(expected, PathResolver.IsLocalDirectory(target));
}
