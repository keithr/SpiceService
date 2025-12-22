# Create Batch Script Installer Package
# This creates a folder with all files + install.bat that users can run

param(
    [string]$OutputDir = "..\dist\SpiceServiceTray-BatchInstaller"
)

Write-Host "Creating Batch Script Installer Package..." -ForegroundColor Cyan

$trayAppDir = "..\SpiceSharp.Api.Tray\bin\Release\net8.0-windows"
$mcpRemoteDir = "..\McpRemote\bin\Release\net8.0\win-x64\publish"
$librariesDir = "..\libraries"

Write-Host "Building application..." -ForegroundColor Yellow
dotnet build ..\SpiceService.sln --configuration Release | Out-Null

if (-not (Test-Path $trayAppDir)) {
    Write-Host "ERROR: Tray app not found at $trayAppDir" -ForegroundColor Red
    exit 1
}

Write-Host "Creating installer directory..." -ForegroundColor Yellow
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Write-Host "Copying application files..." -ForegroundColor Yellow
Copy-Item "$trayAppDir\*" -Destination $OutputDir -Recurse -Exclude "*.pdb"

Write-Host "Copying McpRemote.exe..." -ForegroundColor Yellow
Copy-Item "$mcpRemoteDir\McpRemote.exe" -Destination $OutputDir -Force

Write-Host "Copying library files..." -ForegroundColor Yellow
$libDest = Join-Path $OutputDir "libraries"
New-Item -ItemType Directory -Path $libDest -Force | Out-Null
Copy-Item "$librariesDir\*" -Destination $libDest -Recurse

Write-Host "Copying installer script..." -ForegroundColor Yellow
Copy-Item "install.bat" -Destination $OutputDir -Force

Write-Host "Creating README..." -ForegroundColor Yellow
$readme = @"
SpiceService Tray - Batch Installer
===================================

INSTALLATION:
1. Double-click install.bat
2. Follow the prompts
3. No administrator rights required!

The application will be installed to:
%LocalAppData%\SpiceService\Tray

UNINSTALLATION:
1. Delete the folder: %LocalAppData%\SpiceService\Tray
2. Delete shortcuts from Start Menu, Desktop, and Startup (if created)

For more information, see the main README.md
"@
$readme | Out-File -FilePath (Join-Path $OutputDir "README.txt") -Encoding UTF8

Write-Host "`n✅ Batch installer package created!" -ForegroundColor Green
Write-Host "Location: $OutputDir" -ForegroundColor Cyan
$dirInfo = Get-ChildItem $OutputDir -Recurse | Measure-Object -Property Length -Sum
Write-Host "Size: $([math]::Round($dirInfo.Sum / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host "`nUsers can:" -ForegroundColor Yellow
Write-Host "  1. Extract/copy this folder anywhere" -ForegroundColor White
Write-Host "  2. Run install.bat" -ForegroundColor White
Write-Host "  3. Follow the prompts" -ForegroundColor White
Write-Host "`nThis completely bypasses MSI and policy checks!" -ForegroundColor Green

