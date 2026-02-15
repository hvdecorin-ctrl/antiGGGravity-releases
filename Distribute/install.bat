@echo off
echo ============================================
echo   antiGGGravity Revit Add-in Installer
echo ============================================
echo.

:: Set install directory
set INSTALL_DIR=C:\antiGGGravity
set ADDIN_2025=C:\ProgramData\Autodesk\Revit\Addins\2025
set ADDIN_2026=C:\ProgramData\Autodesk\Revit\Addins\2026

:: Check for admin rights (needed for ProgramData)
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo [!] Please run this script as Administrator.
    echo     Right-click install.bat and select "Run as administrator"
    pause
    exit /b 1
)

:: Create install directory
echo [1/3] Installing add-in files to %INSTALL_DIR% ...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
xcopy /E /Y /Q "%~dp0addin\*" "%INSTALL_DIR%\" >nul
echo       Done.

:: Deploy .addin manifest to Revit 2025
echo [2/3] Registering with Revit 2025 ...
if exist "%ADDIN_2025%" (
    copy /Y "%~dp0antiGGGravity.addin" "%ADDIN_2025%\" >nul
    echo       Done.
) else (
    echo       Skipped - Revit 2025 not found.
)

:: Deploy .addin manifest to Revit 2026
echo [3/3] Registering with Revit 2026 ...
if exist "%ADDIN_2026%" (
    copy /Y "%~dp0antiGGGravity.addin" "%ADDIN_2026%\" >nul
    echo       Done.
) else (
    echo       Skipped - Revit 2026 not found.
)

echo.
echo ============================================
echo   Installation complete!
echo   Please restart Revit to load the add-in.
echo ============================================
pause
