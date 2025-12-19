# Start SpiceSharp REST API Server
# This script starts the API server in a new window

$projectPath = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectPath "SpiceSharp.Api.Web.csproj"

Write-Host "Starting SpiceSharp REST API Server..." -ForegroundColor Green
Write-Host "Project: $projectFile" -ForegroundColor Gray
Write-Host ""

# Check if project exists
if (-not (Test-Path $projectFile)) {
    Write-Host "Error: Project file not found at $projectFile" -ForegroundColor Red
    exit 1
}

# Build first to ensure everything is up to date
Write-Host "Building project..." -ForegroundColor Yellow
$buildResult = dotnet build $projectFile 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Write-Host $buildResult
    exit 1
}

Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""
Write-Host "Starting server..." -ForegroundColor Yellow
Write-Host "API will be available at: http://localhost:5126" -ForegroundColor Cyan
Write-Host "Swagger UI: http://localhost:5126" -ForegroundColor Cyan
Write-Host ""
Write-Host "Press Ctrl+C to stop the server" -ForegroundColor Gray
Write-Host ""

# Run the server
Set-Location $projectPath
dotnet run

