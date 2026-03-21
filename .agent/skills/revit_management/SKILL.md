# Revit Add-in Management Skill

This skill provides the mandatory procedures for building, testing, and deploying the antiGGGravity add-in. Agents MUST follow the correct workflow based on the task requirement.

---

## 🛠️ Workflow A: Fast Iteration (Testing)
*Use this for 95% of tasks (bug fixes, UI updates, feature testing).*
* **Requirement**: Does **NOT** require closing Revit.
* **Tool**: Revit Add-In Manager.

1. **Build**: Run `dotnet build antiGGGravity.csproj -c Debug`.
2. **DLL Path**: `c:\Users\DELL\source\repos\antiGGGravity\bin\Debug\net8.0-windows\antiGGGravity.dll`.
3. **Action**: Instruct the user to load/reload this DLL via the **Add-In Manager** inside Revit.

---

## 🚀 Workflow B: Full Deployment (Installation)
*Use this ONLY when the user asks to "install", "deploy", or "release".*
* **Requirement**: Revit **MUST** be closed.
* **Effect**: Updates the Revit Ribbon and Icons permanently.

1. **Clean Build**: 
   ```powershell
   Remove-Item -Path "bin", "obj" -Recurse -Force -ErrorAction SilentlyContinue
   dotnet build --configuration Release
   ```
2. **Update Manifest**: Ensure `antiGGGravity.addin` points to the absolute path:
   `c:\Users\DELL\source\repos\antiGGGravity\bin\Release\net8.0-windows\antiGGGravity.dll`.
3. **Deploy**: Copy `.addin` to `C:\ProgramData\Autodesk\Revit\Addins\2025\` and `2026\`.
4. **Verify**: Use `Test-Path` and check timestamps on target paths.

---

## 💻 Workflow C: Multi-PC Support (Home vs. Office)
*Use this to ensure smooth transitions when working across different computers.*

1. **Portable Paths**: The project uses **relative paths** in the `.csproj` but **absolute paths** in the `.addin` manifest for deployment. 
2. **Re-Deploy on Change**: When switching to a new PC (e.g., pulling latest code at Home):
   * Perform a **Clean Build** (Workflow B, Step 1).
   * Re-run the **Deployment** (Workflow B, Step 3) once to update the `.addin` file with the correct absolute path for the *current* machine.
3. **Environment Sync**: Ensure Revit versions (2025/2026) are installed in the same default paths (`C:\ProgramData\Autodesk\Revit\Addins\`) across all PCs for the scripts to work without modification.

## 🎨 Workflow D: UI Standards (Premium Branding)
*Use this for ALL user interface modifications (Windows, Dialogs, Toolbars).*

1. **Rule: Title Case is Mandatory.** 
   - All headers, section labels, checkboxes, and radio buttons must use **Title Case**.
   - **Incorrect**: `SELECT VIEWS`, `application scope`.
   - **Correct**: `Select Views`, `Application Scope`.
2. **Rule: Resizability & Safety.**
   - Use `PremiumWindowChrome` (10px border) and set `MinWidth`/`MinHeight` in XAML.
   - Maintain a **15px bottom-right margin** for interactive elements.
3. **Workflow**: Follow the detailed [.agent/workflows/ui_design_standard.md](file:///c:/Users/DELL/source/repos/antiGGGravity/.agent/workflows/ui_design_standard.md).

---

## 🚨 Critical Rules
* **Never Auto-Deploy**: Do not run a full deployment unless explicitly requested.
* **Verification**: Deployment is not complete without verifying the file replacement on disk.
* **Local Absolute Paths**: The `<Assembly>` tag in the `.addin` file MUST point to the current PC's repository location.
