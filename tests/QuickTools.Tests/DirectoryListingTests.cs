using QuickTools.Services;

namespace QuickTools.Tests;

public sealed class DirectoryListingTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "QuickTools.Tests", Guid.NewGuid().ToString("N"));

    public DirectoryListingTests() => Directory.CreateDirectory(_directory);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private string File(string name)
    {
        string path = Path.Combine(_directory, name);
        System.IO.File.WriteAllText(path, string.Empty);
        return path;
    }

    private string Folder(string name) => Directory.CreateDirectory(Path.Combine(_directory, name)).FullName;

    [Fact]
    public void FoldersComeFirst_ThenFiles_EachSortedIgnoringCase()
    {
        File("banana.txt");
        Folder("Zeta");
        File("Apple.txt");
        Folder("alpha");

        var contents = DirectoryListing.List(_directory, 100);

        Assert.Equal(["alpha", "Zeta", "Apple.txt", "banana.txt"], contents.Items.Select(i => i.Name));
        Assert.Equal([true, true, false, false], contents.Items.Select(i => i.IsDirectory));
        Assert.Null(contents.Error);
        Assert.Equal(0, contents.MoreCount);
    }

    [Fact]
    public void NumbersSortNumerically()
    {
        File("file10.txt");
        File("file2.txt");
        File("file1.txt");

        var contents = DirectoryListing.List(_directory, 100);

        Assert.Equal(["file1.txt", "file2.txt", "file10.txt"], contents.Items.Select(i => i.Name));
    }

    [Fact]
    public void HiddenAndSystemItemsAreSkipped()
    {
        File("visible.txt");
        System.IO.File.SetAttributes(File("hidden.txt"), FileAttributes.Hidden);
        System.IO.File.SetAttributes(File("system.sys"), FileAttributes.System);
        new DirectoryInfo(Folder("hiddenFolder")).Attributes |= FileAttributes.Hidden;

        var contents = DirectoryListing.List(_directory, 100);

        Assert.Equal(["visible.txt"], contents.Items.Select(i => i.Name));
    }

    [Fact]
    public void FullPathPointsAtTheItem()
    {
        string file = File("a.txt");
        string folder = Folder("sub");

        var contents = DirectoryListing.List(_directory, 100);

        Assert.Equal([folder, file], contents.Items.Select(i => i.FullPath));
    }

    [Fact]
    public void MoreThanTheLimit_KeepsTheFirstItemsAndCountsTheRest()
    {
        Folder("dir");
        for (int i = 1; i <= 5; i++)
        {
            File($"f{i}.txt");
        }

        var contents = DirectoryListing.List(_directory, 3);

        Assert.Equal(["dir", "f1.txt", "f2.txt"], contents.Items.Select(i => i.Name));
        Assert.Equal(3, contents.MoreCount);
    }

    [Fact]
    public void EmptyFolder_HasNoItemsAndNoError()
    {
        var contents = DirectoryListing.List(_directory, 100);

        Assert.Empty(contents.Items);
        Assert.Null(contents.Error);
    }

    [Fact]
    public void MissingFolder_ReportsAnErrorInsteadOfThrowing()
    {
        var contents = DirectoryListing.List(Path.Combine(_directory, "nope"), 100);

        Assert.Empty(contents.Items);
        Assert.NotNull(contents.Error);
    }

    [Theory]
    [InlineData("App.lnk", "App")]
    [InlineData("Site.url", "Site")]
    [InlineData("notes.txt", "notes.txt")]
    [InlineData("tool.exe", "tool.exe")]
    public void DisplayName_HidesShortcutExtensionsOnly(string name, string expected) =>
        Assert.Equal(expected, new DirectoryItem(name, name, IsDirectory: false).DisplayName);

    [Fact]
    public void DisplayName_KeepsDotsInFolderNames() =>
        Assert.Equal("v1.lnk", new DirectoryItem("v1.lnk", "v1.lnk", IsDirectory: true).DisplayName);
}
