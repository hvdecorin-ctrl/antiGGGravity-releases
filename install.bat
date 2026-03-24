@echo off
setlocal

echo ==================================================
echo   antiGGGravity Revit Toolkit Installation
echo ==================================================
echo.

:: Detect current folder 
set "SCRIPTPATH=%~dp0"

:: Set Revit Addins Folder
set "ADDINS_FOLDER=%APPDATA%\Autodesk\Revit\Addins"

:: Confirm version with user
echo Which Revit version do you want to install?
echo (Available: 2022, 2023, 2024, 2025, 2026)
set /p REVIT_YEAR="Year (e.g. 2026): "

set "SOURCE_DIR=%SCRIPTPATH%R%REVIT_YEAR%"
set "TARGET_DIR=%ADDINS_FOLDER%\%REVIT_YEAR%"

if not exist "%SOURCE_DIR%" (
    echo.
    echo ERROR: Folder for Revit %REVIT_YEAR% not found in this package.
    pause
    exit /b 1
)

echo.
echo Installing antiGGGravity for Revit %REVIT_YEAR%...
echo Source: %SOURCE_DIR%
echo Target: %TARGET_DIR%
echo.

:: Ensure target dir exists
if not exist "%TARGET_DIR%" mkdir "%TARGET_DIR%"

:: Copy manifest (.addin) to the root of the addins folder
copy /y "%SOURCE_DIR%\antiGGGravity.addin" "%TARGET_DIR%\antiGGGravity.addin"

:: Copy the assembly subfolder
if not exist "%TARGET_DIR%\antiGGGravity" mkdir "%TARGET_DIR%\antiGGGravity"
xcopy /s /i /y "%SOURCE_DIR%\antiGGGravity\*" "%TARGET_DIR%\antiGGGravity\"

echo.
echo ✓ Installation Complete! 🚀
echo Please restart Revit to use the toolkit.
echo.
pause
