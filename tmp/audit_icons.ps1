$ribbonFile = "c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml"
$lines = Get-Content $ribbonFile

$isInsideItem = $false
$currentCommand = ""
$currentIcon = ""
$currentName = ""

Write-Host "--- DETAILED COMMAND AUDIT ---"

for ($i = 0; $i -lt $lines.Count; $i++) {
    $line = $lines[$i]
    
    # Check for start of an item (- name: or - type:)
    if ($line -match "^\s*-\s+name:\s*(.*)") {
        $currentName = $Matches[1].Trim()
        $currentCommand = ""
        $currentIcon = ""
        
        # Scan ahead for command and icon in this block
        $j = $i + 1
        while ($j -lt $lines.Count -and $lines[$j] -notmatch "^\s*-\s+(name|type|separator):") {
            if ($lines[$j] -match "command:\s*(\w+)") { $currentCommand = $Matches[1] }
            if ($lines[$j] -match "icon:\s*(\w+)") { $currentIcon = $Matches[1] }
            $j++
        }
        
        if ($currentCommand -ne "") {
            if ($currentIcon -eq "") {
                Write-Host "MISSING ICON: Command '$currentCommand' ($currentName) at line $($i+1) has no 'icon' key."
            }
            else {
                # Write-Host "FOUND: $currentCommand -> $currentIcon"
            }
        }
    }
}
