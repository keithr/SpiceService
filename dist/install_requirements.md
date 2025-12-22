# SpiceService Tray MSI Installer - Requirements

This document outlines the requirements for building and installing the SpiceService Tray MSI installer.

---

## Building the MSI

### Prerequisites

1. **.NET SDK 8.0 or later**
   - Download from: https://dotnet.microsoft.com/download
   - Verify installation: `dotnet --version` (should show 8.0 or later)

2. **WiX Toolset v3.11 or later**
   - Download from: https://wixtoolset.org/releases/
   - Or install via: `winget install WiXToolset.WiXToolset`
   - Verify installation: Check for `C:\Program Files (x86)\WiX Toolset v3.14\bin\candle.exe`

3. **Visual Studio Build Tools (Optional)**
   - Required only if you want to use MSBuild instead of standalone WiX tools
   - The build script will automatically fall back to standalone WiX tools if MSBuild is not found
   - Download from: https://visualstudio.microsoft.com/downloads/

### Build Process

1. **Navigate to installer directory:**
   ```powershell
   cd SpiceServiceTray.Installer
   ```

2. **Run the build script:**
   ```powershell
   .\build-installer.ps1
   ```

3. **The build script will:**
   - Build and publish `McpRemote.exe` to `bin\Release\net8.0\win-x64\publish\`
   - Build the tray application to `bin\Release\net8.0-windows\`
   - Harvest application files (excluding manually defined exe/dll/McpRemote)
   - Harvest library files from the `libraries` directory
   - Compile WiX source files (`Product.wxs`, `HarvestedFiles.wxs`, `HarvestedLibraries.wxs`)
   - Link the MSI package

4. **Output:**
   - MSI file: `dist\SpiceServiceTray.msi`
   - Size: ~38-40 MB (includes application + 500+ SPICE library files)

### Build Script Behavior

- **If MSBuild is found:** Uses MSBuild to build the WiX project (`.wixproj`)
- **If MSBuild is not found:** Automatically falls back to standalone WiX tools (`candle.exe`, `light.exe`, `heat.exe`)
- Both methods produce identical MSI files

### Build Configuration

- **Default Configuration:** `Release`
- **Platform:** `x64`
- **Install Scope:** `perUser` (no administrator privileges required)

---

## Installing the MSI

### Prerequisites

1. **Windows 10 or later**
   - Windows 7/8 may work but are not officially supported

2. **.NET 8.0 Desktop Runtime**
   - Required for the tray application to run
   - Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   - Select "Desktop Runtime" for your architecture (x64)

3. **User Account**
   - No administrator privileges required (per-user installation)
   - Installation occurs in user's `%LocalAppData%\SpiceService\Tray`

### Installation Process

1. **Double-click `SpiceServiceTray.msi`**
   - Or right-click → "Install"

2. **Follow the installation wizard:**
   - Accept license terms
   - Choose installation directory (default: `%LocalAppData%\SpiceService\Tray`)
   - Click "Install"

3. **After installation:**
   - Application launches automatically (if CustomAction is enabled)
   - Start Menu shortcut created: `SpiceService\SpiceService Tray`
   - Desktop shortcut created (optional)
   - Startup shortcut created (optional, for auto-start on login)

### Installation Location

- **Application:** `%LocalAppData%\SpiceService\Tray\`
- **Libraries:** `%LocalAppData%\SpiceService\Tray\libraries\`
- **Shortcuts:** User-specific Start Menu, Desktop, and Startup folders

### Uninstallation

1. **Via Control Panel:**
   - Settings → Apps → Installed apps
   - Find "SpiceService Tray Application"
   - Click "Uninstall"

2. **Via Start Menu:**
   - Start Menu → SpiceService → "Uninstall SpiceService Tray"

3. **Manual cleanup (if needed):**
   - Delete: `%LocalAppData%\SpiceService\Tray\`
   - Remove shortcuts from Start Menu, Desktop, and Startup folders

---

## Troubleshooting

### Build Issues

**Problem:** `MSBuild not found!`
- **Solution:** This is expected if Visual Studio Build Tools are not installed. The build script will automatically use standalone WiX tools instead. No action needed.

**Problem:** `WiX Toolset not found!`
- **Solution:** Install WiX Toolset v3.11 or later from https://wixtoolset.org/releases/

**Problem:** `Failed to harvest files!`
- **Solution:** Ensure the tray application has been built first:
  ```powershell
  dotnet build SpiceService.sln --configuration Release
  ```

**Problem:** `McpRemote.exe must be published first`
- **Solution:** The build script automatically publishes McpRemote.exe. If this error occurs, ensure the `McpRemote` project builds successfully.

**Problem:** Duplicate symbol errors during linking
- **Solution:** The build script automatically excludes manually defined files (SpiceServiceTray.exe, SpiceServiceTray.dll, McpRemote.exe) from harvesting. If errors persist, check that `Product.wxs` matches the expected structure.

### Installation Issues

**Problem:** "The system administrator has set policies to prevent this installation" (Error 1625)

**Diagnosis:**

This error indicates that Windows security policies are blocking the MSI installation. Common causes:

1. **Software Restriction Policy (SRP)** - Group Policy setting that blocks unsigned executables
2. **AppLocker** - Enterprise security feature blocking unsigned MSIs
3. **Windows Defender Application Control (WDAC)** - Advanced security policy
4. **Corporate/domain policies** - IT-managed security restrictions

**How to Diagnose:**

1. **Check Event Viewer:**
   ```
   - Open Event Viewer (Win+R → eventvwr.msc)
   - Navigate to: Windows Logs → Application
   - Look for events from "MsiInstaller" source around the time of installation failure
   - Check for Error 1625 or policy-related messages
   ```

2. **Check Group Policy:**
   ```
   - Open Group Policy Editor (Win+R → gpedit.msc)
   - Navigate to: Computer Configuration → Windows Settings → Security Settings → Software Restriction Policies
   - Check if Software Restriction Policies are configured
   - Look for rules blocking MSI files or unsigned executables
   ```

3. **Check AppLocker:**
   ```
   - Open Local Security Policy (Win+R → secpol.msc)
   - Navigate to: Application Control Policies → AppLocker
   - Check for rules under "Packaged app Rules" or "Windows Installer Rules"
   - Look for rules that might block unsigned MSIs
   ```

4. **Check MSI Signature:**
   ```
   - Right-click the MSI file → Properties → Digital Signatures tab
   - If "Digital Signatures" tab is missing, the MSI is unsigned
   - Unsigned MSIs are often blocked by security policies
   ```

5. **Check Installation Log:**
   ```
   - Run: msiexec /i SpiceServiceTray.msi /L*v install.log
   - Open install.log and search for "1625" or "policy"
   - Look for specific policy names or restrictions mentioned
   ```

**Solution Options:**

1. **Code Sign the MSI (Recommended for Production):**
   - Obtain a code signing certificate (from a trusted Certificate Authority)
   - Sign the MSI using `signtool.exe`:
     ```powershell
     signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com SpiceServiceTray.msi
     ```
   - Code-signed MSIs are typically trusted by security policies
   - **Note:** Code signing certificates cost money and require verification

2. **Use Batch Script Installer (Bypass MSI):**
   - The batch script installer (`install.bat`) completely bypasses MSI and policy checks
   - Located in: `SpiceServiceTray.Installer\install.bat`
   - Or use the ZIP package: `SpiceServiceTray-BatchInstaller.zip`
   - **Advantages:**
     - No MSI involved (bypasses MSI-specific policies)
     - No code signing required
     - Works in restricted environments
     - Same installation result (per-user, no admin required)

3. **Request Policy Exception (Enterprise/Corporate):**
   - Contact your IT administrator
   - Request exception for:
     - MSI file hash (if using hash-based rules)
     - Publisher certificate (if code-signed)
     - File path exception (if using path-based rules)
   - Provide MSI file details:
     - File hash: `Get-FileHash SpiceServiceTray.msi -Algorithm SHA256`
     - Publisher: Check Properties → Digital Signatures (if signed)
     - File path: Where the MSI will be located

4. **Temporary Workaround (Not Recommended):**
   - **Warning:** Only for personal/test machines, not for production
   - Temporarily disable Software Restriction Policy (if you have admin rights):
     ```
     - Open Group Policy Editor (gpedit.msc)
     - Navigate to: Computer Configuration → Windows Settings → Security Settings → Software Restriction Policies
     - Right-click "Software Restriction Policies" → Delete Policy
     - Reboot and try installation again
     - Re-enable policies after installation
     ```
   - **Note:** This may violate corporate security policies

5. **Install as Administrator (May Not Work):**
   - Some policies block even admin installations
   - Try: Right-click MSI → "Run as administrator"
   - If this works, the policy may only block non-admin installs
   - **Note:** This defeats the purpose of per-user installation

**Recommended Approach:**

For **personal use**: Use the batch script installer (`install.bat`) - it bypasses all MSI-related policies.

For **production/distribution**: Code sign the MSI with a trusted certificate authority certificate.

For **enterprise/corporate**: Work with IT to add an exception or use the batch script installer.

**Verification:**

After applying a solution, verify installation works:
```powershell
# Check if application installed
Test-Path "$env:LOCALAPPDATA\SpiceService\Tray\SpiceServiceTray.exe"

# Check if shortcuts created
Test-Path "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\SpiceService\SpiceService Tray.lnk"

# Launch application
& "$env:LOCALAPPDATA\SpiceService\Tray\SpiceServiceTray.exe"
```

**Problem:** "A newer version is already installed"
- **Solution:** Uninstall the existing version first, then install the new MSI

**Problem:** Application doesn't start after installation
- **Solution:**
  1. Check if `.NET 8.0 Desktop Runtime` is installed
  2. Launch manually from Start Menu: `SpiceService\SpiceService Tray`
  3. Check Windows Event Viewer for error messages
  4. View application logs from tray menu (if application starts)

**Problem:** Libraries not found
- **Solution:** Ensure the `libraries` folder exists at `%LocalAppData%\SpiceService\Tray\libraries\`. If missing, reinstall the MSI.

**Problem:** Shortcuts not created
- **Solution:** This may occur if user-specific folders are restricted. The application will still function; launch manually from the installation directory.

### Runtime Issues

**Problem:** "Application failed to start"
- **Solution:** Install `.NET 8.0 Desktop Runtime` from https://dotnet.microsoft.com/download/dotnet/8.0

**Problem:** MCP server not accessible
- **Solution:**
  1. Check tray icon status (should show "Running")
  2. Verify port 8081 is not in use by another application
  3. Check firewall settings (if network access is enabled)
  4. View logs from tray menu for detailed error messages

---

## Technical Details

### MSI Package Structure

- **Install Scope:** `perUser` (user-specific installation, no admin required)
- **Package Type:** Windows Installer Package (MSI)
- **Installer Version:** 500 (Windows Installer 5.0)
- **Upgrade Code:** `a1b2c3d4-e5f6-7890-abcd-ef1234567890` (fixed for all versions)

### File Harvesting

The build process uses WiX `heat.exe` to automatically harvest files:

1. **Application Files** (`HarvestedFiles.wxs`):
   - Source: `SpiceSharp.Api.Tray\bin\Release\net8.0-windows\`
   - Exclusions: `SpiceServiceTray.exe`, `SpiceServiceTray.dll`, `McpRemote.exe` (manually defined in `Product.wxs`)
   - Includes: All DLLs, config files, runtime dependencies

2. **Library Files** (`HarvestedLibraries.wxs`):
   - Source: `libraries\` directory (project root)
   - Includes: All `.lib` files (500+ SPICE component libraries)

3. **Manually Defined Files** (`Product.wxs`):
   - `SpiceServiceTray.exe` (main executable)
   - `SpiceServiceTray.dll` (main assembly)
   - `McpRemote.exe` (MCP proxy executable)

### Custom Actions

- **LaunchApplication:** Automatically starts the tray application after installation (if enabled)
- **Execute:** `immediate` (runs during installation, not deferred)
- **Impersonate:** `yes` (runs as installing user, not system)

### Registry Entries

The installer creates user-specific registry entries under `HKCU\Software\SpiceService\Tray`:
- `InstallPath`: Installation directory path
- `LibrariesPath`: Libraries directory path
- `Installed`: Installation flag (integer, value: 1)

---

## Version History

- **Current Version:** Determined automatically from `SpiceServiceTray.exe` file version
- **Build Number:** Calculated as days since 2024-01-01
- **Upgrade Behavior:** `MajorUpgrade` with downgrade prevention

---

## Support

For issues or questions:
- Check the main `README.md` for usage instructions
- Review `INSTALLATION_OPTIONS.md` for alternative installation methods
- View application logs from the tray menu
- Check Windows Event Viewer for system-level errors

---

**Last Updated:** 2025-01-21  
**MSI Build Method:** Standalone WiX Tools (fallback path)  
**Product.wxs Source:** Git commit `8a66115` (restored from working version)

