@echo off
setlocal
echo ==================================================
echo   antiGGGravity Revit Add-in Installer
echo ==================================================
echo.

:CHOOSE_VERSION
echo Please select the Revit version to install for:
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

set SOURCE_DIR=%~dp0R%REVIT_YEAR%
set TARGET_DIR=%AppData%\Autodesk\Revit\Addins\%REVIT_YEAR%

if not exist "%SOURCE_DIR%" (
    echo Error: Source folder %SOURCE_DIR% not found.
    echo Please ensure you are running this from the distribution root.
    pause
    exit /b 1
)

echo.
echo Installing for Revit %REVIT_YEAR%...
echo Source: %SOURCE_DIR%
echo Target: %TARGET_DIR%
echo.

if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

xcopy "%SOURCE_DIR%\*" "%TARGET_DIR%" /s /e /y /i

if %ERRORLEVEL% EQU 0 (
    echo.
    echo Successfully installed antiGGGravity for Revit %REVIT_YEAR%! 🚀
    echo Please restart Revit to see the new tools.
) else (
    echo.
    echo Installation failed. Error code: %ERRORLEVEL%
)

pause
