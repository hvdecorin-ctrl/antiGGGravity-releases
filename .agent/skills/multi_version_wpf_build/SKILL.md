---
name: multi_version_wpf_build
description: "Handles compiling multi-version SDK-style WPF .NET Framework (net48) and .NET Core (net8.0-windows) projects without MC1000 errors or parallel lock collisions."
---

# Multi-Version WPF Build Skill 

When compiling Revit add-ins that target multiple versions of Revit (e.g., 2022-2024 using `.NET Framework 4.8`, and 2025-2026 using `.NET 8.0-windows`) inside a single **SDK-style `.csproj`**, the `.NET SDK` compiler (especially versions 8.0 and 10.0+) often fails with XAML compilation bugs (`MC1000`).

This skill defines the precise workflow, `.csproj` structures, and scripts required to successfully build these projects on any host machine (like an Office PC) without obscure MSBuild crashes.

## 🚨 The Core Problems Addressed
1. **The `MC1000 System.Runtime NeutralResourcesLanguageAttribute` Bug**: Modern `.NET 8+` SDKs contain a bug in `PresentationBuildTasks.dll` where they fail to reflect over `.NET 4.8` facades when resolving types required by `netstandard2.0` NuGet packages (like `System.Text.Json`).
2. **Assembly Candidate Leakage**: MSBuild often scans the project directory (including `Distribute` or `bin` subfolders) and incorrectly references `.NET 8` assemblies for `.NET 4.8` builds, leading to "Assembly not found" or target framework mismatch errors.
3. **Parallel `MarkupCompilePass1` Collisions**: If your project uses `<TargetFrameworks>net8.0-windows;net48</TargetFrameworks>` (plural), invoking `dotnet build` will parallelize the WPF build, causing `error CS0016: Could not write to output file '...View.g.cs' because it is being used by another process.`
4. **Ghost `.NET 8` Payload Injection**: The intermediate WPF compiler (`wpftmp.csproj`) often falls back to `Configuration=Debug`, accidentally loading `.NET 8` dependencies into `.NET 4.8` passes.

## ✅ Step 1: Immutable `.csproj` Configuration Rules
Whenever you modify the `.csproj` for multi-version support, you **MUST** ensure the following rules are strictly followed:

1. **Never use Plural TargetFrameworks**: Instead of `<TargetFrameworks>`, use a singular conditional `<TargetFramework>` to disable concurrent build locks:
   ```xml
   <TargetFramework Condition="'$(Configuration)' == 'R22' OR '$(Configuration)' == 'R23' OR '$(Configuration)' == 'R24'">net48</TargetFramework>
   <TargetFramework Condition="'$(Configuration)' == 'R25' OR '$(Configuration)' == 'R26' OR '$(Configuration)' == 'Debug' OR '$(Configuration)' == 'Release'">net8.0-windows</TargetFramework>
   ```

2. **Always Bind External Dependencies to `TargetFramework`**: Prevent the intermediate WPF project from defaulting to `.NET 8` artifacts by strictly scoping `PackageReference` and `Reference` to `TargetFramework` **AND** `Configuration`:
   ```xml
   <!-- Correct -->
   <PackageReference Include="Autodesk.Revit.SDK.Refs.2023" Version="*" Condition="'$(TargetFramework)' == 'net48'" />
   
   <!-- Incorrect (Vulnerable to wpftmp proxy leaks) -->
   <PackageReference Include="Autodesk.Revit.SDK.Refs.2023" Version="*" Condition="'$(Configuration)' == 'R23'" />
   ```

3. **Opt-Out of Attribute Generation**: To mitigate the `NeutralResourcesLanguageAttribute` crash, always define these properties in your core `<PropertyGroup>`:
   ```xml
   <GenerateTargetFrameworkAttribute>false</GenerateTargetFrameworkAttribute>
   <GenerateAssemblyNeutralResourcesLanguageAttribute>false</GenerateAssemblyNeutralResourcesLanguageAttribute>
   <GenerateResourceUsePreserializedResources>true</GenerateResourceUsePreserializedResources>
   ```

4. **Prevent Assembly Leakage (CRITICAL)**: Override `<AssemblySearchPaths>` specifically for `.NETFramework` targets to stop MSBuild from finding incorrect version assemblies in your project directory:
   ```xml
   <PropertyGroup Condition="'$(TargetFrameworkIdentifier)' == '.NETFramework'">
     <AssemblySearchPaths>
       {CandidateAssemblyFiles};
       {HintPathFromItem};
       {TargetFrameworkDirectory};
       {RegistryPreferredFilePaths};
       {AssemblyFolders};
       {GAC};
       {RawFileName};
     </AssemblySearchPaths>
   </PropertyGroup>
   ```
   *Note: Excluding `{ProjectPathAndSolutionCalls}` and `{DirectoryOfPrimaryAssembly}` prevents it from scanning your `bin/` or `Distribute/` folders.*

5. **Explicit Core References**: Legacy WPF builds often fail to resolve basic assemblies like `System.Net.Http` or `Microsoft.CSharp` (for dynamic) without explicit inclusion:
   ```xml
   <ItemGroup Condition="'$(TargetFramework)' == 'net48'">
     <Reference Include="System" />
     <Reference Include="Microsoft.CSharp" />
     <Reference Include="System.Net.Http" />
     <Reference Include="System.Windows.Forms" />
   </ItemGroup>
   ```

## ✅ Step 2: Isolating the Environment (Office PC)
If the Office PC only has the latest broken `.NET SDKs` installed (or missing older MSBuild toolchains), the absolute most reliable method is to pin the `.NET Sdk` using `global.json` for `.net48` compile tasks, or strictly utilize `MSBuild.exe` from Visual Studio 2022.

Before deploying on the Office PC, add a `global.json` script. **Version `6.0.428`** is currently the most stable for bypassing WPF compiler bugs for Revit 2022-2024:
```json
{
  "sdk": {
    "version": "6.0.428",
    "rollForward": "latestFeature"
  }
}
```
*(Only do this if the Office PC has .NET 6 SDK available and .NET 8/10 is crashing).*

## ✅ Step 3: The Automated Build Script (`deploy_multi.ps1`)
You must use a synchronized loop script to build each configuration isolated linearly, clearing the output appropriately to avoid cross-version contamination.

Create or update `deploy_multi.ps1` with this pattern:
```powershell
param (
    [string[]]$VersionsToBuild = @("R22", "R23", "R24", "R25", "R26")
)

$distRoot = "Distribute"
if (!(Test-Path $distRoot)) { New-Item -ItemType Directory -Path $distRoot }

foreach ($v in $VersionsToBuild) {
    Write-Host "Building for $v..." -ForegroundColor Cyan
    
    # 1. Execute dotnet build specifically locking the Configuration
    dotnet build [YourProjectName].csproj -c $v
    
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build Failed for $v! Stopping." -ForegroundColor Red
        exit 1
    }

    # 2. Identify correct Output Path based on framework
    $binFolder = "bin\$v"
    $targetDir = Join-Path $distRoot ("R20" + $v.Substring(1))
    
    if ($v -in @("R25", "R26")) {
        $binFolder = Join-Path $binFolder "net8.0-windows"
    } else {
        $binFolder = Join-Path $binFolder "net48"
    }

    if (!(Test-Path $targetDir)) { New-Item -ItemType Directory -Path $targetDir }
    
    # 3. Cleanly Distribute Outputs
    Copy-Item -Path "$binFolder\*" -Destination $targetDir -Recurse -Force
}
Write-Host "Multi-version build complete." -ForegroundColor Green
```

## ✅ Step 4: Revit API Compatibility Layer
Do not use `ElementId.Value` or `ElementId.IntegerValue` directly in multi-version code. Instead, implement a compatibility extension:

```csharp
public static class RevitCompatibility {
    public static long GetIdValue(this ElementId id) {
#if REVIT2024_OR_GREATER
        return id.Value;
#else
        return id.IntegerValue;
#endif
    }
}
```

And for folder dialogs, use `#if` fallbacks:
```csharp
#if REVIT2025_OR_GREATER
    var dialog = new Microsoft.Win32.OpenFolderDialog();
#else
    using (var dialog = new System.Windows.Forms.FolderBrowserDialog()) { ... }
#endif
```

## Summary for the Agent
1. When asked to build multi-version Revit SDK projects, **verify** the `.csproj` complies with the `TargetFramework` and `AssemblySearchPaths` rules above.
2. Run `deploy_multi.ps1` to loop the configurations. 
3. If MSBuild acts buggy with `MC1000` anyway, use `global.json` to pin to `.NET SDK 6.0.428`.
