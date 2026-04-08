@echo off
setlocal enabledelayedexpansion

echo ==========================================================
echo   antiGGGravity Revit Add-in Smart Uninstaller
echo ==========================================================
echo.
echo Detecting installed antiGGGravity instances and removing...
echo.

set "UNINSTALLED_VERSIONS="
set "FOUND_INSTANCES=0"

for %%V in (2022 2023 2024 2025 2026 2027) do (
    set "REVIT_YEAR=%%V"
    set "TARGET_DIR=%AppData%\Autodesk\Revit\Addins\!REVIT_YEAR!"
    
    set "FOUND_THIS_YEAR=0"
    
    if exist "!TARGET_DIR!\antiGGGravity.addin" (
        set "FOUND_THIS_YEAR=1"
        del "!TARGET_DIR!\antiGGGravity.addin"
    )
    
    if exist "!TARGET_DIR!\antiGGGravity" (
        set "FOUND_THIS_YEAR=1"
        rd /s /q "!TARGET_DIR!\antiGGGravity"
    )
    
    if !FOUND_THIS_YEAR! EQU 1 (
        echo [-] Removed antiGGGravity from Revit !REVIT_YEAR!.
        set "UNINSTALLED_VERSIONS=!UNINSTALLED_VERSIONS! %%V"
        set "FOUND_INSTANCES=1"
    )
)

echo.
if !FOUND_INSTANCES! EQU 1 (
    echo ==========================================================
    echo   SUCCESS: Uninstalled antiGGGravity from: !UNINSTALLED_VERSIONS!
    echo ==========================================================
) else (
    echo ==========================================================
    echo   INFO: No antiGGGravity installations detected.
    echo ==========================================================
)

echo.
pause
