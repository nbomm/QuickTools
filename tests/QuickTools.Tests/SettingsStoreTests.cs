using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "QuickTools.Tests", Guid.NewGuid().ToString("N"));

    private string SettingsPath => Path.Combine(_directory, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void MissingFile_LoadsAndWritesDefaults()
    {
        var store = new SettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.True(settings.MirrorMenus);
        Assert.Equal(["Notepad", "File Explorer", "Microsoft Edge"], settings.LeftMenu.Select(e => e.Name));
        Assert.Equal(["Notepad", "File Explorer", "Microsoft Edge"], settings.RightMenu.Select(e => e.Name));
        Assert.True(File.Exists(SettingsPath));
        Assert.Null(store.LastLoadError);
    }

    [Fact]
    public void MissingFile_IsFirstRun_ButOnlyUntilTheFileExists()
    {
        var store = new SettingsStore(SettingsPath);

        store.Load();
        Assert.True(store.IsFirstRun);

        store.Load();
        Assert.False(store.IsFirstRun);
    }

    [Fact]
    public void ExistingFile_IsNotFirstRun()
    {
        var store = new SettingsStore(SettingsPath);
        store.Save(AppSettings.CreateDefault());

        store.Load();

        Assert.False(store.IsFirstRun);
    }

    [Fact]
    public void CorruptFile_IsNotFirstRun()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ not json");
        var store = new SettingsStore(SettingsPath);

        store.Load();

        Assert.False(store.IsFirstRun);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsNestedFolders()
    {
        var store = new SettingsStore(SettingsPath);
        var original = new AppSettings
        {
            MirrorMenus = false,
            LeftMenu =
            [
                MenuEntry.Folder("Dev",
                [
                    new MenuEntry { Name = "Terminal", Target = "wt.exe", Arguments = "-d .", RunAsAdmin = true },
                    MenuEntry.Folder("Empty"),
                ]),
                MenuEntry.Separator(),
                MenuEntry.Shortcut("Docs", "https://learn.microsoft.com"),
            ],
            RightMenu = [MenuEntry.Shortcut("Notepad", "notepad.exe")],
        };

        store.Save(original);
        store.Save(original); // second save goes through File.Replace
        var loaded = store.Load();

        Assert.False(loaded.MirrorMenus);
        Assert.Equal(3, loaded.LeftMenu.Count);
        var dev = loaded.LeftMenu[0];
        Assert.Equal(EntryKind.Folder, dev.Kind);
        Assert.Equal("Terminal", dev.Children![0].Name);
        Assert.Equal("-d .", dev.Children[0].Arguments);
        Assert.True(dev.Children[0].RunAsAdmin);
        Assert.Empty(dev.Children[1].Children!);
        Assert.Equal(EntryKind.Separator, loaded.LeftMenu[1].Kind);
        Assert.Equal("notepad.exe", loaded.RightMenu[0].Target);
    }

    [Fact]
    public void NewSettings_UseTheDefaultHotkey()
    {
        Assert.Equal("Ctrl+Alt+Q", new SettingsStore(SettingsPath).Load().Hotkey);
    }

    [Theory]
    [InlineData("Ctrl+Shift+F5")]
    [InlineData("")] // turned off
    public void Hotkey_RoundTrips(string hotkey)
    {
        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();
        settings.Hotkey = hotkey;
        store.Save(settings);

        Assert.Equal(hotkey, store.Load().Hotkey);
    }

    [Fact]
    public void SettingsFileWithoutHotkey_GetsTheDefault()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "mirrorMenus": true, "leftMenu": [], "rightMenu": [] }""");

        Assert.Equal("Ctrl+Alt+Q", new SettingsStore(SettingsPath).Load().Hotkey);
    }

    [Fact]
    public void CorruptFile_IsBackedUpAndDefaultsAreUsed()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, "{ not json");
        var store = new SettingsStore(SettingsPath);

        var settings = store.Load();

        Assert.Equal(3, settings.LeftMenu.Count);
        Assert.NotNull(store.LastLoadError);
        Assert.Equal("{ not json", File.ReadAllText(SettingsPath + ".bad"));
    }

    [Fact]
    public void LockedFile_UsesDefaultsWithoutOverwriting()
    {
        var original = new AppSettings { LeftMenu = [MenuEntry.Shortcut("Mine", "calc.exe")] };
        var store = new SettingsStore(SettingsPath);
        store.Save(original);
        string before = File.ReadAllText(SettingsPath);

        AppSettings settings;
        using (new FileStream(SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            settings = store.Load();
        }

        Assert.Equal("Notepad", settings.LeftMenu[0].Name);
        Assert.NotNull(store.LastLoadError);
        Assert.Equal(before, File.ReadAllText(SettingsPath));
        Assert.False(File.Exists(SettingsPath + ".bad"));
    }

    [Fact]
    public void ExplicitNulls_AreNormalized()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(SettingsPath, """{ "mirrorMenus": false, "hotkey": null, "leftMenu": null, "rightMenu": [ { "kind": "Folder", "name": "F", "children": null } ] }""");

        var settings = new SettingsStore(SettingsPath).Load();

        Assert.False(settings.MirrorMenus);
        Assert.Equal(string.Empty, settings.Hotkey);
        Assert.Empty(settings.LeftMenu);
        Assert.NotNull(settings.RightMenu[0].Children);
    }
}
