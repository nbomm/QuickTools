using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.Menus;

/// <summary>
/// Turns menu entries into a ContextMenuStrip. The Settings and Exit items are appended here rather than
/// stored in the settings file, so neither a hand edit nor a settings bug can remove them.
/// </summary>
internal sealed class MenuBuilder(IconCache icons, Action<MenuEntry> launch, Action openSettings, Action exit)
{
    /// <summary>Longest folder listing in a submenu; a menu of thousands of items is unusable and slow to build.</summary>
    private const int MaxDirectoryItems = 200;

    public ContextMenuStrip Build(IReadOnlyList<MenuEntry> entries)
    {
        var menu = new ContextMenuStrip();
        _ = new MenuScroller(menu); // hooks the menu's events and disposes itself with it
        AddEntries(menu.Items, entries);

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(CreateItem("&Settings…", icons.SettingsIcon, openSettings));
        menu.Items.Add(CreateItem("E&xit", icons.ExitIcon, exit));
        return menu;
    }

    private void AddEntries(ToolStripItemCollection items, IEnumerable<MenuEntry> entries)
    {
        foreach (var entry in entries)
        {
            switch (entry.Kind)
            {
                case EntryKind.Separator:
                    items.Add(new ToolStripSeparator());
                    break;

                case EntryKind.Folder:
                    var folder = new ToolStripMenuItem(entry.Name, icons.FolderIcon);
                    AddEntries(folder.DropDownItems, entry.Children ?? []);
                    if (folder.DropDownItems.Count == 0)
                    {
                        folder.DropDownItems.Add(new ToolStripMenuItem("(empty)") { Enabled = false });
                    }

                    items.Add(folder);
                    break;

                case EntryKind.Shortcut when PathResolver.IsLocalDirectory(entry.Target):
                    items.Add(CreateDirectoryMenu(entry.Name, icons.Get(entry) ?? icons.FolderIcon, PathResolver.Normalize(entry.Target!), entry));
                    break;

                default:
                    var item = CreateItem(entry.Name, icons.Get(entry), () => launch(entry));
                    item.ToolTipText = entry.Target;
                    items.Add(item);
                    break;
            }
        }
    }

    /// <summary>
    /// A submenu listing a folder's contents. It is filled each time it opens, so it is always current and
    /// folders that are never opened cost nothing. The first item opens the folder itself.
    /// </summary>
    /// <param name="openEntry">What the "Open folder" item launches.</param>
    private ToolStripMenuItem CreateDirectoryMenu(string text, Image? image, string path, MenuEntry openEntry)
    {
        var menu = new ToolStripMenuItem(text, image) { ToolTipText = path };

        // A submenu needs an item to show its arrow and to open at all; PopulateDirectoryMenu replaces it.
        menu.DropDownItems.Add(new ToolStripMenuItem());
        menu.DropDownOpening += (_, _) => PopulateDirectoryMenu(menu, path, openEntry);
        return menu;
    }

    private void PopulateDirectoryMenu(ToolStripMenuItem menu, string path, MenuEntry openEntry)
    {
        var previous = menu.DropDownItems.Cast<ToolStripItem>().ToArray();
        menu.DropDownItems.Clear();
        foreach (var item in previous)
        {
            item.Dispose();
        }

        menu.DropDownItems.Add(CreateItem("Open folder", icons.FolderIcon, () => launch(openEntry)));
        menu.DropDownItems.Add(new ToolStripSeparator());

        var contents = DirectoryListing.List(path, MaxDirectoryItems);
        foreach (var child in contents.Items)
        {
            // Menu text treats & as a keyboard key marker.
            string text = child.DisplayName.Replace("&", "&&");
            var childEntry = MenuEntry.Shortcut(child.DisplayName, child.FullPath);

            if (child.IsDirectory)
            {
                menu.DropDownItems.Add(CreateDirectoryMenu(text, icons.FolderIcon, child.FullPath, childEntry));
            }
            else
            {
                var file = CreateItem(text, icons.GetFileIcon(child.FullPath), () => launch(childEntry));
                file.ToolTipText = child.FullPath;
                menu.DropDownItems.Add(file);
            }
        }

        if (contents.Error is not null)
        {
            menu.DropDownItems.Add(new ToolStripMenuItem("(can't read this folder)") { Enabled = false, ToolTipText = contents.Error });
        }
        else if (contents.Items.Count == 0)
        {
            menu.DropDownItems.Add(new ToolStripMenuItem("(empty)") { Enabled = false });
        }
        else if (contents.MoreCount > 0)
        {
            menu.DropDownItems.Add(new ToolStripSeparator());
            menu.DropDownItems.Add(CreateItem($"{contents.MoreCount:N0} more…", icons.FolderIcon, () => launch(openEntry)));
        }
    }

    private static ToolStripMenuItem CreateItem(string text, Image? image, Action action)
    {
        var item = new ToolStripMenuItem(text, image);

        // Run the action after the click finishes, so the menu is never disposed (Exit, rebuild)
        // or blocked (UAC prompt) while it is still handling its own event.
        item.Click += (_, _) => SynchronizationContext.Current!.Post(_ => action(), null);
        return item;
    }
}
