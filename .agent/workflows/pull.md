---
description: Pull the latest code from the remote repository to sync between Home and Office PCs
---

# Pull Latest Code

This workflow ensures your local workspace is up-to-date with the latest changes from the remote repository. Use this at the start of every session (Office/Home).

// turbo
1. **Sync with Remote**:
   ```powershell
   git fetch origin
   git pull origin master
   ```

> [!TIP]
> If you have local changes that conflict with the pull, I will automatically help you stash them, pull the latest, and then re-apply your changes.

## Git Rules
> [!IMPORTANT]
> **Never auto-commit or auto-push.** Only run `git add`, `git commit`, or `git push` when the user explicitly asks. The default workflow is: edit code → build → deploy. Git operations are manual and user-initiated only.

## Automatic Session Start
I will attempt to run this automatically whenever you mention you are starting a new session or switching PCs.
