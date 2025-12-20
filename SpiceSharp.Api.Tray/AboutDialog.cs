using System.Reflection;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace SpiceSharp.Api.Tray;

public partial class AboutDialog : Form
{
    public AboutDialog(string? endpointUrl = null)
    {
        InitializeComponent();
        LoadIcon();
        LoadVersion();
        LoadEndpointUrl(endpointUrl);
    }

    private void LoadIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = new[]
            {
                "SpiceSharp.Api.Tray.Resources.spice_100x100.png",
                "SpiceSharp.Api.Tray.resources.spice_100x100.png",
                "resources.spice_100x100.png",
                "spice_100x100.png"
            };
            
            foreach (var resourceName in resourceNames)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    iconPictureBox.Image = new Bitmap(stream);
                    break;
                }
            }
            
            if (iconPictureBox.Image == null)
            {
                var allResources = assembly.GetManifestResourceNames();
                var iconResource = allResources.FirstOrDefault(r => r.Contains("spice_100x100") || r.Contains("spice"));
                if (iconResource != null)
                {
                    using var stream = assembly.GetManifestResourceStream(iconResource);
                    if (stream != null)
                    {
                        iconPictureBox.Image = new Bitmap(stream);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load icon image: {ex.Message}");
        }
    }

    private void LoadVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        versionLabel.Text = version != null 
            ? $"Version {version.Major}.{version.Minor}.{version.Build}.{version.Revision}"
            : "Version Unknown";
    }
    
    private void LoadEndpointUrl(string? endpointUrl)
    {
        if (!string.IsNullOrEmpty(endpointUrl))
        {
            urlLabel.Text = $"MCP Endpoint: {endpointUrl}";
            urlLabel.Visible = true;
        }
        else
        {
            urlLabel.Visible = false;
        }
    }
    
    private Icon CreateDialogIcon()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            var resourceNames = new[]
            {
                "SpiceSharp.Api.Tray.Resources.spice_100x100.png",
                "SpiceSharp.Api.Tray.resources.spice_100x100.png",
                "resources.spice_100x100.png",
                "spice_100x100.png"
            };
            
            foreach (var resourceName in resourceNames)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var bitmap = new Bitmap(stream);
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
            
            var allResources = assembly.GetManifestResourceNames();
            var iconResource = allResources.FirstOrDefault(r => r.Contains("spice_100x100") || r.Contains("spice"));
            if (iconResource != null)
            {
                using var stream = assembly.GetManifestResourceStream(iconResource);
                if (stream != null)
                {
                    using var bitmap = new Bitmap(stream);
                    return Icon.FromHandle(bitmap.GetHicon());
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load embedded icon: {ex.Message}");
        }
        
        return SystemIcons.Application;
    }
}
