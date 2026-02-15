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

echo [1/3] Removing add-in files ...
if exist "C:\antiGGGravity" rmdir /S /Q "C:\antiGGGravity"
echo       Done.

echo [2/3] Unregistering from Revit 2025 ...
if exist "C:\ProgramData\Autodesk\Revit\Addins\2025\antiGGGravity.addin" (
    del "C:\ProgramData\Autodesk\Revit\Addins\2025\antiGGGravity.addin"
    echo       Done.
) else (
    echo       Skipped - not found.
)

echo [3/3] Unregistering from Revit 2026 ...
if exist "C:\ProgramData\Autodesk\Revit\Addins\2026\antiGGGravity.addin" (
    del "C:\ProgramData\Autodesk\Revit\Addins\2026\antiGGGravity.addin"
    echo       Done.
) else (
    echo       Skipped - not found.
)

echo.
echo ============================================
echo   Uninstall complete!
echo   Please restart Revit.
echo ============================================
pause
