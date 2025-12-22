# Create ZIP-based installer package
# This creates a ZIP file that users can extract and run install.bat

param(
    [string]$OutputPath = "..\dist\SpiceServiceTray-Portable.zip"
)

Write-Host "Creating ZIP-based installer package..." -ForegroundColor Cyan

$trayAppDir = "..\SpiceSharp.Api.Tray\bin\Release\net8.0-windows"
$mcpRemoteDir = "..\McpRemote\bin\Release\net8.0\win-x64\publish"
$librariesDir = "..\libraries"
$tempDir = Join-Path $env:TEMP "SpiceServiceTray-Installer-$(Get-Random)"

Write-Host "Building application..." -ForegroundColor Yellow
dotnet build ..\SpiceService.sln --configuration Release | Out-Null

if (-not (Test-Path $trayAppDir)) {
    Write-Host "ERROR: Tray app not found at $trayAppDir" -ForegroundColor Red
    exit 1
}

Write-Host "Creating temporary directory..." -ForegroundColor Yellow
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

Write-Host "Copying application files..." -ForegroundColor Yellow
Copy-Item "$trayAppDir\*" -Destination $tempDir -Recurse -Exclude "*.pdb"
Copy-Item "$mcpRemoteDir\McpRemote.exe" -Destination $tempDir -Force

Write-Host "Copying library files..." -ForegroundColor Yellow
$libDest = Join-Path $tempDir "libraries"
New-Item -ItemType Directory -Path $libDest -Force | Out-Null
Copy-Item "$librariesDir\*" -Destination $libDest -Recurse

Write-Host "Copying installer script..." -ForegroundColor Yellow
Copy-Item "install.bat" -Destination $tempDir -Force

Write-Host "Creating README..." -ForegroundColor Yellow
$readme = @"
SpiceService Tray - Portable Installation
==========================================

INSTALLATION:
1. Extract this ZIP file to any location
2. Run install.bat
3. Follow the prompts

The application will be installed to:
%LocalAppData%\SpiceService\Tray

No administrator rights required!

UNINSTALLATION:
1. Delete the folder: %LocalAppData%\SpiceService\Tray
2. Delete shortcuts from Start Menu, Desktop, and Startup (if created)

For more information, see the main README.md
"@
$readme | Out-File -FilePath (Join-Path $tempDir "README.txt") -Encoding UTF8

Write-Host "Creating ZIP file..." -ForegroundColor Yellow
$outputDir = Split-Path $OutputPath -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

Compress-Archive -Path "$tempDir\*" -DestinationPath $OutputPath -Force

Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item $tempDir -Recurse -Force

$zipInfo = Get-Item $OutputPath
Write-Host "`nZIP package created successfully!" -ForegroundColor Green
Write-Host "Location: $OutputPath" -ForegroundColor Cyan
Write-Host "Size: $([math]::Round($zipInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
Write-Host "`nUsers can extract this ZIP and run install.bat" -ForegroundColor Yellow

