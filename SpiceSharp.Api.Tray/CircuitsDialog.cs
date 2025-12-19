using System.Text.Json;
using SpiceSharp.Api.Web.Services;

namespace SpiceSharp.Api.Tray;

public partial class CircuitsDialog : Form
{
    private readonly List<CircuitInfo> _circuits;
    private readonly MCPService _mcpService;
    private ListView _listView = null!;

    public CircuitsDialog(List<CircuitInfo> circuits, MCPService mcpService)
    {
        _circuits = circuits;
        _mcpService = mcpService;
        InitializeComponent();
        LoadCircuits();
    }

    private void InitializeComponent()
    {
        Text = "Circuits";
        Size = new Size(600, 400);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _listView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true
        };

        _listView.Columns.Add("Circuit ID", 200);
        _listView.Columns.Add("Description", 300);
        _listView.Columns.Add("Active", 80);

        var panel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50
        };

        var closeButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(500, 10),
            Size = new Size(80, 30)
        };

        var refreshButton = new Button
        {
            Text = "Refresh",
            Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
            Location = new Point(410, 10),
            Size = new Size(80, 30)
        };
        refreshButton.Click += async (s, e) => await RefreshCircuits();

        panel.Controls.Add(closeButton);
        panel.Controls.Add(refreshButton);

        Controls.Add(_listView);
        Controls.Add(panel);
    }

    private void LoadCircuits()
    {
        _listView.Items.Clear();
        foreach (var circuit in _circuits)
        {
            var item = new ListViewItem(circuit.Id);
            item.SubItems.Add(circuit.Description);
            item.SubItems.Add(circuit.IsActive ? "Yes" : "No");
            item.Tag = circuit;
            _listView.Items.Add(item);
        }
    }

    private async Task RefreshCircuits()
    {
        try
        {
            // Direct service call
            var result = await _mcpService.ExecuteTool("list_circuits", default);
            var circuits = ParseCircuitsFromMCPResult(result);
            
            if (circuits != null)
            {
                _circuits.Clear();
                _circuits.AddRange(circuits);
                LoadCircuits();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to refresh circuits: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private List<CircuitInfo> ParseCircuitsFromMCPResult(MCPToolResult result)
    {
        // Extract JSON from MCP result and deserialize
        if (result.Content.Count > 0 && result.Content[0].Type == "text")
        {
            var jsonText = result.Content[0].Text;
            var jsonDoc = JsonDocument.Parse(jsonText);
            var circuits = new List<CircuitInfo>();
            
            foreach (var circuitElement in jsonDoc.RootElement.EnumerateArray())
            {
                circuits.Add(new CircuitInfo
                {
                    Id = circuitElement.GetProperty("id").GetString() ?? "",
                    Description = circuitElement.GetProperty("description").GetString() ?? "",
                    IsActive = circuitElement.GetProperty("is_active").GetBoolean()
                });
            }
            
            return circuits;
        }
        
        return new List<CircuitInfo>();
    }
}

