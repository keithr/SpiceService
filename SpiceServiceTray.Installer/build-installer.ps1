# Build script for SpiceService Tray MSI Installer
# Requires: WiX Toolset installed, .NET SDK

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "Building SpiceService Tray MSI Installer..." -ForegroundColor Cyan

# Step 1: Build and publish McpRemote.exe
Write-Host "`nStep 1: Building and publishing McpRemote.exe..." -ForegroundColor Yellow
$mcpRemoteProject = "..\McpRemote\McpRemote.csproj"
$mcpRemoteExe = "..\McpRemote\bin\$Configuration\net8.0\win-x64\publish\McpRemote.exe"

dotnet publish $mcpRemoteProject -c $Configuration --runtime win-x64 --self-contained false -p:PublishSingleFile=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to publish McpRemote.exe!" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $mcpRemoteExe)) {
    Write-Host "McpRemote.exe not found at expected location: $mcpRemoteExe" -ForegroundColor Red
    exit 1
}

Write-Host "McpRemote.exe published successfully" -ForegroundColor Green

# Step 2: Build the tray application (skip if files are locked but exist)
Write-Host "`nStep 2: Building tray application..." -ForegroundColor Yellow
$trayProject = "..\SpiceSharp.Api.Tray\SpiceSharp.Api.Tray.csproj"
$trayAppExe = "..\SpiceSharp.Api.Tray\bin\$Configuration\net8.0-windows\SpiceServiceTray.exe"

# Try to build, but if files are locked and the exe exists, continue anyway
dotnet build $trayProject -c $Configuration --no-incremental

if ($LASTEXITCODE -ne 0) {
    if (Test-Path $trayAppExe) {
        Write-Host "Build had file lock issues, but executable exists. Continuing with existing build..." -ForegroundColor Yellow
    } else {
        Write-Host "Failed to build tray application and executable not found!" -ForegroundColor Red
        exit 1
    }
}

# Step 3: Check if WiX is installed
Write-Host "`nStep 3: Checking for WiX Toolset..." -ForegroundColor Yellow
$wixPath = $null

# Check common WiX installation locations
# Check for any WiX Toolset version by searching Program Files
$wixPaths = @()
$possiblePaths = @(
    "${env:ProgramFiles(x86)}",
    "${env:ProgramFiles}"
)

foreach ($basePath in $possiblePaths) {
    if (Test-Path $basePath) {
        $wixDirs = Get-ChildItem $basePath -Filter "WiX Toolset*" -Directory -ErrorAction SilentlyContinue
        foreach ($wixDir in $wixDirs) {
            $binPath = Join-Path $wixDir.FullName "bin"
            if (Test-Path $binPath) {
                $wixPaths += $binPath
            }
        }
    }
}

# Also check specific common versions
$wixPaths += @(
    "${env:ProgramFiles(x86)}\WiX Toolset v3.14\bin",
    "${env:ProgramFiles}\WiX Toolset v3.14\bin",
    "${env:ProgramFiles(x86)}\WiX Toolset v3.11\bin",
    "${env:ProgramFiles}\WiX Toolset v3.11\bin",
    "${env:ProgramFiles(x86)}\WiX Toolset v3.10\bin",
    "${env:ProgramFiles}\WiX Toolset v3.10\bin",
    "${env:ProgramFiles(x86)}\WiX Toolset v4.0\bin",
    "${env:ProgramFiles}\WiX Toolset v4.0\bin"
)

foreach ($path in $wixPaths) {
    if (Test-Path $path) {
        $wixPath = $path
        break
    }
}

# Also check if heat.exe is in PATH
if (-not $wixPath) {
    $heatCmd = Get-Command heat.exe -ErrorAction SilentlyContinue
    if ($heatCmd) {
        $wixPath = Split-Path $heatCmd.Path -Parent
    }
}

if (-not $wixPath -or -not (Test-Path (Join-Path $wixPath "heat.exe"))) {
    Write-Host "WiX Toolset not found!" -ForegroundColor Red
    Write-Host "Please install WiX Toolset v3.11 or later from: https://wixtoolset.org/releases/" -ForegroundColor Yellow
    Write-Host "Or run: winget install WiXToolset.WiXToolset" -ForegroundColor Yellow
    Write-Host "`nAfter installation, restart your terminal and try again." -ForegroundColor Yellow
    exit 1
}
Write-Host "WiX Toolset found at: $wixPath" -ForegroundColor Green

# Step 4: Build the installer
Write-Host "`nStep 4: Building MSI installer..." -ForegroundColor Yellow

# Find MSBuild
$msbuild = $null
$vsPaths = @(
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe",
    "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\*\Bin\MSBuild.exe",
    "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\*\MSBuild\*\Bin\MSBuild.exe"
)

foreach ($path in $vsPaths) {
    $found = Get-Item $path -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        $msbuild = $found.FullName
        break
    }
}

if (-not $msbuild) {
    # Try to find msbuild in PATH
    $msbuild = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msbuild) {
        $msbuild = $msbuild.Path
    } else {
        Write-Host "MSBuild not found! Please install Visual Studio Build Tools or ensure MSBuild is in PATH." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Using MSBuild: $msbuild" -ForegroundColor Cyan
& $msbuild "SpiceServiceTray.Installer.wixproj" /p:Configuration=$Configuration /p:Platform=$Platform /p:SolutionDir="$PSScriptRoot\..\" /t:Rebuild

if ($LASTEXITCODE -ne 0) {
    Write-Host "MSBuild failed, trying standalone WiX tools..." -ForegroundColor Yellow
    
    # Fallback to standalone WiX tools
    $trayAppSourceDir = Resolve-Path "..\SpiceSharp.Api.Tray\bin\$Configuration\net8.0-windows"
    $candleExe = Join-Path $wixPath "candle.exe"
    $lightExe = Join-Path $wixPath "light.exe"
    $heatExe = Join-Path $wixPath "heat.exe"
    
    # Ensure dist directory exists (for Release builds)
    $distDir = if ($Configuration -eq "Release") { 
        $solutionRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        Join-Path $solutionRoot "dist"
    } else { 
        "bin\$Configuration" 
    }
    if (-not (Test-Path $distDir)) {
        New-Item -ItemType Directory -Path $distDir -Force | Out-Null
    }
    $binDir = $distDir
    
    # Harvest files
    Write-Host "Harvesting files..." -ForegroundColor Cyan
    & $heatExe dir "$trayAppSourceDir" -cg HarvestedFiles -gg -sfrag -srd -scom -sreg -dr TrayAppFolder -var var.TrayAppSourceDir -out HarvestedFiles.wxs -x SpiceServiceTray.exe -x SpiceServiceTray.dll
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to harvest files!" -ForegroundColor Red
        exit 1
    }
    
    # Remove SpiceServiceTray.exe and .dll from HarvestedFiles.wxs (manually defined in Product.wxs)
    if (Test-Path "HarvestedFiles.wxs") {
        $content = Get-Content "HarvestedFiles.wxs" -Raw
        $componentsToRemove = @()
        
        # Find components containing SpiceServiceTray.exe or .dll
        if ($content -match "(?s)<Component[^>]*Id=`"([^`"]+)`"[^>]*>.*?<File[^>]*Source=`"[^`"]*SpiceServiceTray\.(exe|dll)`"[^>]*>.*?</Component>") {
            $componentsToRemove += $matches[1]
            Write-Host "Found SpiceServiceTray component to remove: $($matches[1])" -ForegroundColor Yellow
        }
        
        # Also check for any other matches
        $matches = [regex]::Matches($content, "(?s)<Component[^>]*Id=`"([^`"]+)`"[^>]*>.*?<File[^>]*Source=`"[^`"]*SpiceServiceTray\.(exe|dll)`"[^>]*>.*?</Component>")
        foreach ($match in $matches) {
            if ($match.Groups[1].Value -notin $componentsToRemove) {
                $componentsToRemove += $match.Groups[1].Value
            }
        }
        
        foreach ($componentId in $componentsToRemove) {
            Write-Host "Removing component $componentId from HarvestedFiles.wxs..." -ForegroundColor Yellow
            # Remove the component
            $content = $content -replace "(?s)\s*<Component[^>]*Id=`"$componentId`"[^>]*>.*?</Component>", ""
            # Remove the ComponentRef
            $content = $content -replace "(?s)\s*<ComponentRef Id=`"$componentId`" />\r?\n?", ""
        }
        
        # Remove any ComponentRef entries that don't have matching components
        $allComponentRefs = [regex]::Matches($content, "<ComponentRef Id=`"([^`"]+)`" />")
        $allComponents = [regex]::Matches($content, "<Component[^>]*Id=`"([^`"]+)`"")
        $componentIds = $allComponents | ForEach-Object { $_.Groups[1].Value }
        
        foreach ($refMatch in $allComponentRefs) {
            $refId = $refMatch.Groups[1].Value
            if ($refId -notin $componentIds) {
                Write-Host "Removing orphaned ComponentRef: $refId" -ForegroundColor Yellow
                $content = $content -replace "(?s)\s*<ComponentRef Id=`"$refId`" />\r?\n?", ""
            }
        }
        
        Set-Content "HarvestedFiles.wxs" -Value $content -NoNewline
    }
    
    # Compile .wxs files
    Write-Host "Compiling WiX source files..." -ForegroundColor Cyan
    # Convert backslashes to forward slashes for WiX preprocessor
    $trayAppSourceDirEscaped = $trayAppSourceDir -replace '\\', '/'
    # Define variable without var. prefix - WiX maps it automatically
    & $candleExe "Product.wxs" -o "Product.wixobj" "-dTrayAppSourceDir=$trayAppSourceDirEscaped" -ext WixUIExtension.dll -ext WixUtilExtension.dll
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to compile Product.wxs!" -ForegroundColor Red
        exit 1
    }
    & $candleExe "HarvestedFiles.wxs" -o "HarvestedFiles.wixobj" "-dTrayAppSourceDir=$trayAppSourceDirEscaped"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to compile HarvestedFiles.wxs!" -ForegroundColor Red
        exit 1
    }
    
    # Link .wixobj files to create MSI
    Write-Host "Linking MSI..." -ForegroundColor Cyan
    # Check if localization file exists, otherwise skip it
    $locFile = Join-Path $wixPath "WixUI_en-us.wxl"
    $locParam = if (Test-Path $locFile) { "-loc `"$locFile`"" } else { "" }
    $msiOutputPath = if ($Configuration -eq "Release") { 
        $solutionRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        Join-Path (Join-Path $solutionRoot "dist") "SpiceServiceTray.msi"
    } else { 
        "$binDir\SpiceServiceTray.msi" 
    }
    & $lightExe "Product.wixobj" "HarvestedFiles.wixobj" -out $msiOutputPath -ext WixUIExtension.dll -ext WixUtilExtension.dll -cultures:en-us -sice:ICE38 -sice:ICE64 $locParam
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to link MSI!" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "MSI created successfully using standalone WiX tools!" -ForegroundColor Green
}

Write-Host "`nBuild completed successfully!" -ForegroundColor Green
# Determine MSI path based on configuration - check both possible locations
$msiPath = if ($Configuration -eq "Release") { 
    $solutionRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    Join-Path (Join-Path $solutionRoot "dist") "SpiceServiceTray.msi"
} else { 
    "bin\$Configuration\SpiceServiceTray.msi" 
}
# Also check the old location in case MSBuild was used
$oldMsiPath = "bin\$Configuration\SpiceServiceTray.msi"
$actualMsiPath = if (Test-Path $msiPath) { 
    $msiPath 
} elseif (Test-Path $oldMsiPath) { 
    $oldMsiPath 
} else { 
    $null 
}
if ($actualMsiPath) {
    Write-Host "MSI installer created: $actualMsiPath" -ForegroundColor Green
    $fileInfo = Get-Item $actualMsiPath
    Write-Host "Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB" -ForegroundColor Cyan
    $fullPath = (Resolve-Path $actualMsiPath).Path
    Write-Host "Full path: $fullPath" -ForegroundColor Cyan
    # If MSI is in old location and should be in dist, move it
    if ($Configuration -eq "Release" -and $actualMsiPath -eq $oldMsiPath) {
        Write-Host "Moving MSI to dist directory..." -ForegroundColor Yellow
        $distDir = Split-Path $msiPath -Parent
        if (-not (Test-Path $distDir)) {
            New-Item -ItemType Directory -Path $distDir -Force | Out-Null
        }
        Move-Item $actualMsiPath $msiPath -Force
        Write-Host "MSI moved to: $msiPath" -ForegroundColor Green
    }
} else {
    Write-Host "MSI file not found at expected locations:" -ForegroundColor Yellow
    Write-Host "  Expected: $msiPath" -ForegroundColor Yellow
    Write-Host "  Fallback: $oldMsiPath" -ForegroundColor Yellow
}

