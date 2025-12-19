# Build Verification Script
# Verifies that the project still builds after cleanup

$ErrorActionPreference = "Stop"
$solutionPath = Join-Path $PSScriptRoot "..\SpiceService.sln"

Write-Host "========================================="
Write-Host "Build Verification"
Write-Host "========================================="
Write-Host "Solution: $solutionPath"
Write-Host ""

if (-not (Test-Path $solutionPath)) {
    Write-Error "Solution file not found: $solutionPath"
    exit 1
}

# Check if MSBuild is available
$msbuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe 2>$null

if (-not $msbuildPath) {
    # Try alternative locations
    $msbuildPath = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msbuildPath) {
        $msbuildPath = $msbuildPath.Source
    }
}

if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
    Write-Warning "MSBuild not found. Attempting to use dotnet build instead..."
    $useDotnet = $true
}
else {
    Write-Host "Using MSBuild: $msbuildPath"
    $useDotnet = $false
}

Write-Host ""
Write-Host "Building solution..."
Write-Host ""

try {
    if ($useDotnet) {
        $buildResult = & dotnet build $solutionPath --configuration Release --no-incremental 2>&1
        $exitCode = $LASTEXITCODE
    }
    else {
        $buildResult = & $msbuildPath $solutionPath /p:Configuration=Release /t:Rebuild /v:minimal 2>&1
        $exitCode = $LASTEXITCODE
    }
    
    $buildResult | ForEach-Object { Write-Host $_ }
    
    Write-Host ""
    Write-Host "========================================="
    if ($exitCode -eq 0) {
        Write-Host "[OK] Build SUCCESSFUL" -ForegroundColor Green
        Write-Host "========================================="
        exit 0
    }
    else {
        Write-Host "[ERROR] Build FAILED (Exit code: $exitCode)" -ForegroundColor Red
        Write-Host "========================================="
        exit $exitCode
    }
}
catch {
    Write-Host ""
    Write-Host "========================================="
    Write-Host "[ERROR] Build ERROR: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "========================================="
    exit 1
}
