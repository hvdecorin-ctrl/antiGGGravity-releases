---
description: Build project for testing with Revit Add-In Manager
---

# Build for Add-In Manager

This workflow builds the project in **Debug** mode so you can test changes immediately using the Revit Add-In Manager without restarting Revit.

// turbo
1. **Build Project**:
   ```powershell
   dotnet build antiGGGravity.csproj -c Debug
   ```

2. **Load in Revit**:
   - Open Revit.
   - Go to **Add-ins > Add-In Manager (Manual)**.
   - Click **Add** and navigate to:
     `c:\Users\duy\Documents\Tool Development\Revit Addin Development\antiGGGravity\bin\Debug\net8.0-windows\antiGGGravity.dll`
   - Select your command and click **Run**.

> [!TIP]
> This build will succeed even if Revit is open, as the DLL in the `bin` folder is not locked by Revit (Add-In Manager loads it into memory).
