# Same as Python script but in PowerShell, using simple state machine
$ribbonFile = "c:\Users\DELL\source\repos\antiGGGravity\Resources\ribbon.yaml"

$lines = Get-Content $ribbonFile

$iconToCommands = @{} # Icon name -> List of Commands
$currentCommand = ""
$currentIcon = ""

# Track each pushbutton's icon
$i = 0
while ($i -lt $lines.Count) {
    if ($lines[$i] -match " - name: (\w+)") {
        # Starting a new button block
        $btnCmd = ""
        $btnIcon = ""
        $j = $i
        while ($j -lt $lines.Count -and ($j -eq $i -or $lines[$j] -notmatch "^      - ")) {
            if ($lines[$j] -match "command: (\w+)") { $btnCmd = $Matches[1] }
            if ($lines[$j] -match "icon: (\w+)") { $btnIcon = $Matches[1] }
            $j++
        }
       
        if ($btnCmd -ne "" -and $btnIcon -ne "") {
            if (-not $iconToCommands.ContainsKey($btnIcon)) {
                $iconToCommands[$btnIcon] = New-Object System.Collections.Generic.List[string]
            }
            $iconToCommands[$btnIcon].Add($btnCmd)
        }
    }
    $i++
}

foreach ($icon in $iconToCommands.Keys) {
    if ($iconToCommands[$icon].Count -gt 1) {
        $cmds = $iconToCommands[$icon] | Select-Object -Unique | Sort-Object
        if ($cmds.Count -gt 1) {
            Write-Host "Shared Icon '$icon': Used by $($cmds -join ', ')"
        }
    }
}
