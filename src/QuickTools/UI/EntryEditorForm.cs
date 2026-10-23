using QuickTools.Model;
using QuickTools.Services;

namespace QuickTools.UI;

/// <summary>Edits one shortcut or folder in place; the caller passes a copy and keeps it only on OK.</summary>
internal sealed class EntryEditorForm : Form
{
    private const int TextBoxWidth = 340;

    private readonly MenuEntry _entry;
    private readonly bool _isFolder;
    private readonly IconCache _icons;
    private readonly TextBox _name = CreateTextBox();
    private readonly TextBox _target = CreateTextBox();
    private readonly TextBox _arguments = CreateTextBox();
    private readonly TextBox _workingDirectory = CreateTextBox();
    private readonly TextBox _iconPath = CreateTextBox();
    private readonly CheckBox _runAsAdmin = new() { Text = "Run as administrator", AutoSize = true };
    private readonly TableLayoutPanel _grid;

    public EntryEditorForm(MenuEntry entry, bool isNew, IconCache icons)
    {
        _entry = entry;
        _isFolder = entry.Kind == EntryKind.Folder;
        _icons = icons;

        SuspendLayout();
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Text = (isNew ? "New " : "Edit ") + (_isFolder ? "folder" : "shortcut");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(10);

        _grid = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, Dock = DockStyle.Fill };
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _name.Text = entry.Name;
        AddRow("&Name:", _name);
        AddRow(string.Empty, new Label
        {
            Text = "Put & before a letter to make it the menu's keyboard key (&& for a literal &).",
            UseMnemonic = false,
            AutoSize = true,

            // Wrap at the text box width rather than widening the column.
            MaximumSize = new Size(TextBoxWidth, 0),
            ForeColor = SystemColors.GrayText,
        });

        if (!_isFolder)
        {
            _target.Text = entry.Target;
            _arguments.Text = entry.Arguments;
            _workingDirectory.Text = entry.WorkingDirectory;
            _iconPath.Text = entry.IconPath;
            _runAsAdmin.Checked = entry.RunAsAdmin;

            AddRow("&Target:", _target,
                CreateButton("File…", BrowseTargetFile),
                CreateButton("Folder…", BrowseTargetFolder),
                CreateButton("App…", BrowseTargetApp));
            AddRow(string.Empty, new Label
            {
                Text = "A folder as the target becomes a submenu listing its contents. To just open a folder, " +
                    "use explorer.exe as the target and the folder as the argument.",
                UseMnemonic = false,
                AutoSize = true,
                MaximumSize = new Size(TextBoxWidth, 0),
                ForeColor = SystemColors.GrayText,
            });
            AddRow("&Arguments:", _arguments);
            AddRow("&Start in:", _workingDirectory, CreateButton("Browse…", BrowseWorkingDirectory));
            AddRow("&Icon:", _iconPath, CreateButton("Browse…", BrowseIcon));
            AddRow(string.Empty, _runAsAdmin);
        }

        var ok = CreateButton("OK", Accept);
        ok.MinimumSize = new Size(80, 0);
        var cancel = CreateButton("Cancel", () => { });
        cancel.MinimumSize = new Size(80, 0);
        cancel.DialogResult = DialogResult.Cancel;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0),
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        _grid.Controls.Add(buttons, 0, _grid.RowCount);
        _grid.SetColumnSpan(buttons, 3);

        AcceptButton = ok;
        CancelButton = cancel;
        Controls.Add(_grid);
        ResumeLayout(false);
        PerformLayout();
    }

    private void AddRow(string label, Control field, params Control[] buttons)
    {
        int row = _grid.RowCount++;
        _grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        if (label.Length > 0)
        {
            _grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        }

        _grid.Controls.Add(field, 1, row);

        if (buttons.Length > 0)
        {
            var panel = new FlowLayoutPanel
            {
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                WrapContents = false,
                Margin = Padding.Empty,
            };
            panel.Controls.AddRange(buttons);
            _grid.Controls.Add(panel, 2, row);
        }
    }

    private void Accept()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            ShowValidationError("Enter a name.", _name);
            return;
        }

        if (!_isFolder && string.IsNullOrWhiteSpace(_target.Text))
        {
            ShowValidationError("Enter a target: a program, file, folder or URL.", _target);
            return;
        }

        _entry.Name = _name.Text.Trim();
        if (!_isFolder)
        {
            _entry.Target = _target.Text.Trim();
            _entry.Arguments = NullIfBlank(_arguments.Text);
            _entry.WorkingDirectory = NullIfBlank(_workingDirectory.Text);
            _entry.IconPath = NullIfBlank(_iconPath.Text);
            _entry.RunAsAdmin = _runAsAdmin.Checked;
        }

        DialogResult = DialogResult.OK;
    }

    private void BrowseTargetFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a program or file",
            Filter = "Programs and shortcuts|*.exe;*.lnk;*.bat;*.cmd;*.ps1;*.msc|All files|*.*",
            DereferenceLinks = false,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetTarget(dialog.FileName, Path.GetFileNameWithoutExtension(dialog.FileName));
        }
    }

    private void BrowseTargetFolder()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose a folder to open" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            SetTarget(dialog.SelectedPath, new DirectoryInfo(dialog.SelectedPath).Name);
        }
    }

    private void BrowseTargetApp()
    {
        if (AppPickerForm.Pick(this, _icons, multiSelect: false) is [var app])
        {
            SetTarget(app.Target, app.MenuName);
        }
    }

    private void BrowseWorkingDirectory()
    {
        using var dialog = new FolderBrowserDialog { Description = "Choose the folder the program starts in" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _workingDirectory.Text = dialog.SelectedPath;
        }
    }

    private void BrowseIcon()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose an icon",
            Filter = "Icons and programs|*.ico;*.exe|All files|*.*",
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _iconPath.Text = dialog.FileName;
        }
    }

    private void SetTarget(string path, string suggestedName)
    {
        _target.Text = path;
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            _name.Text = suggestedName;
        }
    }

    private void ShowValidationError(string message, Control field)
    {
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        field.Focus();
    }

    private static string? NullIfBlank(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // Anchored left only: a Left+Right anchor in an AutoSize column makes the column and box keep widening each other.
    private static TextBox CreateTextBox() => new() { Width = TextBoxWidth, Anchor = AnchorStyles.Left };

    private static Button CreateButton(string text, Action onClick)
    {
        // GrowAndShrink: the default GrowOnly keeps the DPI-scaled default size and then gets scaled again.
        var button = new Button { Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        button.Click += (_, _) => onClick();
        return button;
    }
}
