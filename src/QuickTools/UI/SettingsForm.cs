using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.UI;

internal sealed class SettingsForm : Form
{
    private readonly Func<AppSettings, bool?, bool> _apply;
    private readonly bool _startupInitially;
    private readonly IconCache _icons = new();
    private readonly MenuEditor _leftEditor;
    private readonly MenuEditor _rightEditor;
    private readonly TabPage _rightTab = new();
    private readonly TableLayoutPanel _layout;
    private readonly TableLayoutPanel _bottom;
    private readonly CheckBox _mirror = new() { Text = "&Right-click menu mirrors the left-click menu", AutoSize = true };
    private readonly CheckBox _startup = new() { Text = "Start QuickTools &when I sign in to Windows", AutoSize = true };

    /// <param name="apply">
    /// Saves new settings; receives the new startup setting only if the user changed it.
    /// Returns false if saving failed, in which case the window stays open.
    /// </param>
    public SettingsForm(AppSettings settings, bool startWithWindows, Launcher launcher, Icon icon, Func<AppSettings, bool?, bool> apply)
    {
        _apply = apply;
        _startupInitially = startWithWindows;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = "QuickTools Settings";
        Icon = icon;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(430, 490);

        // QuickTools lives only in the tray. Without a taskbar button, minimizing would leave a stray
        // title bar above the taskbar, so there is no minimize button either.
        ShowInTaskbar = false;
        MinimizeBox = false;

        _leftEditor = new MenuEditor(_icons, launcher) { Dock = DockStyle.Fill };
        _rightEditor = new MenuEditor(_icons, launcher) { Dock = DockStyle.Fill };
        _leftEditor.LoadEntries(settings.LeftMenu);
        _rightEditor.LoadEntries(settings.RightMenu);

        var leftTab = new TabPage("Left-click menu") { Padding = new Padding(4) };
        leftTab.Controls.Add(_leftEditor);
        _rightTab.Padding = new Padding(4);
        _rightTab.Controls.Add(_rightEditor);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(leftTab);
        tabs.TabPages.Add(_rightTab);

        _mirror.Checked = settings.MirrorMenus;
        _mirror.CheckedChanged += (_, _) => UpdateMirrorState();
        _startup.Checked = startWithWindows;

        var copy = CreateButton("&Copy left → right", 0, CopyLeftToRight);
        copy.Anchor = AnchorStyles.Left;
        // OK saves and closes, as in standard Windows settings dialogs.
        var ok = CreateButton("OK", 80, Save);
        var cancel = CreateButton("Cancel", 80, Close);
        CancelButton = cancel;

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = Padding.Empty,
        };
        actions.Controls.Add(cancel);
        actions.Controls.Add(ok);

        _bottom = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Dock = DockStyle.Fill, Margin = Padding.Empty };
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.Controls.Add(copy, 0, 0);
        _bottom.Controls.Add(actions, 1, 0);

        _layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(8) };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _layout.Controls.Add(_mirror, 0, 0);
        _layout.Controls.Add(_startup, 0, 1);
        _layout.Controls.Add(tabs, 0, 2);
        _layout.Controls.Add(_bottom, 0, 3);
        Controls.Add(_layout);

        UpdateMirrorState();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        FitToContent();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);

        // Child controls finish rescaling after this event, so measure once they have.
        BeginInvoke(FitToContent);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _icons.Dispose();
    }

    /// <summary>
    /// Derives MinimumSize from what the content actually needs at the current DPI (a fixed pixel
    /// minimum is wrong at any scaling but one), and grows the window if it is currently smaller.
    /// </summary>
    private void FitToContent()
    {
        PerformLayout();

        // Only the editor stretches when the window resizes, so the window can shrink by exactly
        // the editor's spare room.
        Size editorNeeds = _leftEditor.GetMinimumContentSize();
        int minWidth = Width - (_leftEditor.Width - editorNeeds.Width);
        int minHeight = Height - (_leftEditor.Height - editorNeeds.Height);

        // The checkboxes and the bottom button row must also fit side to side.
        int otherWidth = new[]
        {
            _mirror.PreferredSize.Width + _mirror.Margin.Horizontal,
            _startup.PreferredSize.Width + _startup.Margin.Horizontal,
            // Summed per child: a TableLayoutPanel with a percent column under-reports its preferred width.
            _bottom.Controls.Cast<Control>().Sum(c => c.PreferredSize.Width + c.Margin.Horizontal) + _bottom.Margin.Horizontal,
        }.Max() + _layout.Padding.Horizontal;
        minWidth = Math.Max(minWidth, SizeFromClientSize(new Size(otherWidth, 0)).Width);

        // Never larger than the screen: on small or heavily scaled displays the button column scrolls instead.
        Size workArea = Screen.FromControl(this).WorkingArea.Size;
        MinimumSize = new Size(Math.Min(minWidth, workArea.Width), Math.Min(minHeight, workArea.Height));
        Size = new Size(
            Math.Min(Math.Max(Width, MinimumSize.Width), workArea.Width),
            Math.Min(Math.Max(Height, MinimumSize.Height), workArea.Height));
    }

    private static Button CreateButton(string text, int minimumWidth, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(minimumWidth, 0),
        };
        button.Click += (_, _) => onClick();
        return button;
    }

    private void UpdateMirrorState()
    {
        _rightEditor.Enabled = !_mirror.Checked;
        _rightTab.Text = _mirror.Checked ? "Right-click menu (mirrored)" : "Right-click menu";
    }

    private void CopyLeftToRight()
    {
        if (_rightEditor.GetEntries().Count > 0 &&
            MessageBox.Show(this, "Replace the right-click menu with a copy of the left-click menu?", Text,
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        _rightEditor.LoadEntries(_leftEditor.GetEntries());
    }

    private void Save()
    {
        var settings = new AppSettings
        {
            MirrorMenus = _mirror.Checked,
            LeftMenu = _leftEditor.GetEntries(),
            RightMenu = _rightEditor.GetEntries(),
        };

        // Only touch the Run key if the user changed the setting.
        bool? startup = _startup.Checked != _startupInitially ? _startup.Checked : null;

        if (_apply(settings, startup))
        {
            Close();
        }
    }
}
