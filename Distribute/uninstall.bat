@echo off
echo ============================================
echo   antiGGGravity Revit Add-in Uninstaller
echo ============================================
echo.

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Please run this script as Administrator.
    pause
    exit /b 1
)

set "BASE_INSTALL=C:\ProgramData\antiGGGravity"
echo [1/2] Removing program binaries ...
if exist "%BASE_INSTALL%" rmdir /S /Q "%BASE_INSTALL%"
echo       Done.

echo [2/2] Unregistering from all Revit versions ...
set "VERSIONS=2022 2023 2024 2025 2026"
setlocal enabledelayedexpansion
for %%V in (%VERSIONS%) do (
    set "ADDIN_PATH=C:\ProgramData\Autodesk\Revit\Addins\%%V\antiGGGravity.addin"
    if exist "!ADDIN_PATH!" (
        del /Q "!ADDIN_PATH!"
        echo       Removed from Revit %%V.
    ) else (
        echo       Skipped Revit %%V ^(Not found^).
    )
)

echo.
echo ============================================
echo   Uninstall complete!
echo   Please restart Revit.
echo ============================================
pause
