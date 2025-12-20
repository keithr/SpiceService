using System.Windows.Forms;
using SpiceSharp.Api.Tray.Models;

namespace SpiceSharp.Api.Tray;

public partial class IDEConfigurationSuccessDialog : Form
{
    private readonly ConfigurationResult _result;
    
    public IDEConfigurationSuccessDialog(ConfigurationResult result)
    {
        _result = result;
        InitializeComponent();
        InitializeDialog();
    }
    
    private void InitializeDialog()
    {
        var hasErrors = _result.Errors.Count > 0;
        var hasSuccess = _result.ConfiguredIDEs.Count > 0 || _result.VSCodeConfigJson != null;
        
        if (hasErrors && hasSuccess)
        {
            this.Text = "Configuration Partially Successful";
            titleLabel.Text = "Configuration Partially Successful";
        }
        else if (hasErrors)
        {
            this.Text = "Configuration Failed";
            titleLabel.Text = "Configuration Failed";
        }
        else
        {
            this.Text = "Configuration Successful";
            titleLabel.Text = "✓ Configuration Successful";
        }
        
        // Build success message
        var message = new System.Text.StringBuilder();
        
        if (_result.ConfiguredIDEs.Count > 0)
        {
            message.AppendLine("✓ Successfully configured the following IDEs:");
            message.AppendLine();
            foreach (var ide in _result.ConfiguredIDEs)
            {
                message.AppendLine($"  • {ide}");
            }
            message.AppendLine();
        }
        
        // VS Code section
        if (_result.VSCodeConfigJson != null)
        {
            message.AppendLine("VS Code Configuration:");
            message.AppendLine("VS Code uses workspace-level configs. Add this to your workspace's .vscode/mcp.json file:");
            
            // Set VS Code JSON in text box
            vsCodeTextBox.Text = _result.VSCodeConfigJson;
            vsCodeTextBox.Visible = true;
            copyButton.Visible = true;
            vsCodeLabel.Visible = true;
            
            // Adjust message label height to make room for VS Code section
            messageLabel.Height = 180;
            vsCodeLabel.Location = new System.Drawing.Point(0, 185);
            vsCodeTextBox.Location = new System.Drawing.Point(0, 205);
        }
        else
        {
            vsCodeTextBox.Visible = false;
            copyButton.Visible = false;
            vsCodeLabel.Visible = false;
        }
        
        // Errors section
        if (_result.Errors.Count > 0)
        {
            message.AppendLine();
            message.AppendLine("✗ Configuration failed:");
            foreach (var error in _result.Errors)
            {
                message.AppendLine($"  • {error}");
            }
            message.AppendLine();
        }
        
        // Next steps
        if (_result.ConfiguredIDEs.Count > 0)
        {
            message.AppendLine("Next Steps:");
            message.AppendLine("1. Restart configured IDEs to load new settings");
            message.AppendLine("2. Look for SpiceService tools in MCP/Agent UI");
            message.AppendLine("3. Verify connection at the configured endpoint");
            message.AppendLine();
        }
        
        // Backups
        if (_result.BackupPaths.Count > 0)
        {
            message.AppendLine("Backups created at:");
            foreach (var backup in _result.BackupPaths)
            {
                message.AppendLine($"  {backup}");
            }
        }
        
        messageLabel.Text = message.ToString();
    }
    
    private void CopyButton_Click(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(vsCodeTextBox.Text))
        {
            Clipboard.SetText(vsCodeTextBox.Text);
            MessageBox.Show(
                "Configuration copied to clipboard!",
                "Success",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }
    
    private void OkButton_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.OK;
        Close();
    }
}

