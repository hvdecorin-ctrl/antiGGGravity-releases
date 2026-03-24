@echo off
setlocal

echo ==================================================
echo   antiGGGravity Revit Toolkit Uninstallation
echo ==================================================
echo.

:: Detect current folder 
set "SCRIPTPATH=%~dp0"

:: Set Revit Addins Folder
set "ADDINS_FOLDER=%APPDATA%\Autodesk\Revit\Addins"

:: Confirm version with user
echo Which Revit version do you want to uninstall?
set /p REVIT_YEAR="Year (e.g. 2026): "

set "TARGET_DIR=%ADDINS_FOLDER%\%REVIT_YEAR%"

if not exist "%TARGET_DIR%\antiGGGravity.addin" (
    echo.
    echo ERROR: Cannot find antiGGGravity installation for Revit %REVIT_YEAR%.
    pause
    exit /b 1
)

echo.
echo Uninstalling antiGGGravity for Revit %REVIT_YEAR%...
echo Deleting from: %TARGET_DIR% ...
echo.

:: Remove the manifest
del /f /q "%TARGET_DIR%\antiGGGravity.addin"

:: Remove the assembly folder
if exist "%TARGET_DIR%\antiGGGravity" (
    rmdir /s /q "%TARGET_DIR%\antiGGGravity"
)

echo.
echo ✓ Uninstallation Complete! 
echo.
pause
