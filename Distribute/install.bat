@echo off
setlocal enabledelayedexpansion
echo ============================================
echo   antiGGGravity Revit Add-in Multi-Installer
echo ============================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Please run this script as Administrator.
    echo     Right-click and select "Run as administrator"
    pause
    exit /b 1
)

set "BASE_INSTALL=C:\ProgramData\antiGGGravity"
if not exist "%BASE_INSTALL%" mkdir "%BASE_INSTALL%"

:: Versions to check
set "VERSIONS=2022 2023 2024 2025 2026"

for %%V in (%VERSIONS%) do (
    echo.
    echo [*] Checking Revit %%V ...
    set "REVIT_ADDIN_PATH=C:\ProgramData\Autodesk\Revit\Addins\%%V"
    
    if exist "!REVIT_ADDIN_PATH!" (
        echo     Revit %%V found. Deploying...
        
        set "TARGET_DIR=%BASE_INSTALL%\%%V"
        if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"
        
        :: Copy version-specific binaries
        xcopy /E /Y /Q "%~dp0R%%V\*" "!TARGET_DIR!\" >nul
        
        :: Copy and Patch Manifest
        set "MANIFEST_SRC=!TARGET_DIR!\antiGGGravity.addin"
        set "MANIFEST_DEST=!REVIT_ADDIN_PATH!\antiGGGravity.addin"
        
        if exist "!MANIFEST_SRC!" (
            copy /Y "!MANIFEST_SRC!" "!MANIFEST_DEST!" >nul
            
            :: Update Assembly path in .addin to absolute path
            powershell -Command "(Get-Content '!MANIFEST_DEST!') -replace '<Assembly>.*</Assembly>', '<Assembly>!TARGET_DIR!\antiGGGravity.dll</Assembly>' | Set-Content '!MANIFEST_DEST!'"
            echo     Successfully installed into Revit %%V.
        ) else (
            echo     Error: Manifest not found in %~dp0R%%V
        )
    ) else (
        echo     Revit %%V not installed ^(Dir not found^).
    )
)

echo.
echo ============================================
echo   Multi-Version Installation Complete!
echo   Supported Versions: 2022 - 2026
echo ============================================
pause
