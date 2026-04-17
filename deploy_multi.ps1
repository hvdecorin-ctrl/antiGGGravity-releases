# Deploy multi-version script
# Builds antiGGGravity for all Revit versions and packages for client distribution.
# 
# IMPORTANT: Do NOT enable Obfuscar obfuscation. It breaks the ribbon at runtime
# because it renames properties needed for YAML deserialization and corrupts
# string constants used for resource loading. The EMBED_LICENSE build flag
# already provides client protection by bypassing license checks.
#
# Usage:
#   .\deploy_multi.ps1                          # Build all versions
#   .\deploy_multi.ps1 -VersionsToBuild "R26"   # Build specific version

param (
    [Parameter(Mandatory=$false)]
    [object]$VersionsToBuild = @("R22", "R23", "R24", "R25", "R26", "R27")
)

# Normalize input if it comes in as a single string with spaces or commas
if ($VersionsToBuild -is [string]) {
    $VersionsToBuild = $VersionsToBuild -split '[\s,]+' | Where-Object { $_ -ne "" }
}

$distRoot = "Distribute"
Write-Host "Cleaning up previous build artifacts..." -ForegroundColor Gray
Remove-Item -Path $distRoot, "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue
if (!(Test-Path $distRoot)) { New-Item -ItemType Directory -Path $distRoot }

foreach ($v in $VersionsToBuild) {
    Write-Host "`n==================================================" -ForegroundColor Yellow
    Write-Host " Building $v..." -ForegroundColor Yellow
    Write-Host "==================================================`n" -ForegroundColor Yellow

    # Cleanup obj for every version to prevent cross-framework contamination
    if (Test-Path "obj") { Remove-Item "obj" -Recurse -Force -ErrorAction SilentlyContinue }

    # 1. SDK VERSION HANDLING
    $isNetCore = ($v -eq "R27" -or $v -eq "R26" -or $v -eq "R25")
    $globalJsonPath = Join-Path $PSScriptRoot "global.json"
    
    # Choose project file (Legacy for R22-R24)
    $projFile = "antiGGGravity.csproj"
    if (!$isNetCore) {
        $projFile = "antiGGGravity_Legacy.csproj"
        
        # Force SDK 6.0.428 for .NET Framework targets (fixes MC1000 bug in newer SDKs)
        $json = '{ "sdk": { "version": "6.0.428", "rollForward": "latestFeature" } }'
        $json | Set-Content -Path $globalJsonPath -Force
    } else {
        # Remove pin for .NET Core targets
        if (Test-Path $globalJsonPath) { Remove-Item $globalJsonPath -Force }
    }

    # 2. RESTORE
    Write-Host "Restoring for $v using $projFile..." -ForegroundColor Gray
    
    # Robustly find MSBuild 2022 path (Professional or Community)
    $msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe"
    if (!(Test-Path $msbuildPath)) {
        $msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
    }
    
    & $msbuildPath $projFile /t:Restore /p:Configuration=$v /p:EmbedLicense=true

    # 3. CLEAN & BUILD
    if ($isNetCore) {
        # NET 8+ / NET 10 uses dotnet build
        dotnet build $projFile -c $v --no-incremental -p:EmbedLicense=true
    } else {
        # NET 4.8 WPF builds have MC1000 bugs in 'dotnet build'. 
        # We use Full MSBuild.exe from Visual Studio + Legacy Project to fix this.
        & $msbuildPath $projFile /p:Configuration=$v /p:DeployToRevit=false /p:EmbedLicense=true /t:Clean,Build /nodeReuse:false
    }
    
    # Cleanup global.json after use
    if (Test-Path $globalJsonPath) { Remove-Item $globalJsonPath -Force }

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $v. Stopping script." -ForegroundColor Red
        exit 1
    }

    # 4. RESOLVE PATHS
    $folderName = "R20" + $v.Substring(1)
    $targetDir = Join-Path $distRoot $folderName
    $addonSubDir = Join-Path $targetDir "antiGGGravity"
    
    if (!(Test-Path $addonSubDir)) { New-Item -ItemType Directory -Path $addonSubDir -Force }
    
    $inDir = "bin\$v"
    $isNet8 = ($v -eq "R26" -or $v -eq "R25")
    $isNet10 = ($v -eq "R27")
    if ($isNet10) {
        $inDir = Join-Path $inDir "net10.0-windows"
    } elseif ($isNet8) {
        $inDir = Join-Path $inDir "net8.0-windows"
    } else {
        $inDir = Join-Path $inDir "net48"
    }
    $inDir = Resolve-Path $inDir

    # 5. COPY ASSEMBLIES (no obfuscation — see header comment)
    Write-Host "Copying assemblies to $addonSubDir..." -ForegroundColor Green
    Copy-Item -Path "$inDir\antiGGGravity.dll" -Destination $addonSubDir -Force
    
    # Copy dependencies from bin (exclude PDB debug symbols)
    Get-ChildItem -Path $inDir -Exclude "antiGGGravity.dll", "antiGGGravity.pdb" | Copy-Item -Destination $addonSubDir -Force -Recurse

    # 6. GENERATE MANIFEST (.addin)
    $manifestPath = Join-Path $targetDir "antiGGGravity.addin"
    $manifestContent = @"
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>antiGGGravity</Name>
    <FullClassName>antiGGGravity.App</FullClassName>
    <Assembly>antiGGGravity\antiGGGravity.dll</Assembly>
    <AddInId>35A1E7B9-940B-4D95-8E39-9C17FD430693</AddInId>
    <VendorId>antiGGGravity</VendorId>
    <VendorDescription>antiGGGravity</VendorDescription>
  </AddIn>
</RevitAddIns>
"@
    $manifestContent | Set-Content -Path $manifestPath -Encoding utf8
}

# 7. COPY SUPPORTING FILES
Write-Host "`nCopying supporting files into Distribute..." -ForegroundColor Yellow
$supportingFiles = @("install.bat", "uninstall.bat", "instructions.txt")
foreach ($file in $supportingFiles) {
    if (Test-Path "$PSScriptRoot\$file") {
        Copy-Item -Path "$PSScriptRoot\$file" -Destination $distRoot -Force
    }
}

# 8. CREATE ZIP PACKAGE
Write-Host "`nCreating zip package for client..." -ForegroundColor Yellow
$zipPath = "$PSScriptRoot\antiGGGravity_Installer_AllVersions.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$distRoot\*" -DestinationPath $zipPath -Force

Write-Host "`nALL VERSIONS BUILT SUCCESSFULLY! 🚀" -ForegroundColor Green
Write-Host "Artifacts are ready in the 'Distribute' folder." -ForegroundColor Green
Write-Host "Client package created: $zipPath" -ForegroundColor Cyan
