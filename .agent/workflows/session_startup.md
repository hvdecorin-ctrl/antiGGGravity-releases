# Session Startup Workflow

Every AI session MUST start with this mandatory scan to ensure full alignment with project rules and technical procedures.

## 🔎 Step 1: Context-Aware Verification (FAST SYNC)
1. **Check History**: Look back at the conversation. **If you see that a previous agent has already performed a successful "Session Startup Scan"** in this specific chat history, you may skip reading the full files. 
2. **Quick Verify**: Simply run `list_dir` on the folders below to ensure no new files were added since the last scan.
3. **ONLY FULL SCAN IF**: This is a direct cold start (no previous history of a scan) or if you see significant file changes in the repository.

## 🔎 Step 2: Mandatory Scans (COLD START)
Before carrying out any task, the agent MUST read the following files:

1. **[.cursorrules](file:///c:/Users/DELL/source/repos/antiGGGravity/.cursorrules)**: Critical project-wide rules and deployment policies.
2. **[.agent/skills/**/SKILL.md](file:///c:/Users/DELL/source/repos/antiGGGravity/.agent/skills/)**: All specialized technical procedures (e.g., Revit Management).
3. **[.agent/workflows/*.md](file:///c:/Users/DELL/source/repos/antiGGGravity/.agent/workflows/)**: Task-specific standardized workflows.

## 🛠️ Step 2: Environment Sync
- Confirm the current OS and Revit versions being targeted (default R26).
- Use `dotnet build -c Debug` to verify the initial state without locking deployment binaries.

## 📝 Step 3: Initialization
- Acknowledge that the rules have been read.
- Propose an `implementation_plan.md` for any complex task before writing code.
