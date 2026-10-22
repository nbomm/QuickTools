using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.UI;

/// <summary>
/// Tree editor for one menu. The tree itself is the working copy: each node's Tag holds its entry
/// (minus children), and <see cref="GetEntries"/> rebuilds the model from the tree's structure.
/// </summary>
internal sealed class MenuEditor : UserControl
{
    private const string NoImageKey = "none";
    private const string FolderImageKey = "folder";
    private const string SeparatorText = "──────────────";
    private const int MinimumTreeWidth = 200;
    private const int ButtonGap = 6;

    private readonly IconCache _icons;
    private readonly Launcher _launcher;
    private readonly ImageList _images;
    private readonly Bitmap _blankImage;
    private readonly TreeView _tree;
    private readonly TableLayoutPanel _layout;
    private readonly FlowLayoutPanel _buttons;
    private readonly Label _footer;
    private readonly Button _edit;
    private readonly Button _remove;
    private readonly Button _moveUp;
    private readonly Button _moveDown;
    private readonly Button _testLaunch;

    public MenuEditor(IconCache icons, Launcher launcher)
    {
        _icons = icons;
        _launcher = launcher;

        int imageSize = LogicalToDeviceUnits(16);
        _images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(imageSize, imageSize) };
        // ImageList keeps the original until its handle exists, so this is disposed with the control.
        _blankImage = new Bitmap(imageSize, imageSize);
        _images.Images.Add(NoImageKey, _blankImage);
        _images.Images.Add(FolderImageKey, icons.FolderIcon);

        _tree = new TreeView
        {
            Dock = DockStyle.Fill,
            HideSelection = false,
            AllowDrop = true,
            ImageList = _images,
            ImageKey = NoImageKey,
            SelectedImageKey = NoImageKey,
        };
        _tree.AfterSelect += (_, _) => UpdateButtons();
        _tree.NodeMouseDoubleClick += (_, e) => { _tree.SelectedNode = e.Node; EditSelected(); };
        _tree.KeyDown += OnTreeKeyDown;
        _tree.ItemDrag += (_, e) => { if (e.Item is TreeNode node) DoDragDrop(node, DragDropEffects.Move); };
        _tree.DragEnter += OnDragOver;
        _tree.DragOver += OnDragOver;
        _tree.DragDrop += OnDragDrop;

        _buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,

            // Only kicks in when the window is limited to a screen too small for all buttons.
            AutoScroll = true,
        };
        _buttons.Controls.Add(CreateButton("Add &Shortcut…", AddShortcut));
        _buttons.Controls.Add(CreateButton("Add &Folder…", AddFolder));
        _buttons.Controls.Add(CreateButton("Add Se&parator", AddSeparator));
        _buttons.Controls.Add(_edit = CreateButton("&Edit…", EditSelected));
        _buttons.Controls.Add(_remove = CreateButton("&Remove", RemoveSelected));
        _buttons.Controls.Add(_moveUp = CreateButton("Move &Up", () => MoveSelected(-1)));
        _buttons.Controls.Add(_moveDown = CreateButton("Move &Down", () => MoveSelected(1)));
        _buttons.Controls.Add(_testLaunch = CreateButton("&Test Launch", TestLaunch));

        // No outer margins, so the first and last buttons line up with the tree's top and bottom edges.
        // Logical units: the form's autoscaling scales margins.
        var allButtons = _buttons.Controls.Cast<Control>().ToList();
        foreach (var button in allButtons)
        {
            button.Margin = new Padding(0, 0, 0, button == allButtons[^1] ? 0 : ButtonGap);
        }

        _buttons.Layout += (_, _) =>
        {
            StretchButtons();
            ReserveScrollBarSpace();
        };

        _footer = new Label
        {
            Text = "Settings… and Exit are always added to the end of this menu. " +
                "Drag items to reorder or move them into folders; drop files from Explorer to add them.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 6, 3, 0),
            Dock = DockStyle.Fill,
        };

        _layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(_tree, 0, 0);
        _layout.Controls.Add(_buttons, 1, 0);
        _layout.Controls.Add(_footer, 0, 1);
        _layout.SetColumnSpan(_footer, 2);
        Controls.Add(_layout);

        UpdateButtons();
    }

    /// <summary>
    /// The smallest size at which no button is cut off and the tree is still usable, at the current DPI.
    /// Computed from the controls' real preferred sizes so it holds at any scaling.
    /// </summary>
    public Size GetMinimumContentSize()
    {
        // Measured from the buttons' natural size, not their current (stretched) size.
        var buttons = _buttons.Controls.Cast<Control>().ToList();
        Size natural = NaturalButtonSize(buttons);
        int buttonsHeight = buttons.Sum(b => natural.Height + b.Margin.Vertical);

        int width = LogicalToDeviceUnits(MinimumTreeWidth) + _tree.Margin.Horizontal
            + natural.Width + _buttons.Margin.Horizontal;

        // The footer wraps, so its height depends on the width it gets.
        int footerHeight = _footer.GetPreferredSize(new Size(width - _footer.Margin.Horizontal, 0)).Height
            + _footer.Margin.Vertical;
        int height = buttonsHeight + _buttons.Margin.Vertical + footerHeight;

        return new Size(width + Padding.Horizontal, height + Padding.Vertical);
    }

    /// <summary>
    /// Gives every button the same width, and heights that together fill the column exactly, so the
    /// stack spans the same height as the tree. Below their natural height the column scrolls instead.
    /// </summary>
    private void StretchButtons()
    {
        var buttons = _buttons.Controls.Cast<Control>().ToList();
        Size natural = NaturalButtonSize(buttons);
        int available = _buttons.ClientSize.Height - buttons.Sum(b => b.Margin.Vertical);
        int height = Math.Max(natural.Height, available / buttons.Count);

        // Spread the leftover pixels so the last button ends exactly at the bottom edge.
        int leftover = height > natural.Height ? available - height * buttons.Count : 0;

        for (int i = 0; i < buttons.Count; i++)
        {
            var size = new Size(natural.Width, height + (i < leftover ? 1 : 0));
            if (buttons[i].Size != size)
            {
                buttons[i].Size = size;
            }
        }
    }

    private static Size NaturalButtonSize(List<Control> buttons) => new(
        buttons.Max(b => Math.Max(b.GetPreferredSize(Size.Empty).Width, b.MinimumSize.Width)),
        buttons.Max(b => b.GetPreferredSize(Size.Empty).Height));

    /// <summary>
    /// When the buttons need to scroll, widen the column by the scrollbar's width so the vertical
    /// scrollbar doesn't squeeze the buttons and cause a horizontal one as well.
    /// </summary>
    private void ReserveScrollBarSpace()
    {
        var buttons = _buttons.Controls.Cast<Control>().ToList();
        bool scrolling = buttons.Sum(c => c.Height + c.Margin.Vertical) > _buttons.ClientSize.Height;

        var style = scrolling
            ? new ColumnStyle(SizeType.Absolute, buttons.Max(c => c.Width + c.Margin.Horizontal)
                + SystemInformation.GetVerticalScrollBarWidthForDpi(DeviceDpi) + _buttons.Margin.Horizontal)
            : new ColumnStyle(SizeType.AutoSize);

        var current = _layout.ColumnStyles[1];
        if (current.SizeType != style.SizeType || current.Width != style.Width)
        {
            // This runs while the table is laying out its children, when it ignores new layout requests,
            // so ask for one once the current pass has finished.
            _layout.ColumnStyles[1] = style;
            if (IsHandleCreated)
            {
                BeginInvoke(() => _layout.PerformLayout());
            }
        }
    }

    public void LoadEntries(IEnumerable<MenuEntry> entries)
    {
        _tree.BeginUpdate();
        _tree.Nodes.Clear();
        foreach (var entry in entries)
        {
            _tree.Nodes.Add(CreateNode(entry));
        }

        _tree.ExpandAll();
        _tree.EndUpdate();
        UpdateButtons();
    }

    public List<MenuEntry> GetEntries() => ReadNodes(_tree.Nodes);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _images.Dispose();
            _blankImage.Dispose();
        }

        base.Dispose(disposing);
    }

    private static List<MenuEntry> ReadNodes(TreeNodeCollection nodes) =>
        nodes.Cast<TreeNode>().Select(node =>
        {
            var entry = EntryOf(node).Clone();
            entry.Children = entry.Kind == EntryKind.Folder ? ReadNodes(node.Nodes) : null;
            return entry;
        }).ToList();

    private static MenuEntry EntryOf(TreeNode node) => (MenuEntry)node.Tag!;

    private TreeNode CreateNode(MenuEntry entry)
    {
        var node = new TreeNode();
        ApplyEntry(node, entry);

        if (entry.Kind == EntryKind.Folder)
        {
            foreach (var child in entry.Children ?? [])
            {
                node.Nodes.Add(CreateNode(child));
            }
        }

        return node;
    }

    private void ApplyEntry(TreeNode node, MenuEntry entry)
    {
        // Children live in the tree, not the tag, so the two can't drift apart.
        var tag = entry.Clone();
        tag.Children = null;
        node.Tag = tag;

        node.Text = entry.Kind == EntryKind.Separator ? SeparatorText : entry.Name;
        node.ForeColor = entry.Kind == EntryKind.Separator ? SystemColors.GrayText : Color.Empty;
        node.ToolTipText = entry.Target ?? string.Empty;
        node.ImageKey = node.SelectedImageKey = ImageKeyFor(entry);
    }

    private string ImageKeyFor(MenuEntry entry)
    {
        switch (entry.Kind)
        {
            case EntryKind.Folder:
                return FolderImageKey;
            case EntryKind.Separator:
                return NoImageKey;
        }

        string key = "file:" + IconCache.CacheKey(entry);
        if (!_images.Images.ContainsKey(key))
        {
            if (_icons.Get(entry) is not { } image)
            {
                return NoImageKey;
            }

            _images.Images.Add(key, image);
        }

        return key;
    }

    private void AddShortcut()
    {
        var entry = new MenuEntry { Kind = EntryKind.Shortcut };
        using var editor = new EntryEditorForm(entry, isNew: true);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            InsertNode(CreateNode(entry), _tree.SelectedNode);
        }
    }

    private void AddFolder()
    {
        var entry = MenuEntry.Folder(string.Empty);
        using var editor = new EntryEditorForm(entry, isNew: true);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            InsertNode(CreateNode(entry), _tree.SelectedNode);
        }
    }

    private void AddSeparator() => InsertNode(CreateNode(MenuEntry.Separator()), _tree.SelectedNode);

    /// <summary>Inserts into a target folder, after a target item, or at the end when there's no target.</summary>
    private void InsertNode(TreeNode node, TreeNode? target)
    {
        if (target is null)
        {
            _tree.Nodes.Add(node);
        }
        else if (EntryOf(target).Kind == EntryKind.Folder)
        {
            target.Nodes.Add(node);
            target.Expand();
        }
        else
        {
            SiblingsOf(target).Insert(target.Index + 1, node);
        }

        _tree.SelectedNode = node;
        _tree.Focus();
    }

    private void EditSelected()
    {
        if (_tree.SelectedNode is not { } node || EntryOf(node).Kind == EntryKind.Separator)
        {
            return;
        }

        var copy = EntryOf(node).Clone();
        using var editor = new EntryEditorForm(copy, isNew: false);
        if (editor.ShowDialog(this) == DialogResult.OK)
        {
            ApplyEntry(node, copy);
        }
    }

    private void RemoveSelected()
    {
        if (_tree.SelectedNode is not { } node)
        {
            return;
        }

        if (node.Nodes.Count > 0 &&
            MessageBox.Show(this, $"Remove the folder \"{node.Text}\" and everything in it?", "Remove folder",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        node.Remove();
        UpdateButtons();
    }

    private void MoveSelected(int offset)
    {
        if (_tree.SelectedNode is not { } node)
        {
            return;
        }

        var siblings = SiblingsOf(node);
        int newIndex = node.Index + offset;
        if (newIndex < 0 || newIndex >= siblings.Count)
        {
            return;
        }

        bool expanded = node.IsExpanded;
        node.Remove();
        siblings.Insert(newIndex, node);
        if (expanded)
        {
            node.Expand();
        }

        _tree.SelectedNode = node;
    }

    private void TestLaunch()
    {
        if (_tree.SelectedNode is { } node && EntryOf(node).Kind == EntryKind.Shortcut &&
            !_launcher.TryLaunch(EntryOf(node), out string? error))
        {
            MessageBox.Show(this, error, $"Couldn't open {node.Text}", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Delete:
                RemoveSelected();
                e.Handled = true;
                break;
            case Keys.Enter:
                EditSelected();
                e.Handled = true;
                break;
            case Keys.Up when e.Alt:
                MoveSelected(-1);
                e.Handled = true;
                break;
            case Keys.Down when e.Alt:
                MoveSelected(1);
                e.Handled = true;
                break;
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
        else if (DraggedNode(e) is { } dragged)
        {
            var target = NodeAt(e);
            e.Effect = CanDrop(dragged, target) ? DragDropEffects.Move : DragDropEffects.None;
        }
        else
        {
            e.Effect = DragDropEffects.None;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        var target = NodeAt(e);

        if (e.Data?.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (string path in paths)
            {
                string name = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path);
                var node = CreateNode(MenuEntry.Shortcut(name, path));
                InsertNode(node, target);
                target = node; // keep multiple dropped files in their original order
            }

            return;
        }

        if (DraggedNode(e) is not { } dragged || !CanDrop(dragged, target))
        {
            return;
        }

        bool expanded = dragged.IsExpanded;

        if (target is null)
        {
            dragged.Remove();
            _tree.Nodes.Add(dragged);
        }
        else if (EntryOf(target).Kind == EntryKind.Folder)
        {
            dragged.Remove();
            target.Nodes.Add(dragged);
            target.Expand();
        }
        else
        {
            // Dragging down lands after the target, dragging up lands before it.
            bool movingDown = dragged.Parent == target.Parent && dragged.Index < target.Index;
            dragged.Remove();
            SiblingsOf(target).Insert(target.Index + (movingDown ? 1 : 0), dragged);
        }

        if (expanded)
        {
            dragged.Expand();
        }

        _tree.SelectedNode = dragged;
    }

    /// <summary>A node can't be dropped onto itself or into its own subtree.</summary>
    private static bool CanDrop(TreeNode dragged, TreeNode? target)
    {
        for (var node = target; node is not null; node = node.Parent)
        {
            if (node == dragged)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The node being dragged, only if it comes from this tree.</summary>
    private TreeNode? DraggedNode(DragEventArgs e) =>
        e.Data?.GetDataPresent(typeof(TreeNode)) == true && e.Data.GetData(typeof(TreeNode)) is TreeNode node && node.TreeView == _tree
            ? node
            : null;

    private TreeNode? NodeAt(DragEventArgs e) => _tree.GetNodeAt(_tree.PointToClient(new Point(e.X, e.Y)));

    private TreeNodeCollection SiblingsOf(TreeNode node) => node.Parent?.Nodes ?? _tree.Nodes;

    private void UpdateButtons()
    {
        var entry = _tree.SelectedNode is { } node ? EntryOf(node) : null;
        _edit.Enabled = entry is not null && entry.Kind != EntryKind.Separator;
        _remove.Enabled = entry is not null;
        _moveUp.Enabled = entry is not null;
        _moveDown.Enabled = entry is not null;
        _testLaunch.Enabled = entry?.Kind == EntryKind.Shortcut;
    }

    private static Button CreateButton(string text, Action onClick)
    {
        // Sized by StretchButtons rather than AutoSize, so the stack can fill the tree's height.
        // GrowAndShrink still matters: in the default GrowOnly mode a Button reports its current
        // (stretched) size as its preferred size, which would ratchet the natural size up.
        var button = new Button
        {
            Text = text,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(130, 0),
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
