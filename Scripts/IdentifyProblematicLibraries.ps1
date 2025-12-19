# Script to identify problematic KiCad library files
# Files that can't be parsed, have no models, or are clearly defective

$librariesPath = Join-Path $PSScriptRoot "..\libraries"
$parserPath = Join-Path $PSScriptRoot "..\SpiceSharp.Api.Core\bin\Debug\net8.0\SpiceSharp.Api.Core.dll"

$problematicFiles = @{
    ParseErrors = @()
    NoModels = @()
    EmptyFiles = @()
    SubcircuitOnly = @()
    InvalidContent = @()
}

$libraryFiles = Get-ChildItem -Path $librariesPath -Filter "kicad_*.lib"

Write-Host "Analyzing $($libraryFiles.Count) library files..." -ForegroundColor Cyan

foreach ($file in $libraryFiles) {
    $filePath = $file.FullName
    $fileName = $file.Name
    
    try {
        $content = Get-Content $filePath -Raw -ErrorAction Stop
        
        # Check if file is empty or only whitespace
        if ([string]::IsNullOrWhiteSpace($content)) {
            $problematicFiles.EmptyFiles += $fileName
            continue
        }
        
        # Check for .MODEL statements
        $hasModel = $content -match "\.MODEL\s+\w+\s+\w+"
        
        # Check for .SUBCKT (subcircuits - not supported by our parser)
        $hasSubckt = $content -match "\.SUBCKT"
        
        # Check for other unsupported statements
        $hasUnsupported = $content -match "\.ENDS\s|\.INCLUDE|\.LIB\s"
        
        if (-not $hasModel) {
            if ($hasSubckt) {
                $problematicFiles.SubcircuitOnly += $fileName
            }
            elseif ($hasUnsupported) {
                $problematicFiles.InvalidContent += $fileName
            }
            else {
                $problematicFiles.NoModels += $fileName
            }
        }
        
        # Try to parse with our parser (if we can load the DLL)
        # For now, we'll rely on the regex checks above
        
    }
    catch {
        $problematicFiles.ParseErrors += "$fileName : $($_.Exception.Message)"
    }
}

Write-Host "`n=== Analysis Results ===" -ForegroundColor Yellow
Write-Host "Empty files: $($problematicFiles.EmptyFiles.Count)" -ForegroundColor $(if ($problematicFiles.EmptyFiles.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Subcircuit-only files (.SUBCKT): $($problematicFiles.SubcircuitOnly.Count)" -ForegroundColor $(if ($problematicFiles.SubcircuitOnly.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Files with no models: $($problematicFiles.NoModels.Count)" -ForegroundColor $(if ($problematicFiles.NoModels.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Parse errors: $($problematicFiles.ParseErrors.Count)" -ForegroundColor $(if ($problematicFiles.ParseErrors.Count -gt 0) { "Red" } else { "Green" })
Write-Host "Invalid content: $($problematicFiles.InvalidContent.Count)" -ForegroundColor $(if ($problematicFiles.InvalidContent.Count -gt 0) { "Red" } else { "Green" })

$totalProblematic = $problematicFiles.EmptyFiles.Count + 
                   $problematicFiles.SubcircuitOnly.Count + 
                   $problematicFiles.NoModels.Count + 
                   $problematicFiles.ParseErrors.Count +
                   $problematicFiles.InvalidContent.Count

Write-Host "`nTotal problematic files: $totalProblematic" -ForegroundColor $(if ($totalProblematic -gt 0) { "Red" } else { "Green" })

# Export list of files to remove
$filesToRemove = $problematicFiles.EmptyFiles + 
                $problematicFiles.SubcircuitOnly + 
                $problematicFiles.NoModels +
                ($problematicFiles.ParseErrors | ForEach-Object { ($_ -split " : ")[0] }) +
                $problematicFiles.InvalidContent

if ($filesToRemove.Count -gt 0) {
    $outputFile = Join-Path $librariesPath "..\files_to_remove.txt"
    $filesToRemove | Sort-Object | Out-File -FilePath $outputFile -Encoding UTF8
    Write-Host "`nList of files to remove saved to: $outputFile" -ForegroundColor Cyan
    Write-Host "Files to remove ($($filesToRemove.Count)):" -ForegroundColor Yellow
    $filesToRemove | Select-Object -First 20 | ForEach-Object { Write-Host "  - $_" }
    if ($filesToRemove.Count -gt 20) {
        Write-Host "  ... and $($filesToRemove.Count - 20) more" -ForegroundColor Gray
    }
}

return $problematicFiles
