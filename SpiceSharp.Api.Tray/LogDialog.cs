using SpiceSharp.Api.Tray.Services;
using System.Text;

namespace SpiceSharp.Api.Tray;

public partial class LogDialog : Form
{
    private readonly CircularLogBuffer _logBuffer;
    private TextBox _logTextBox = null!;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private int _lastEntryCount = 0;

    public LogDialog(CircularLogBuffer logBuffer)
    {
        _logBuffer = logBuffer;
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 500 };
        InitializeComponent();
        
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();
        
        RefreshLogDisplay();
    }

    private void InitializeComponent()
    {
        Text = "Application Logs";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;
        MinimizeBox = true;
        MaximizeBox = true;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };

        // Toolbar
        var toolbarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 40
        };

        var clearButton = new Button
        {
            Text = "Clear",
            Size = new Size(80, 30),
            Location = new Point(10, 5)
        };
        clearButton.Click += (s, e) =>
        {
            _logBuffer.Clear();
            RefreshLogDisplay();
        };
        toolbarPanel.Controls.Add(clearButton);

        var copyButton = new Button
        {
            Text = "Copy All",
            Size = new Size(80, 30),
            Location = new Point(100, 5)
        };
        copyButton.Click += (s, e) =>
        {
            if (!string.IsNullOrEmpty(_logTextBox.Text))
            {
                Clipboard.SetText(_logTextBox.Text);
                MessageBox.Show("Logs copied to clipboard.", "Copy", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        };
        toolbarPanel.Controls.Add(copyButton);

        var autoScrollCheckbox = new CheckBox
        {
            Text = "Auto-scroll",
            Checked = true,
            AutoSize = true,
            Location = new Point(200, 8)
        };
        toolbarPanel.Controls.Add(autoScrollCheckbox);

        var entryCountLabel = new Label
        {
            Text = $"Entries: {_logBuffer.Count}",
            AutoSize = true,
            Location = new Point(300, 10)
        };
        toolbarPanel.Controls.Add(entryCountLabel);

        mainPanel.Controls.Add(toolbarPanel, 0, 0);

        // Log text box
        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9),
            BackColor = Color.Black,
            ForeColor = Color.LimeGreen
        };
        mainPanel.Controls.Add(_logTextBox, 0, 1);

        Controls.Add(mainPanel);

        // Store references for use in refresh
        var tagData = new LogDialogTagData
        {
            AutoScroll = autoScrollCheckbox,
            EntryCountLabel = entryCountLabel
        };
        _logTextBox.Tag = tagData;
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_logBuffer.Count != _lastEntryCount)
        {
            RefreshLogDisplay();
        }
    }

    private void RefreshLogDisplay()
    {
        var entries = _logBuffer.GetEntries().ToList();
        _lastEntryCount = entries.Count;

        var tagData = _logTextBox.Tag as LogDialogTagData;
        var autoScroll = tagData?.AutoScroll?.Checked ?? true;
        var entryCountLabel = tagData?.EntryCountLabel;

        if (entryCountLabel != null)
        {
            entryCountLabel.Text = $"Entries: {_lastEntryCount}";
        }

        // Save scroll position if not auto-scrolling
        int scrollPosition = 0;
        if (!autoScroll && _logTextBox.Lines.Length > 0)
        {
            scrollPosition = _logTextBox.GetCharIndexFromPosition(new Point(0, 0));
        }

        var sb = new StringBuilder();
        foreach (var entry in entries)
        {
            sb.AppendLine(entry.ToString());
        }

        _logTextBox.Text = sb.ToString();

        // Restore scroll position or scroll to bottom
        if (autoScroll)
        {
            _logTextBox.SelectionStart = _logTextBox.Text.Length;
            _logTextBox.ScrollToCaret();
        }
        else if (scrollPosition < _logTextBox.Text.Length)
        {
            _logTextBox.SelectionStart = scrollPosition;
            _logTextBox.ScrollToCaret();
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _refreshTimer.Stop();
        _refreshTimer.Dispose();
        base.OnFormClosing(e);
    }
}

internal class LogDialogTagData
{
    public CheckBox? AutoScroll { get; set; }
    public Label? EntryCountLabel { get; set; }
}

