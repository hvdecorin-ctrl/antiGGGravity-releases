Add-Type -AssemblyName System.Drawing

$iconPath = "c:\Users\DELL\source\repos\antiGGGravity\Resources\Icons"
$files = Get-ChildItem $iconPath -Filter *.png

Write-Host "Processing icons in $iconPath..."

foreach ($file in $files) {
    try {
        # Load image via stream to avoid file locking
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
        $ms = New-Object System.IO.MemoryStream(,$bytes)
        $img = [System.Drawing.Image]::FromStream($ms)

        if ($img.Width -ne 32 -or $img.Height -ne 32) {
            $resized = New-Object System.Drawing.Bitmap(32, 32)
            $graph = [System.Drawing.Graphics]::FromImage($resized)
            $graph.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graph.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graph.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            
            $graph.DrawImage($img, 0, 0, 32, 32)
            
            # Dispose resources before saving
            $graph.Dispose()
            $img.Dispose()
            $ms.Dispose()

            # Save resized image
            $resized.Save($file.FullName, [System.Drawing.Imaging.ImageFormat]::Png)
            $resized.Dispose()
            
            Write-Host "Resized: $($file.Name)"
        } else {
            $img.Dispose()
            $ms.Dispose()
        }
    } catch {
        Write-Warning "Failed to resize $($file.Name): $_"
    }
}

Write-Host "Done."
