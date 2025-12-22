# Installation Options Without Admin Privileges

Since the MSI is being blocked by Software Restriction Policy, here are alternative installation methods:

## Option 1: ZIP File Distribution (Recommended - No Installer Needed)

**Pros:**
- No MSI/installer required
- No policy blocking
- Works immediately
- Easy to update (just replace files)

**Cons:**
- No automatic Start Menu shortcuts
- No automatic uninstaller
- User must manually create shortcuts

**Implementation:**
1. Create a ZIP file with all application files
2. User extracts to `%LocalAppData%\SpiceService\Tray\`
3. User manually creates shortcuts if desired
4. Include a simple batch script to create shortcuts

## Option 2: Batch Script Installer

**Pros:**
- No MSI/installer required
- Can create shortcuts automatically
- No policy blocking
- Simple to maintain

**Cons:**
- Less polished than MSI
- No uninstaller (manual deletion)

**Implementation:**
- Create `install.bat` that:
  - Copies files to `%LocalAppData%\SpiceService\Tray\`
  - Creates Start Menu shortcuts
  - Creates Desktop shortcut (optional)
  - Creates Startup shortcut (optional)

## Option 3: PowerShell Script Installer

**Pros:**
- More robust than batch script
- Better error handling
- Can create shortcuts properly
- Can check for .NET runtime

**Cons:**
- May trigger execution policy warnings
- Less familiar to some users

## Option 4: Fix MSI with Different Approach

Try these MSI modifications:

### 4a. Remove ALL CustomActions
- CustomActions might trigger policy checks
- Remove the LaunchApplication CustomAction entirely

### 4b. Use InstallerVersion 200 instead of 500
- Older installer versions might bypass some policy checks

### 4c. Add Property to Mark as Safe
```xml
<Property Id="MSIINSTALLPERUSER" Value="1" />
<Property Id="ALLUSERS" Value="" />
```

### 4d. Remove UI Completely
- Some UI extensions might trigger checks
- Use silent installation only

## Option 5: Portable Application Distribution

**Pros:**
- No installation needed
- User runs from any location
- No policy issues
- Easy to update

**Cons:**
- No automatic shortcuts
- User must manage location

**Implementation:**
- Package as portable app
- Include README with instructions
- Optional: Include shortcut creation script

## Option 6: Use Different Installer Technology

### 6a. Inno Setup
- Free installer builder
- Better policy handling
- Can create per-user installers easily

### 6b. NSIS (Nullsoft Scriptable Install System)
- Free, open-source
- Good per-user support
- Less likely to trigger policy

## Recommendation

**Best Option: ZIP File + Batch Script**

This provides:
- ✅ No admin rights needed
- ✅ No policy blocking
- ✅ Automatic shortcut creation
- ✅ Simple for users
- ✅ Easy to maintain

Would you like me to create:
1. A ZIP packaging script
2. A batch script installer
3. A PowerShell installer
4. Try more MSI modifications

