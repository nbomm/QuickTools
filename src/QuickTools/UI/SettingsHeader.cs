using System.ComponentModel;
using System.Diagnostics;

namespace QuickTools.UI;

/// <summary>
/// The app's icon beside its name and version, copyright and project link, at the top of the
/// Settings window. The icon is a square as tall as the text and is redrawn at its exact pixel size,
/// so it stays sharp at any display scaling.
/// </summary>
internal sealed class SettingsHeader : TableLayoutPanel
{
    private const string ProjectUrl = "https://github.com/nbomm/QuickTools";

    private readonly PictureBox _icon = new() { SizeMode = PictureBoxSizeMode.Normal, Anchor = AnchorStyles.Left };
    private readonly FlowLayoutPanel _text;
    private readonly Label _title;

    public SettingsHeader()
    {
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 2;
        RowCount = 1;
        ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _title = new Label { Text = $"QuickTools v{AppVersion.Number}", AutoSize = true, Margin = Padding.Empty };

        var link = new LinkLabel
        {
            Text = ProjectUrl["https://".Length..],
            AutoSize = true,
            Margin = Padding.Empty,
        };
        link.LinkClicked += (_, _) => OpenProjectPage();

        _text = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(8, 0, 0, 0),
        };
        _text.Controls.Add(_title);
        if (AppVersion.Copyright is { Length: > 0 } copyright)
        {
            _text.Controls.Add(new Label { Text = copyright, AutoSize = true, Margin = Padding.Empty, UseMnemonic = false });
        }

        _text.Controls.Add(link);

        Controls.Add(_icon, 0, 0);
        Controls.Add(_text, 1, 0);

        _icon.SizeChanged += (_, _) => RedrawIcon();
        UpdateTitleFont();
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);

        // The window's font changes when it moves to a monitor with different scaling; follow it.
        UpdateTitleFont();
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        // Setting AutoSize in the constructor lays out before the text panel exists.
        if (_text is null)
        {
            base.OnLayout(levent);
            return;
        }

        // A square as tall as the three lines of text, like the icon in a classic About box.
        int side = _text.GetPreferredSize(Size.Empty).Height;
        if (side > 0 && _icon.Size != new Size(side, side))
        {
            _icon.Size = new Size(side, side);
        }

        base.OnLayout(levent);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _icon.Image?.Dispose();
            _title.Font.Dispose();
        }

        base.Dispose(disposing);
    }

    private void UpdateTitleFont()
    {
        var old = _title.Font;
        _title.Font = new Font(Font.FontFamily, Font.SizeInPoints * 1.35f, FontStyle.Bold, GraphicsUnit.Point);
        if (old != Font)
        {
            old.Dispose();
        }
    }

    private void RedrawIcon()
    {
        if (_icon.Width <= 0)
        {
            return;
        }

        var old = _icon.Image;
        _icon.Image = AppIcon.Draw(_icon.Width);
        old?.Dispose();
    }

    private void OpenProjectPage()
    {
        try
        {
            using var _ = Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            MessageBox.Show(this, $"Couldn't open {ProjectUrl}.\n\n{ex.Message}", "QuickTools",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
