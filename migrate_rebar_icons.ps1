Add-Type -AssemblyName System.Drawing
$sourceRoot = "C:\Users\DELL\AppData\Roaming\pyRevit\Extensions\antiGGGGravity.extension\antiGGGGravity.tab\Rebar.Panel"
$destDir = "c:\Users\DELL\source\repos\antiGGGravity\Resources\Icons"

# Function to process an icon
function Process-Icon($sourceFile, $baseName) {
    if (Test-Path $sourceFile) {
        Write-Host "Processing $baseName from $sourceFile"
        
        # Paths
        $dest32 = Join-Path $destDir "$baseName(32x32).png"
        $dest16 = Join-Path $destDir "$baseName(16x16).png"

        try {
            # Load original
            $img = [System.Drawing.Image]::FromFile($sourceFile)
            
            # Save 32x32 (assume original is good for 32x32 slot, resize if needed)
            $b32 = New-Object System.Drawing.Bitmap -ArgumentList 32, 32
            $g32 = [System.Drawing.Graphics]::FromImage($b32)
            $g32.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g32.DrawImage($img, 0, 0, 32, 32)
            $b32.Save($dest32, [System.Drawing.Imaging.ImageFormat]::Png)
            $g32.Dispose(); $b32.Dispose()

            # Save 16x16
            $b16 = New-Object System.Drawing.Bitmap -ArgumentList 16, 16
            $g16 = [System.Drawing.Graphics]::FromImage($b16)
            $g16.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $g16.DrawImage($img, 0, 0, 16, 16)
            $b16.Save($dest16, [System.Drawing.Imaging.ImageFormat]::Png)
            $g16.Dispose(); $b16.Dispose()

            $img.Dispose()
        }
        catch {
            Write-Host "Error processing $baseName : $_"
        }
    }
    else {
        Write-Host "No icon found for $baseName"
    }
}

# 1. Process Pulldowns
$pulldowns = Get-ChildItem -Path $sourceRoot -Recurse -Filter "*.pulldown"
foreach ($pd in $pulldowns) {
    $name = $pd.Name.Replace(".pulldown", "")
    $iconPath = Join-Path $pd.FullName "icon.png"
    Process-Icon $iconPath "Rebar$name"
}

# 2. Process Pushbuttons
$pushbuttons = Get-ChildItem -Path $sourceRoot -Recurse -Filter "*.pushbutton"
foreach ($pb in $pushbuttons) {
    $name = $pb.Name.Replace(".pushbutton", "")
    # Clean up name: "Select & Delete..." -> "SelectAndDelete..." ? No, keep strings simple or Remove spaces?
    # Keeping spaces is fine for filenames.
    $iconPath = Join-Path $pb.FullName "icon.png"
    Process-Icon $iconPath $name
}
