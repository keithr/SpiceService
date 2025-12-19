using System.Text;
using System.Text.Json;
using SpiceSharp.Api.Web.Services;

namespace SpiceSharp.Api.Tray;

public partial class ExportCircuitDialog : Form
{
    private readonly List<CircuitInfo> _circuits;
    private readonly MCPService _mcpService;
    private ComboBox _circuitComboBox = null!;
    private TextBox _netlistTextBox = null!;
    private Button _saveButton = null!;
    private Button _copyButton = null!;

    public ExportCircuitDialog(List<CircuitInfo> circuits, MCPService mcpService)
    {
        _circuits = circuits;
        _mcpService = mcpService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Export Circuit";
        Size = new Size(700, 500);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(10)
        };

        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Circuit selection
        mainPanel.Controls.Add(new Label { Text = "Circuit:", Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 0);
        _circuitComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        foreach (var circuit in _circuits)
        {
            _circuitComboBox.Items.Add($"{circuit.Id} - {circuit.Description}");
        }
        if (_circuitComboBox.Items.Count > 0)
        {
            _circuitComboBox.SelectedIndex = 0;
        }
        _circuitComboBox.SelectedIndexChanged += async (s, e) => await LoadNetlist();
        mainPanel.Controls.Add(_circuitComboBox, 1, 0);

        // Netlist display
        mainPanel.Controls.Add(new Label { Text = "Netlist:", Anchor = AnchorStyles.Left | AnchorStyles.Top }, 0, 1);
        _netlistTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            ReadOnly = true,
            Font = new Font("Consolas", 9)
        };
        mainPanel.Controls.Add(_netlistTextBox, 1, 1);
        mainPanel.SetColumnSpan(_netlistTextBox, 1);

        // Buttons
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 40
        };
        mainPanel.Controls.Add(buttonPanel, 0, 2);
        mainPanel.SetColumnSpan(buttonPanel, 2);

        _copyButton = new Button
        {
            Text = "Copy to Clipboard",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new Point(10, 5),
            Size = new Size(120, 30)
        };
        _copyButton.Click += CopyButton_Click;

        _saveButton = new Button
        {
            Text = "Save to File...",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left,
            Location = new Point(140, 5),
            Size = new Size(120, 30)
        };
        _saveButton.Click += SaveButton_Click;

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.Cancel,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(580, 5),
            Size = new Size(80, 30)
        };

        buttonPanel.Controls.Add(_copyButton);
        buttonPanel.Controls.Add(_saveButton);
        buttonPanel.Controls.Add(closeButton);

        Controls.Add(mainPanel);

        // Load initial netlist
        if (_circuitComboBox.SelectedIndex >= 0)
        {
            _ = LoadNetlist();
        }
    }

    private async Task LoadNetlist()
    {
        if (_circuitComboBox.SelectedIndex < 0) return;

        var selectedCircuit = _circuits[_circuitComboBox.SelectedIndex];
        _netlistTextBox.Text = "Loading...";

        try
        {
            // Direct service call with arguments
            var arguments = JsonSerializer.SerializeToElement(new { circuit_id = selectedCircuit.Id });
            var result = await _mcpService.ExecuteTool("export_netlist", arguments);
            
            if (result.Content.Count > 0 && result.Content[0].Type == "text")
            {
                _netlistTextBox.Text = result.Content[0].Text;
            }
            else
            {
                _netlistTextBox.Text = "No netlist available";
            }
        }
        catch (Exception ex)
        {
            _netlistTextBox.Text = $"Error: {ex.Message}";
        }
    }

    private void CopyButton_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_netlistTextBox.Text))
        {
            Clipboard.SetText(_netlistTextBox.Text);
            MessageBox.Show("Netlist copied to clipboard.", "Export Circuit",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        if (_circuitComboBox.SelectedIndex < 0) return;

        var selectedCircuit = _circuits[_circuitComboBox.SelectedIndex];
        using var saveDialog = new SaveFileDialog
        {
            Filter = "SPICE Netlist (*.cir)|*.cir|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
            FileName = $"{selectedCircuit.Id}.cir",
            DefaultExt = "cir"
        };

        if (saveDialog.ShowDialog() == DialogResult.OK)
        {
            try
            {
                File.WriteAllText(saveDialog.FileName, _netlistTextBox.Text);
                MessageBox.Show($"Netlist saved to {saveDialog.FileName}", "Export Circuit",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}


