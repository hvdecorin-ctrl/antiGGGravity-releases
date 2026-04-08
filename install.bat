@echo off
setlocal enabledelayedexpansion

echo ==========================================================
echo   antiGGGravity Revit Add-in Smart Installer
echo ==========================================================
echo.
echo Detecting installed Revit versions and installing...
echo.

set "INSTALLED_VERSIONS="
set "FOUND_REBIT=0"

for %%V in (2022 2023 2024 2025 2026 2027) do (
    set "REVIT_YEAR=%%V"
    set "SOURCE_DIR=%~dp0R!REVIT_YEAR!"
    set "TARGET_DIR=%AppData%\Autodesk\Revit\Addins\!REVIT_YEAR!"
    
    REM Check if Revit folder exists in AppData (indicating it's installed or was installed)
    if exist "!TARGET_DIR!\.." (
        REM Check if our distribution folder for this version exists
        if exist "!SOURCE_DIR!" (
            echo [+] Installing for Revit !REVIT_YEAR!...
            
            if not exist "!TARGET_DIR!" mkdir "!TARGET_DIR!"
            
            xcopy "!SOURCE_DIR!\*" "!TARGET_DIR!" /s /e /y /i /q >nul
            
            if !ERRORLEVEL! EQU 0 (
                set "INSTALLED_VERSIONS=!INSTALLED_VERSIONS! %%V"
                set "FOUND_REBIT=1"
            ) else (
                echo [X] Failed to install for Revit !REVIT_YEAR!.
            )
        )
    )
)

echo.
if !FOUND_REBIT! EQU 1 (
    echo ==========================================================
    echo   SUCCESS: Installed antiGGGravity for: !INSTALLED_VERSIONS!
    echo ==========================================================
    echo.
    echo Please RESTART Revit to see the new tools.
) else (
    echo ==========================================================
    echo   ERROR: No compatible Revit versions detected.
    echo ==========================================================
    echo.
    echo Please ensure Revit 2022-2027 is installed.
)

echo.
pause
