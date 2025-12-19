# PowerShell script to create a multi-resolution ICO file from PNG images
# This script uses .NET to create a proper Windows ICO file with multiple sizes

param(
    [string]$OutputPath = "..\resources\spice.ico",
    [string]$ResourcesPath = "..\resources"
)

Add-Type -AssemblyName System.Drawing

# Define the sizes we want in the ICO file
$sizes = @(16, 32, 48, 64, 128, 256)

# Find available PNG files - prefer spice_100x100.png as primary source
# Fall back to other sizes if needed
$primarySource = $null
$possibleSources = @(
    "$ResourcesPath\spice_100x100.png",
    "$ResourcesPath\spice_256x256.png",
    "$ResourcesPath\spice_50x50.png"
)

foreach ($sourcePath in $possibleSources) {
    $fullPath = Resolve-Path $sourcePath -ErrorAction SilentlyContinue
    if ($fullPath -and (Test-Path $fullPath)) {
        $primarySource = Get-Item $fullPath
        break
    }
}

if (-not $primarySource) {
    # Try relative to script directory
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    foreach ($sourcePath in $possibleSources) {
        $fullPath = Join-Path $scriptDir $sourcePath
        if (Test-Path $fullPath) {
            $primarySource = Get-Item $fullPath
            break
        }
    }
}

# Use the primary source for all sizes to ensure consistency
$pngFiles = @{}
foreach ($size in $sizes) {
    $pngFiles[$size] = $primarySource
}

Write-Host "Using source image: $($primarySource.Name) for all icon sizes"

# Create a list to hold the icon images
$iconImages = New-Object System.Collections.ArrayList

foreach ($size in $sizes) {
    $sourceFile = $pngFiles[$size]
    
    if ($sourceFile -and $sourceFile.Exists) {
        try {
            # Load the source image
            $sourceImage = [System.Drawing.Image]::FromFile($sourceFile.FullName)
            
            # Create a new bitmap at the target size
            $bitmap = New-Object System.Drawing.Bitmap($size, $size)
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            
            # Use high-quality scaling
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            
            # Draw the image scaled to the target size
            $graphics.DrawImage($sourceImage, 0, 0, $size, $size)
            
            # Clean up
            $graphics.Dispose()
            $sourceImage.Dispose()
            
            # Add to list
            [void]$iconImages.Add($bitmap)
            
            Write-Host "Added $size x $size icon from $($sourceFile.Name)"
        }
        catch {
            Write-Warning "Failed to process $size x $size : $($_.Exception.Message)"
        }
    }
    else {
        Write-Warning "Source file not found for size $size"
    }
}

if ($iconImages.Count -eq 0) {
    Write-Error "No icon images could be created. Please check that PNG files exist in $ResourcesPath"
    exit 1
}

# Create the ICO file
try {
    # Get the full output path
    $fullOutputPath = Resolve-Path (Split-Path $OutputPath -Parent) | Select-Object -ExpandProperty Path
    $fullOutputPath = Join-Path $fullOutputPath (Split-Path $OutputPath -Leaf)
    
    # Create a memory stream to write the ICO file
    $icoStream = New-Object System.IO.MemoryStream
    
    # Write ICO header
    $icoStream.WriteByte(0)  # Reserved (must be 0)
    $icoStream.WriteByte(0)  # Reserved (must be 0)
    $icoStream.WriteByte(1)  # Type (1 = ICO)
    $icoStream.WriteByte(0)  # Type (continued)
    $icoStream.WriteByte([byte]$iconImages.Count)  # Number of images
    $icoStream.WriteByte(0)  # Number of images (continued)
    
    # Calculate offset for image data (header + directory entries)
    $headerSize = 6
    $directoryEntrySize = 16
    $currentOffset = $headerSize + ($iconImages.Count * $directoryEntrySize)
    
    # Write directory entries
    foreach ($bitmap in $iconImages) {
        $width = [Math]::Min(255, $bitmap.Width)
        $height = [Math]::Min(255, $bitmap.Height)
        
        # Write directory entry
        $icoStream.WriteByte([byte]$width)  # Width (0 = 256)
        $icoStream.WriteByte([byte]$height)  # Height (0 = 256)
        $icoStream.WriteByte(0)  # Color palette (0 = no palette)
        $icoStream.WriteByte(0)  # Reserved
        $icoStream.WriteByte(1)  # Color planes (1)
        $icoStream.WriteByte(0)  # Color planes (continued)
        $icoStream.WriteByte(32)  # Bits per pixel
        $icoStream.WriteByte(0)  # Bits per pixel (continued)
        
        # Calculate PNG data size
        $pngStream = New-Object System.IO.MemoryStream
        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData = $pngStream.ToArray()
        $pngSize = $pngData.Length
        $pngStream.Dispose()
        
        # Write size (4 bytes, little-endian)
        $sizeBytes = [BitConverter]::GetBytes($pngSize)
        $icoStream.Write($sizeBytes, 0, 4)
        
        # Write offset (4 bytes, little-endian)
        $offsetBytes = [BitConverter]::GetBytes($currentOffset)
        $icoStream.Write($offsetBytes, 0, 4)
        
        # Update offset for next image
        $currentOffset += $pngSize
    }
    
    # Write image data
    foreach ($bitmap in $iconImages) {
        $pngStream = New-Object System.IO.MemoryStream
        $bitmap.Save($pngStream, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData = $pngStream.ToArray()
        $icoStream.Write($pngData, 0, $pngData.Length)
        $pngStream.Dispose()
    }
    
    # Write to file
    [System.IO.File]::WriteAllBytes($fullOutputPath, $icoStream.ToArray())
    $icoStream.Dispose()
    
    Write-Host "Successfully created ICO file: $fullOutputPath"
    Write-Host "ICO file contains $($iconImages.Count) icon sizes"
}
catch {
    Write-Error "Failed to create ICO file: $($_.Exception.Message)"
    exit 1
}
finally {
    # Clean up bitmaps
    foreach ($bitmap in $iconImages) {
        $bitmap.Dispose()
    }
}

