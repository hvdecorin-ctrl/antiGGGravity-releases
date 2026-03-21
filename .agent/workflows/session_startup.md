# Session Startup Workflow

Every AI session MUST start with this mandatory scan to ensure full alignment with project rules and technical procedures.

## 🔎 Step 1: Mandatory Scans
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
