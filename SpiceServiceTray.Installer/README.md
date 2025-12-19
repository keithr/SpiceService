# SpiceService Tray MSI Installer

This directory contains the WiX-based MSI installer for the SpiceService Tray Application.

## Quick Build

1. **Ensure WiX Toolset is installed:**
   ```powershell
   winget install WiXToolset.WiXToolset
   ```
   Or download from: https://wixtoolset.org/releases/

2. **Build the tray application:**
   ```powershell
   dotnet build ..\SpiceSharp.Api.Tray\SpiceSharp.Api.Tray.csproj -c Release
   ```

3. **Build the installer:**
   ```powershell
   .\build-installer.ps1
   ```

The MSI will be created at: `bin\Release\SpiceServiceTray.msi`

## What the Installer Does

- **Installs to:** `C:\Program Files\SpiceService\Tray\`
- **Creates Start Menu shortcuts** in `SpiceService` folder
- **Includes all dependencies** (DLLs, config files, etc.)
- **Version:** Automatically reads from `SpiceServiceTray.exe` file version
- **Uninstall:** Available via Windows Settings or Start Menu shortcut

## Installation

1. Right-click `SpiceServiceTray.msi`
2. Select "Install" (requires administrator privileges)
3. Follow the installation wizard

## Uninstallation

- Use Windows Settings > Apps > SpiceService Tray Application > Uninstall
- Or use the Start Menu shortcut "Uninstall SpiceService Tray"

## Features

- ✅ Per-machine installation (available to all users)
- ✅ Automatic version detection from executable
- ✅ Start Menu shortcuts
- ✅ Clean uninstall
- ✅ Major upgrade support (can upgrade from previous versions)

## Troubleshooting

### WiX Toolset not found
- Verify installation: Check `C:\Program Files (x86)\WiX Toolset v3.11\bin\`
- Restart terminal after installing WiX
- Ensure WiX build tools are installed, not just the Visual Studio extension

### Tray application must be built first
- Build the tray app: `dotnet build ..\SpiceSharp.Api.Tray\SpiceSharp.Api.Tray.csproj -c Release`
- Verify output exists: `..\SpiceSharp.Api.Tray\bin\Release\net8.0-windows\`

### Build errors with Heat
- Ensure WiX Toolset is properly installed
- Check that the tray app build output directory exists
- Verify you have write permissions in the installer directory
