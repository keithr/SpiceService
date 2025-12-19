// Copyright (c) 2025 Keith Rule
// This software is free for personal use. Commercial use requires a commercial license.

namespace SpiceSharp.Api.Tray;

static class Program
{
    private static TrayApplication? _trayApp;
    private static System.Threading.Mutex? _singleInstanceMutex;

    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        
        // Ensure only one instance - keep mutex alive for application lifetime
        _singleInstanceMutex = new System.Threading.Mutex(true, "SpiceServiceTray", out bool isNewInstance);
        if (!isNewInstance)
        {
            MessageBox.Show("SpiceService Tray is already running.", "SpiceService Tray", 
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            _trayApp = new TrayApplication();
            Application.Run(_trayApp);
        }
        catch (Exception ex)
        {
            // Show error message to user if application fails to start
            var errorMessage = $"SpiceService Tray failed to start.\n\n" +
                             $"Error: {ex.Message}\n\n" +
                             $"Type: {ex.GetType().Name}\n\n";
            
            if (ex.InnerException != null)
            {
                errorMessage += $"Inner Exception: {ex.InnerException.Message}\n\n";
            }
            
            // Check if it's a .NET runtime issue
            if (ex is System.IO.FileNotFoundException fileEx && 
                (fileEx.FileName?.Contains("Microsoft.NETCore.App") == true || 
                 fileEx.FileName?.Contains("Microsoft.WindowsDesktop.App") == true ||
                 fileEx.FileName?.Contains("Microsoft.AspNetCore.App") == true))
            {
                errorMessage += "This application requires .NET 8.0 Desktop Runtime.\n\n" +
                              "Please download and install it from:\n" +
                              "https://dotnet.microsoft.com/download/dotnet/8.0\n\n" +
                              "Look for 'Desktop Runtime 8.0.x' for Windows x64.";
            }
            else if (ex.Message.Contains("Could not load file or assembly") || 
                     ex.Message.Contains("The system cannot find the file specified"))
            {
                errorMessage += "This may be due to missing dependencies or .NET runtime.\n\n" +
                              "Please ensure .NET 8.0 Desktop Runtime is installed:\n" +
                              "https://dotnet.microsoft.com/download/dotnet/8.0";
            }
            
            errorMessage += $"\n\nStack Trace:\n{ex.StackTrace}";
            
            MessageBox.Show(errorMessage, "SpiceService Tray - Startup Error", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            // Release mutex when application exits
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
    }
}
