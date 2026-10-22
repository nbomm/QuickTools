using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.Menus;

/// <summary>
/// Turns menu entries into a ContextMenuStrip. The Settings and Exit items are appended here rather than
/// stored in the settings file, so neither a hand edit nor a settings bug can remove them.
/// </summary>
internal sealed class MenuBuilder(IconCache icons, Action<MenuEntry> launch, Action openSettings, Action exit)
{
    public ContextMenuStrip Build(IReadOnlyList<MenuEntry> entries)
    {
        var menu = new ContextMenuStrip();
        AddEntries(menu.Items, entries);

        if (menu.Items.Count > 0)
        {
            menu.Items.Add(new ToolStripSeparator());
        }

        menu.Items.Add(CreateItem("&Settings…", icons.SettingsIcon, openSettings));
        menu.Items.Add(CreateItem("E&xit", null, exit));
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

                default:
                    var item = CreateItem(entry.Name, icons.Get(entry), () => launch(entry));
                    item.ToolTipText = entry.Target;
                    items.Add(item);
                    break;
            }
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
