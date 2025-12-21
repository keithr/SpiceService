using System.Windows.Forms;
using SpiceSharp.Api.Tray.Models;
using SpiceSharp.Api.Tray.Services;

namespace SpiceSharp.Api.Tray;

public partial class IDEConfigurationDialog : Form
{
    private readonly IDEConfigurationInput _input;
    private readonly List<DetectedIDE> _detectedIDEs;
    private bool _isInitializing = true;
    
    public IDEConfigurationDialog(IDEConfigurationInput input)
    {
        _input = input;
        _detectedIDEs = IDEDetector.DetectInstalledIDEs();
        InitializeComponent();
        InitializeDialog();
        _isInitializing = false;
    }
    
    private void InitializeDialog()
    {
        // Set endpoint URL (read-only)
        endpointTextBox.Text = _input.McpEndpointUrl;
        endpointTextBox.ReadOnly = true;
        
        // Add network visibility indicator
        if (_input.McpEndpointUrl.Contains("127.0.0.1") || _input.McpEndpointUrl.Contains("localhost"))
        {
            endpointHelpLabel.Text = "(local only)";
        }
        else
        {
            endpointHelpLabel.Text = "⚠ Network accessible";
            endpointHelpLabel.ForeColor = System.Drawing.Color.Orange;
        }
        
        // Set proxy executable path (read-only)
        proxyTextBox.Text = _input.ProxyExecutablePath;
        proxyTextBox.ReadOnly = true;
        
        // Validate proxy executable exists
        if (!File.Exists(_input.ProxyExecutablePath))
        {
            proxyHelpLabel.Text = "⚠ McpRemote.exe not found - configuration may fail";
            proxyHelpLabel.ForeColor = System.Drawing.Color.Red;
        }
        else
        {
            proxyHelpLabel.Text = "(Bundled with SpiceService - no Node.js required)";
        }
        
        // Populate IDE list
        ideCheckedListBox.Items.Clear();
        foreach (var ide in _detectedIDEs)
        {
            var displayText = ide.Name;
            if (ide.IsInstalled)
            {
                displayText += " (detected)";
            }
            else
            {
                displayText += " (not installed)";
            }
            
            var index = ideCheckedListBox.Items.Add(displayText);
            
            // Set checked state for installed IDEs (all IDEs including VS Code are now auto-configured)
            if (ide.IsInstalled)
            {
                ideCheckedListBox.SetItemChecked(index, ide.IsSelected);
            }
            else
            {
                ideCheckedListBox.SetItemChecked(index, false);
            }
        }
        
        // Set default mode (append)
        appendRadioButton.Checked = true;
        overwriteRadioButton.Checked = false;
        
        // Set default backup option
        backupCheckBox.Checked = true;
        
        // Update Apply button state
        UpdateApplyButtonState();
    }
    
    private void UpdateApplyButtonState()
    {
        var hasSelection = ideCheckedListBox.CheckedIndices.Count > 0;
        applyButton.Enabled = hasSelection;
    }
    
    private void IdeCheckedListBox_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        // Prevent checking items that are not installed
        var ide = _detectedIDEs[e.Index];
        if (!ide.IsInstalled)
        {
            e.NewValue = CheckState.Unchecked;
            return;
        }
        
        // Only update button state if not initializing and handle is created
        if (!_isInitializing && IsHandleCreated)
        {
            // Use BeginInvoke to update after the check state changes
            BeginInvoke(new Action(() => UpdateApplyButtonState()));
        }
        else if (!_isInitializing)
        {
            // Handle not created yet, update directly when form is shown
            Shown += (s, args) => UpdateApplyButtonState();
        }
    }
    
    private void ApplyButton_Click(object? sender, EventArgs e)
    {
        // Get selected IDEs (all checked IDEs, including VS Code which is now auto-configured)
        var selectedIDEs = new List<DetectedIDE>();
        for (int i = 0; i < ideCheckedListBox.Items.Count; i++)
        {
            var ide = _detectedIDEs[i];
            if (ideCheckedListBox.GetItemChecked(i))
            {
                selectedIDEs.Add(ide);
            }
        }
        
        // Show overwrite confirmation if needed
        if (overwriteRadioButton.Checked)
        {
            var message = "You are about to OVERWRITE the configuration files for the following IDEs:\n\n";
            foreach (var ide in selectedIDEs.Where(i => !i.RequiresManualSetup))
            {
                message += $"  • {ide.Name}\n";
            }
            message += "\nThis will REMOVE all other MCP server entries.\n\n";
            if (backupCheckBox.Checked)
            {
                message += "Backup will be created before overwriting.\n\n";
            }
            message += "Continue with overwrite?";
            
            var result = MessageBox.Show(
                message,
                "Confirm Overwrite",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            
            if (result != DialogResult.Yes)
            {
                return;
            }
        }
        
        // Execute configuration
        try
        {
            var configResult = ConfigurationExecutor.ExecuteConfiguration(
                selectedIDEs,
                _input.McpEndpointUrl,
                _input.ProxyExecutablePath,
                appendRadioButton.Checked,
                backupCheckBox.Checked);
            
            // Show success dialog
            using var successDialog = new IDEConfigurationSuccessDialog(configResult);
            successDialog.ShowDialog(this);
            
            // Close this dialog
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"An error occurred during configuration:\n\n{ex.Message}",
                "Configuration Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
    
    private void CancelButton_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }
}

