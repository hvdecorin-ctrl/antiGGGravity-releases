---
description: Fully deploy the add-in to Revit (requires Revit restart)
---

# Deploy to Revit

This workflow performs a full deployment of the add-in. It ensures the manifest is portable for multi-PC use and copies the necessary files to the Revit Addins folder.

> [!IMPORTANT]
> **Multi-PC Support**: Always ensure `antiGGGravity.addin` uses a relative path for the `<Assembly>` tag (e.g., `<Assembly>antiGGGravity.dll</Assembly>`). This allows the add-in to work on different PCs without modification.

// turbo
1. **Sanitize Manifest**:
   - Verify `antiGGGravity.addin` does not contain absolute paths (e.g., `C:\Users\...`).
   - If it does, change it to just `antiGGGravity.dll`.

// turbo
2. **Deploy Build**:
   ```powershell
   dotnet build antiGGGravity.csproj -c Debug -p:DeployToRevit=true
   ```

3. **Verify Installation**:
   - Check that `antiGGGravity.addin` and `antiGGGravity.dll` are present in:
     `%AppData%\Autodesk\Revit\Addins\2026\` (or 2025)

4. **Restart Revit**:
   - Launch Revit to see the updated ribbon and commands.

> [!NOTE]
> Use this when you are finished testing and want to update the permanent installation of the tool.
