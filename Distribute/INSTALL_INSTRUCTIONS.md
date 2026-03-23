# antiGGGravity Revit Add-in Installation

This folder contains the automated installer and compiled binaries for the **antiGGGravity** Revit add-in supporting multiple versions.

## How to Install (Automated)

1. Ensure Revit is closed.
2. Right-click on `install.bat` and select **"Run as administrator"**.
3. The script will automatically scan your `C:\ProgramData\Autodesk\Revit\Addins\` directory to detect which versions of Revit (2022 through 2026) are installed.
4. It will copy the respective `antiGGGravity.dll` assemblies to a safe, centralized `C:\ProgramData\antiGGGravity\[Version]` local folder.
5. It will magically configure the `.addin` manifest manifests and place them in the correct Revit Addins folder, mapped directly to the local assembly files.
6. Restart Revit. The add-in will be available in your Ribbon.

## How to Uninstall

1. Close Revit.
2. Right-click on `uninstall.bat` and select **"Run as administrator"**.
3. It will scan and permanently remove the `.addin` manifests from your Revit Addins folder and delete the `C:\ProgramData\antiGGGravity` directory securely.

## Manual Installation (If required)

If you prefer not to use the automated `install.bat` script, you can install the add-in manually:

1. Look in the respective version folders inside this directory:
   - `R2022` 
   - `R2023`
   - `R2024`
   - `R2025`
   - `R2026`
2. Inside each folder, you will find `antiGGGravity.addin` and the program files (e.g. `antiGGGravity.dll`).
3. Open your Revit Add-ins folder for the desired version. Usually located at:
   `C:\ProgramData\Autodesk\Revit\Addins\[VersionYear]\`
   *(Or for a specific user: `%AppData%\Autodesk\Revit\Addins\[VersionYear]\`)*
4. Copy the `.addin` file and its DLL contents into that folder.
5. Open the `.addin` file in Notepad and modify the `<Assembly>` tag path to securely point to exactly where you placed the `.dll` on your system.
