@echo off
setlocal
echo ==================================================
echo   antiGGGravity Revit Add-in Uninstaller
echo ==================================================
echo.

:CHOOSE_VERSION
echo Please select the Revit version to uninstall from:
echo 1. Revit 2022
echo 2. Revit 2023
echo 3. Revit 2024
echo 4. Revit 2025
echo 5. Revit 2026
echo.
set /p choice="Enter choice (1-5): "

if "%choice%"=="1" set REVIT_YEAR=2022
if "%choice%"=="2" set REVIT_YEAR=2023
if "%choice%"=="3" set REVIT_YEAR=2024
if "%choice%"=="4" set REVIT_YEAR=2025
if "%choice%"=="5" set REVIT_YEAR=2026

if "%REVIT_YEAR%"=="" (
    echo Invalid choice. Please try again.
    goto CHOOSE_VERSION
)

set TARGET_DIR=%AppData%\Autodesk\Revit\Addins\%REVIT_YEAR%

echo.
echo Uninstalling from Revit %REVIT_YEAR%...
echo Path: %TARGET_DIR%
echo.

if exist "%TARGET_DIR%\antiGGGravity.addin" del "%TARGET_DIR%\antiGGGravity.addin"
if exist "%TARGET_DIR%\antiGGGravity" rd /s /q "%TARGET_DIR%\antiGGGravity"

echo.
echo Successfully uninstalled antiGGGravity from Revit %REVIT_YEAR%.
pause
