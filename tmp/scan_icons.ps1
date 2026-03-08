$ribbonFile = "c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml"
$iconsDir = "c:\Users\DELL\source\repos\antiGGGravity\Resources\Icons"

# Get all lines matching icon: and extract the name
$yamlIcons = Get-Content $ribbonFile | Select-String "icon:\s*(\w+)" | ForEach-Object {
    $_.Matches.Groups[1].Value
} | Select-Object -Unique | Sort-Object

$existingFiles = Get-ChildItem $iconsDir -Filter "*.png" | ForEach-Object { $_.Name }

Write-Host "--- ANALYSIS RESULTS ---"
Write-Host ""

$missingCount = 0
foreach ($icon in $yamlIcons) {
    if ($icon -eq "icon") { continue } # Skip the comment line
    $expectedFile = "$icon(32x32).png"
    if ($expectedFile -notin $existingFiles) {
        Write-Host "MISSING: Icon '$icon' referenced in YAML but '$expectedFile' not found."
        $missingCount++
    }
}

if ($missingCount -eq 0) {
    Write-Host "All icons referenced in YAML exist in Resources/Icons."
}

Write-Host ""
Write-Host "--- UNUSED FILES ---"
$unusedCount = 0
foreach ($file in $existingFiles) {
    if ($file -match "(\w+)\(32x32\)\.png") {
        $name = $Matches[1]
        if ($name -notin $yamlIcons) {
            Write-Host "UNUSED: File '$file' exists but not referenced in YAML."
            $unusedCount++
        }
    } else {
        # Check files without the suffix
        $name = $file.Replace(".png", "")
        if ($name -notin $yamlIcons) {
            Write-Host "EXTRA: File '$file' does not follow convention or is unused."
            $unusedCount++
        }
    }
}

if ($unusedCount -eq 0) {
    Write-Host "No unused icon files found."
}
