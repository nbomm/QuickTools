using System.Runtime.InteropServices;
using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.UI;

/// <summary>
/// Lets the user pick apps from Start's All apps list (desktop and Store apps), with a search box.
/// The selection survives changes to the search, so several searches can add to one pick.
/// </summary>
internal sealed class AppPickerForm : Form
{
    private const string NoImageKey = "none";

    // Loading an icon can take tens of milliseconds, so they're loaded a few at a time between
    // messages; the list is usable at once and fills in.
    private const int IconsPerTick = 6;

    private readonly IReadOnlyList<InstalledApp> _apps;
    private readonly IconCache _icons;
    private readonly bool _multiSelect;
    private readonly HashSet<InstalledApp> _selected = [];
    private readonly HashSet<string> _iconsTried = new(StringComparer.OrdinalIgnoreCase);
    private readonly ImageList _images;
    private readonly Bitmap _blankImage;
    private readonly TextBox _search;
    private readonly ListView _list;
    private readonly Label _count;
    private readonly Button _ok;
    private readonly System.Windows.Forms.Timer _iconTimer = new() { Interval = 1 };
    private bool _rebuilding;

    private AppPickerForm(IReadOnlyList<InstalledApp> apps, IconCache icons, bool multiSelect)
    {
        _apps = apps;
        _icons = icons;
        _multiSelect = multiSelect;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = multiSelect ? "Add apps" : "Choose an app";
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(380, 480);
        MinimumSize = new Size(300, 300);
        Padding = new Padding(10);

        int imageSize = LogicalToDeviceUnits(16);
        _images = new ImageList { ColorDepth = ColorDepth.Depth32Bit, ImageSize = new Size(imageSize, imageSize) };
        // ImageList keeps the original until its handle exists, so this is disposed with the form.
        _blankImage = new Bitmap(imageSize, imageSize);
        _images.Images.Add(NoImageKey, _blankImage);

        _search = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Search apps", Margin = new Padding(0, 0, 0, 6) };
        _search.TextChanged += (_, _) => ShowMatches();
        _search.KeyDown += OnSearchKeyDown;

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            HeaderStyle = ColumnHeaderStyle.None,
            FullRowSelect = true,
            HideSelection = false,
            MultiSelect = multiSelect,
            SmallImageList = _images,
            Margin = Padding.Empty,
        };
        _list.Columns.Add(string.Empty);
        _list.ClientSizeChanged += (_, _) => _list.Columns[0].Width = _list.ClientSize.Width;
        _list.ItemSelectionChanged += OnItemSelectionChanged;
        _list.ItemActivate += (_, _) => Accept();

        _count = new Label { AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SystemColors.GrayText };

        _ok = CreateButton(multiSelect ? "Add" : "OK", Accept);
        var cancel = CreateButton("Cancel", () => { });
        cancel.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(_ok);

        var bottom = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.Controls.Add(_count, 0, 0);
        bottom.Controls.Add(buttons, 1, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_search, 0, 0);
        layout.Controls.Add(_list, 0, 1);
        layout.Controls.Add(bottom, 0, 2);

        AcceptButton = _ok;
        CancelButton = cancel;
        Controls.Add(layout);

        _iconTimer.Tick += (_, _) => LoadSomeIcons();

        ShowMatches();
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>The apps picked, in list order; empty unless the dialog returned OK.</summary>
    public IReadOnlyList<InstalledApp> SelectedApps { get; private set; } = [];

    /// <summary>
    /// Shows the picker and returns the chosen apps, or null if the user cancelled or the
    /// list couldn't be read (which is reported to the user).
    /// </summary>
    public static IReadOnlyList<InstalledApp>? Pick(IWin32Window owner, IconCache icons, bool multiSelect)
    {
        IReadOnlyList<InstalledApp> apps;
        try
        {
            // Reading the list takes a second or two the first time.
            Cursor.Current = Cursors.WaitCursor;
            apps = InstalledApps.Enumerate();
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            MessageBox.Show(owner, ex.Message, "Couldn't list the installed apps", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return null;
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }

        using var picker = new AppPickerForm(apps, icons, multiSelect);
        return picker.ShowDialog(owner) == DialogResult.OK ? picker.SelectedApps : null;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _search.Focus();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _iconTimer.Dispose();
            _images.Dispose();
            _blankImage.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ShowMatches()
    {
        _rebuilding = true;
        _list.BeginUpdate();
        _list.Items.Clear();

        foreach (var app in InstalledApps.Filter(_apps, _search.Text))
        {
            var item = new ListViewItem(app.Name)
            {
                Tag = app,
                ToolTipText = app.Target,
                ImageKey = _images.Images.ContainsKey(app.Target) ? app.Target : NoImageKey,
            };
            _list.Items.Add(item);
            item.Selected = _selected.Contains(app);
        }

        _list.EndUpdate();
        _rebuilding = false;

        UpdateState();
        _iconTimer.Start();
    }

    /// <summary>Loads icons for the apps currently listed, top first; stops once they all have one.</summary>
    private void LoadSomeIcons()
    {
        int loaded = 0;
        foreach (ListViewItem item in _list.Items)
        {
            var app = (InstalledApp)item.Tag!;
            if (!_iconsTried.Add(app.Target))
            {
                continue;
            }

            // Keyed like a menu entry, so the Settings window's cache already has the icon for the
            // menu once the app is added.
            if (_icons.Get(MenuEntry.Shortcut(app.Name, app.Target)) is { } image)
            {
                _images.Images.Add(app.Target, image);
                item.ImageKey = app.Target;
            }

            if (++loaded == IconsPerTick)
            {
                return;
            }
        }

        _iconTimer.Stop();
    }

    private void OnItemSelectionChanged(object? sender, ListViewItemSelectionChangedEventArgs e)
    {
        if (_rebuilding)
        {
            return;
        }

        var app = (InstalledApp)e.Item!.Tag!;
        if (e.IsSelected)
        {
            // With single selection, a pick hidden by the current search must not linger.
            if (!_multiSelect)
            {
                _selected.Clear();
            }

            _selected.Add(app);
        }
        else
        {
            _selected.Remove(app);
        }

        UpdateState();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down && _list.Items.Count > 0)
        {
            var item = _list.SelectedItems.Count > 0 ? _list.SelectedItems[0] : _list.Items[0];
            item.Selected = true;
            item.Focused = true;
            _list.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Takes the selection; with nothing selected, Enter takes the top match, so typing a few
    /// letters and pressing Enter is enough.
    /// </summary>
    private void Accept()
    {
        var picked = _apps.Where(_selected.Contains).ToList();
        if (picked.Count == 0 && _list.Items.Count > 0)
        {
            picked.Add((InstalledApp)_list.Items[0].Tag!);
        }

        if (picked.Count == 0)
        {
            return;
        }

        SelectedApps = picked;
        DialogResult = DialogResult.OK;
    }

    private void UpdateState()
    {
        _ok.Enabled = _selected.Count > 0 || _list.Items.Count > 0;

        int hidden = _selected.Count(app => !_list.Items.Cast<ListViewItem>().Any(item => Equals(item.Tag, app)));
        _count.Text = _multiSelect && _selected.Count > 0
            ? $"{_selected.Count} selected" + (hidden > 0 ? $" ({hidden} not shown)" : string.Empty)
            : _list.Items.Count == 0 ? "No matching apps" : string.Empty;
    }

    private static Button CreateButton(string text, Action onClick)
    {
        // GrowAndShrink: the default GrowOnly keeps the DPI-scaled default size and then gets scaled again.
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(80, 0),
        };
        button.Click += (_, _) => onClick();
        return button;
    }
}
