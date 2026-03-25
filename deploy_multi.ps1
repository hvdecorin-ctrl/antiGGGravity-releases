# Deploy multi-version script
param (
    [Parameter(Mandatory=$false)]
    [object]$VersionsToBuild = @("R22", "R23", "R24", "R25", "R26")
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
    Write-Host " Building and Obfuscating for $v..." -ForegroundColor Yellow
    Write-Host "==================================================`n" -ForegroundColor Yellow

    # 1. SDK VERSION HANDLING
    $isNet8 = ($v -eq "R26" -or $v -eq "R25")
    # $globalJsonPath = "$PSScriptRoot\global.json"
    # $globalJsonBak = "$PSScriptRoot\global.json.bak"

    # if ($isNet8) {
    #     # Hide global.json so it uses the latest SDK (8.0/10.0) for .NET 8 build
    #     if (Test-Path $globalJsonPath) { Rename-Item $globalJsonPath "global.json.bak" -Force }
    # } else {
    #     # Ensure global.json is active for .NET 4.8 to avoid MC1000 bug
    #     if (Test-Path $globalJsonBak) { Rename-Item $globalJsonBak "global.json" -Force }
    # }

    # 0. RESTORE
    Write-Host "Restoring for $v..." -ForegroundColor Gray
    $msbuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
    & $msbuildPath antiGGGravity.csproj /t:Restore /p:Configuration=$v

    # 1. CLEAN & BUILD
    if ($isNet8) {
        # NET 8+ uses dotnet build perfectly
        dotnet build antiGGGravity.csproj -c $v --no-incremental -p:EmbedLicense=true
    } else {
        # NET 4.8 WPF builds have MC1000 bugs in 'dotnet build'. 
        # We use Full MSBuild.exe from Visual Studio 2022 to fix this.
        $msbuildPath = "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe"
        & $msbuildPath antiGGGravity.csproj /p:Configuration=$v /p:DeployToRevit=false /p:EmbedLicense=true /t:Clean,Build /nodeReuse:false
    }
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed for $v. Stopping script." -ForegroundColor Red
        # Restore global.json if we were hiding it
        # if (Test-Path $globalJsonBak) { Rename-Item $globalJsonBak "global.json" -Force }
        exit 1
    }

    # Restore global.json if we were hiding it
    # if (Test-Path $globalJsonBak) { Rename-Item $globalJsonBak "global.json" -Force }
    
    # 2. RESOLVE PATHS
    $folderName = "R20" + $v.Substring(1)
    $targetDir = Join-Path $distRoot $folderName
    $addonSubDir = Join-Path $targetDir "antiGGGravity" # Keep dlls in a subfolder
    
    if (!(Test-Path $addonSubDir)) { New-Item -ItemType Directory -Path $addonSubDir -Force }
    
    $inDir = "bin\$v"
    $isNet8 = ($v -eq "R26" -or $v -eq "R25")
    if ($isNet8) {
        $inDir = Join-Path $inDir "net8.0-windows"
    } else {
        $inDir = Join-Path $inDir "net48"
    }
    $inDir = Resolve-Path $inDir

    # 3. DYNAMIC OBFUSCATION
    $obfXmlPath = "$PSScriptRoot\obfuscar_temp_$v.xml"
    $obfOutDir = "$PSScriptRoot\bin\Obfuscated_$v"
    if (Test-Path $obfOutDir) { Remove-Item $obfOutDir -Recurse -Force }
    New-Item -ItemType Directory -Path $obfOutDir

    # Get Revit Paths for searching RevitAPI.dll
    $revitYear = "20" + $v.Substring(1)
    $localRevitPath = "C:\Program Files\Autodesk\Revit $revitYear"
    $nugetPackagesPath = "$env:USERPROFILE\.nuget\packages"
    
    # NEW: Recursively find the exact leaf folder for this version's RevitAPI.dll in NuGet
    $sdkRefsSearchPath = Join-Path $nugetPackagesPath "Autodesk.Revit.SDK.Refs.$revitYear"
    $resolvedSdkPath = Get-ChildItem -Path $sdkRefsSearchPath -Recurse -Filter "RevitAPI.dll" | Select-Object -First 1 -ExpandProperty DirectoryName

    $xmlContent = @"
<?xml version="1.0"?>
<Obfuscator>
  <Var name="InPath" value="$inDir" />
  <Var name="OutPath" value="$obfOutDir" />
  <Var name="HideStrings" value="true" />
  <Var name="RenameProperties" value="true" />
  <Var name="RenameEvents" value="true" />
  <Var name="RenameFields" value="true" />
  <Var name="KeepPublicApi" value="false" />
  
  <AssemblySearchPath path="$localRevitPath" />
  <AssemblySearchPath path="$resolvedSdkPath" />
  <AssemblySearchPath path="$nugetPackagesPath" />
  <AssemblySearchPath path="C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.24" />
  <AssemblySearchPath path="C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.24" />
  <AssemblySearchPath path="C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8" />
  <AssemblySearchPath path="C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades" />
  
  <Module file="`$(InPath)\antiGGGravity.dll">
    <SkipType name="antiGGGravity.App" />
    <SkipType name="antiGGGravity.Commands.*.*$" rx="true" />
    <SkipType name="antiGGGravity.StructuralRebar.*Command$" rx="true" />
    <SkipType name="antiGGGravity.Utilities.LicenseResult" skipMethods="true" skipProperties="false" />
    <SkipType name="antiGGGravity.Utilities.LicenseKeyResult" skipMethods="true" skipProperties="false" />
  </Module>
</Obfuscator>
"@
    $xmlContent | Set-Content $obfXmlPath
    
    Write-Host "Running Obfuscar for $v..." -ForegroundColor Blue
    if (Get-Command obfuscar.console -ErrorAction SilentlyContinue) {
        obfuscar.console $obfXmlPath
        
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Obfuscation failed for $v! Check search paths." -ForegroundColor Red
            # Clean up temp file but stop
            Remove-Item $obfXmlPath -ErrorAction SilentlyContinue
            exit 1
        }
        
        # 4. PACKAGE INTO DISTRIBUTE (FROM OBFUSCATED)
        Write-Host "Copying encrypted assemblies to $addonSubDir..." -ForegroundColor Green
        # Copy main DLL from obfuscated output
        Copy-Item -Path "$obfOutDir\antiGGGravity.dll" -Destination $addonSubDir -Force
    } else {
        Write-Host "WARNING: obfuscar.console not found. Skipping obfuscation for $v." -ForegroundColor Cyan
        
        # 4. PACKAGE INTO DISTRIBUTE (FROM BIN)
        Write-Host "Copying standard assemblies to $addonSubDir..." -ForegroundColor Green
        # Copy main DLL from bin folder
        Copy-Item -Path "$inDir\antiGGGravity.dll" -Destination $addonSubDir -Force
    }
    
    # Copy dependencies (not obfuscated) from bin
    Get-ChildItem -Path $inDir -Exclude "antiGGGravity.dll", "antiGGGravity.pdb" | Copy-Item -Destination $addonSubDir -Force -Recurse

    # 5. GENERATE MANIFEST (.addin)
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

    # CLEANUP TEMP
    Remove-Item $obfXmlPath -ErrorAction SilentlyContinue
}

# 6. COPY SUPPORTING FILES (INSTALL/UNINSTALL/INSTRUCUTIONS)
Write-Host "`nCopying supporting files into Distribute..." -ForegroundColor Yellow
$supportingFiles = @("install.bat", "uninstall.bat", "instructions.txt")
foreach ($file in $supportingFiles) {
    if (Test-Path "$PSScriptRoot\$file") {
        Copy-Item -Path "$PSScriptRoot\$file" -Destination $distRoot -Force
    }
}

Write-Host "`nALL VERSIONS BUILT AND ENCRYPTED SUCCESSFULLY! 🚀" -ForegroundColor Green
Write-Host "Artifacts are ready in the 'Distribute' folder." -ForegroundColor Green

