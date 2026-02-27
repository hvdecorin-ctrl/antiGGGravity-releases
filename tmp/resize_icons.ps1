$sourceDir = 'c:\Users\duy\Documents\Tool Development\C# command backup\Prepare icon'
$destDir = 'c:\Users\duy\Documents\Tool Development\Revit Addin Development\antiGGGravity\Resources\Icons'

$files = @{
    'Rebar Suit (32x32).png' = 'RebarSuite(32x32).png'
    'Rebar Palette (32x32).png' = 'RebarPalette(32x32).png'
    'Set Obscured (32x32).png' = 'SetObscured(32x32).png'
}

Add-Type -AssemblyName System.Drawing

foreach ($file in $files.Keys) {
    $srcPath = Join-Path $sourceDir $file
    $destPath = Join-Path $destDir $files[$file]
    
    if (Test-Path $srcPath) {
        $img = [System.Drawing.Image]::FromFile($srcPath)
        $newImg = New-Object System.Drawing.Bitmap(32, 32)
        $g = [System.Drawing.Graphics]::FromImage($newImg)
        
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        
        $g.DrawImage($img, 0, 0, 32, 32)
        
        $newImg.Save($destPath, [System.Drawing.Imaging.ImageFormat]::Png)
        
        $g.Dispose()
        $newImg.Dispose()
        $img.Dispose()
        
        Write-Host "Resized and saved: $destPath"
    } else {
        Write-Host "File not found: $srcPath"
    }
}
