# Quick Build Instructions

## Prerequisites

1. **Install WiX Toolset v3.11 or later**
   ```powershell
   winget install WiXToolset.WiXToolset
   ```
   After installation, restart your terminal/PowerShell session.

2. **Build the Tray Application** (if not already built)
   ```powershell
   dotnet build SpiceSharp.Api.Tray\SpiceSharp.Api.Tray.csproj -c Release
   ```

## Building the Installer

### Option 1: Using the Build Script (Recommended)
```powershell
cd SpiceServiceTray.Installer
.\build-installer.ps1
```

### Option 2: Using Visual Studio
1. Open `SpiceService.sln` in Visual Studio
2. Right-click `SpiceServiceTray.Installer` project
3. Select "Build" (Release configuration)
4. MSI will be in `bin\Release\SpiceServiceTray.msi`

### Option 3: Using MSBuild
```powershell
msbuild SpiceServiceTray.Installer\SpiceServiceTray.Installer.wixproj /p:Configuration=Release /p:Platform=x64 /p:SolutionDir="$PWD\"
```

## Output

The MSI installer will be created at:
```
dist\SpiceServiceTray.msi
```

For Release builds, the MSI is placed in the `dist` directory at the solution root. Debug builds still use `bin\Debug\SpiceServiceTray.msi`.

## Installing

1. Right-click the MSI file
2. Select "Install" (requires administrator privileges)
3. Follow the installation wizard
4. The application will be installed to `C:\Program Files\SpiceService\Tray\`
5. Start Menu shortcuts will be created automatically

## Uninstalling

- Use Windows Settings > Apps > SpiceService Tray Application > Uninstall
- Or use the Start Menu shortcut "Uninstall SpiceService Tray"

## Troubleshooting

### "WiX Toolset not found"
- Verify installation: Check `C:\Program Files (x86)\WiX Toolset v3.11\bin\`
- Restart terminal after installing WiX
- Ensure WiX is installed, not just the Visual Studio extension

### "Tray application must be built first"
- Build the tray app: `dotnet build SpiceSharp.Api.Tray\SpiceSharp.Api.Tray.csproj -c Release`
- Verify output exists: `SpiceSharp.Api.Tray\bin\Release\net8.0-windows\`

### Build errors with Heat
- Ensure WiX Toolset is properly installed
- Check that the tray app build output directory exists
- Verify you have write permissions in the installer directory

