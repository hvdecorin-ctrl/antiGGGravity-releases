# End of Session Workflow

Use this workflow to safely finalize a task and synchronize changes with the repository.

## ✅ Step 1: Final Verification
1. **Clean Build**: Run `dotnet build antiGGGravity.csproj -c Debug` to ensure no errors were introduced.
2. **UI Check**: Verify any new Windows or labels follow **Title Case** and **Premium Margins**.

## 🚀 Step 2: User-Approved Deployment
1. Ask the user if they want to **"Install"** or **"Deploy"** to Revit (Workflow B in `revit_management/SKILL.md`).
2. Only run `DeployToRevit=true` if explicitly confirmed.

## 📦 Step 3: Git Synchronization
1. **NEVER** push without permission.
2. Ask the user: "Would you like to push these changes to GitHub now?"
3. If confirmed, perform:
   ```powershell
   git add .
   git commit -m "[Detailed description of changes]"
   git push
   ```

## 📄 Step 4: Summary
- Create or update the `walkthrough.md` artifact.
- Provide a concise list of modified files and features.
