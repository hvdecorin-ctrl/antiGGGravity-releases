# Deploy multi-version script
param (
    [string[]]$VersionsToBuild = @("R22", "R23", "R24", "R25", "R26")
)

$distRoot = "Distribute"

foreach ($v in $VersionsToBuild) {
    Write-Host "Building for $v..." -ForegroundColor Cyan

    dotnet build antiGGGravity.csproj -c $v

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $v. Stopping script." -ForegroundColor Red
        exit 1
    }
    
    $folderName = "R20" + $v.Substring(1)
    $targetDir = Join-Path $distRoot $folderName
    
    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir }
    
    # Assembly folder
    $binFolder = "bin\$v"
    if ($v -eq "R26" -or $v -eq "R25") {
        $binFolder = Join-Path $binFolder "net8.0-windows"
    } else {
        $binFolder = Join-Path $binFolder "net48"
    }

    Write-Host "Copying from $binFolder to $targetDir..."
    Copy-Item -Path "$binFolder\*" -Destination $targetDir -Recurse -Force
    
    # Create .addin manifest for this version
    $manifestPath = Join-Path $targetDir "antiGGGravity.addin"
    $manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>antiGGGravity</Name>
    <FullClassName>antiGGGravity.App</FullClassName>
    <Assembly>antiGGGravity\$v\antiGGGravity.dll</Assembly>
    <AddInId>35A1E7B9-940B-4D95-8E39-9C17FD430693</AddInId>
    <VendorId>antiGGGravity</VendorId>
    <VendorDescription>antiGGGravity</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    $manifestContent | Set-Content -Path $manifestPath
}

Write-Host "Multi-version build complete. Check the Distribute folder." -ForegroundColor Green
