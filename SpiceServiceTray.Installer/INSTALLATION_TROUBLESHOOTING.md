# Installation Troubleshooting

## Windows SmartScreen Blocking

If Windows shows "Windows protected your PC" or blocks the installer:

### Option 1: Unblock the File (Recommended)
1. Right-click `SpiceServiceTray.msi`
2. Select **Properties**
3. If you see an **Unblock** checkbox at the bottom, check it
4. Click **OK**
5. Try installing again

### Option 2: Run Anyway
1. When Windows shows the SmartScreen warning, click **More info**
2. Click **Run anyway**
3. The installer should proceed

### Option 3: Disable SmartScreen Temporarily (Not Recommended)
1. Open Windows Security
2. Go to **App & browser control**
3. Under **Check apps and files**, select **Warn** or **Off**
4. Install the MSI
5. Re-enable SmartScreen after installation

## Why This Happens

The MSI installer is **not code-signed** (requires a paid certificate). Windows SmartScreen blocks unsigned installers by default as a security measure.

## Verification

The installer is correctly configured for **per-user installation** (no admin rights required):
- ✅ `InstallScope="perUser"` 
- ✅ Installs to `%LocalAppData%\SpiceService\Tray\`
- ✅ Uses HKCU registry (user registry, not system)
- ✅ No system folder modifications

## Alternative: Install from Command Line

You can also install silently from command line:

```powershell
msiexec /i "SpiceServiceTray.msi" /qn
```

Or with a log file for debugging:

```powershell
msiexec /i "SpiceServiceTray.msi" /qn /l*v install.log
```

