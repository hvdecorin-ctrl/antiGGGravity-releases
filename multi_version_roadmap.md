# Master Roadmap: Multi-Revit Version Support (2022-2026)

This roadmap outlines the transition of the entire `antiGGGravity` toolkit to a professional multi-targeting architecture. This allows you to maintain **one codebase** for all Revit versions, which is essential for the Autodesk App Store.

## 1. Technical Architecture

### Unified Project File
We will replace the current `antiGGGravity.csproj` with a multi-targeted SDK-style project.

**Core Settings:**
- **TargetFrameworks**: `net8.0-windows;net48` (Supports both Modern and Classic .NET)
- **Configurations**: `R22;R24;R26` (The three "Boundary" versions)
- **Output Folders**: Each version builds to its own subfolder (e.g., `bin/Release/R22`).

## 2. Scalability Strategy (For 50+ Commands)

To avoid a "huge workload" when managing 50 or more commands, we use the **Global Compatibility Layer** instead of fixing every file individually.

### The Compatibility Layer (`RevitCompatibility.cs`)
We create a central "mapping" file that translates old Revit terminology to new Revit terminology.
- **Goal**: Fix the problem once in the central file, and all 50 commands are fixed instantly.
- **Key Focus**: Handle `ElementId` (Value vs IntegerValue) and `Units` (ForgeTypeId vs DisplayUnitType) globally.

### The "Boundary" Version Method
You do not need to test every version. You only need to ensure the code compiles for these three specific versions:
1.  **Revit 2026**: Tests the Latest API + Modern .NET 8.
2.  **Revit 2024**: Tests the "Bridge" API (New ElementIds) + Classic .NET 4.8.
3.  **Revit 2022**: Tests the "Legacy" API (Old Units/ElementIds) + Classic .NET 4.8.

## 3. Code Adaptation Strategy

### Smart Code Branching
Instead of duplicating files, we use `#if` directives.
- **Example**: `ElementId` handling.
```csharp
#if REVIT2024_OR_GREATER
    long value = element.Id.Value;
#else
    long value = element.Id.IntegerValue;
#endif
```

## 4. Deployment & Marketplace Structure

To support the Autodesk Store, the final package will look like this:
```text
antiGGGravity.bundle/
  Contents/
    antiGGGravity.addin (Global manifest)
    2022/ (Build from net48)
      antiGGGravity.dll
    2024/
      antiGGGravity.dll
    2026/ (Build from net8.0)
      antiGGGravity.dll
```

## 5. Efficient 80/20 Workflow

1.  **Phase 1: 80% Infrastructure (Setup Once)**
    - Convert `.csproj` to multi-target.
    - Create the `RevitCompatibility.cs` layer.
    - Result: Most of the 50 tools will compile immediately.
2.  **Phase 2: 20% Specific Fixes (Refinement)**
    - Fix only the specific commands that use very rare or complex API features.
3.  **Phase 3: Born Compatible**
    - From this point on, every NEW tool you create is automatically checked against 2022 and 2026 as you type.

## 6. Benefits for Autodesk Store
- **Single Submission**: You submit one package that works for everyone.
- **Easy Maintenance**: Bug fixes apply to all versions instantly.
- **Professionalism**: Matches the architecture of major Revit add-in providers.
