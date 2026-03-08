$ribbonFile = "c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml"

$lines = Get-Content $ribbonFile
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -match "command: (\w+)") {
        $found = $false
        # check up to 3 lines before or after for an icon:
        for ($j = -3; $j -le 3; $j++) {
            if (($i + $j -ge 0) -and ($i + $j -lt $lines.Count)) {
                if ($lines[$i + $j] -match "icon: (\w+)") {
                    $found = $true
                    break
                }
            }
        }
        if (-not $found) {
            Write-Host "Missing Icon for command at line $($i+1): $($lines[$i].Trim())"
        }
    }
}
