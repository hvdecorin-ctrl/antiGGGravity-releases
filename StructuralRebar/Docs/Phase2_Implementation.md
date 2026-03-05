# Phase 2: Multi-Level Wall Implementation

## Overview
This phase extends the application to generate continuous reinforcement for stacked walls across multiple levels, following the same architectural pattern established in Phase 1 (Columns).

## 1. Core Logic & Discovery

### [NEW] `StructuralRebar\Core\Geometry\MultiLevelResolver.cs`
Extend the existing `MultiLevelResolver` (used for columns) or create a wall-specific method:
- Add `public static List<Wall> FindWallStack(Document doc, Wall selectedWall, double tolerance = 0.1)`
- **Logic**:
  1. Retrieve all structural walls in the project.
  2. Extract the `LocationCurve` (bottom base line) of the selected wall.
  3. Filter walls that share the same horizontal projection (XY coordinates) within a tolerance.
  4. Sort the resulting list of walls by their base elevation (bottom to top).

### [UPDATE] `StructuralRebar\Core\Calculators\LapSpliceCalculator.cs`
- Ensure tension lap splice multipliers are ready for walls. Walls typically use the same lap length logic as columns (in tension/compression) depending on design code.

## 2. Engine & Generation

### [UPDATE] `StructuralRebar\Core\Engine\RebarEngine.cs`
- Add `public void ProcessWallStack(List<Wall> wallStack, RebarRequest request)`
- **Logic**:
  - Iterate through `wallStack` from `i = 0` to `wallStack.Count - 1`.
  - For each wall:
    1. **Horizontal Bars / Boundaries / Corners (U/L)**: Generate these normally for the current level (independent per floor).
    2. **Vertical Bars**: 
       - If it's the **bottom wall** and starters are enabled (`request.EnableStarterBars`), generate starter bars projecting down into the foundation and up into the wall.
       - If it's a **middle wall** (or bottom wall w/o foundation), vertical bars start at the wall base (or at a splice offset above the slab) and extend **up into the next wall** by `LapLength`.
       - If it's the **top wall**, vertical bars terminate at the top of the wall with the requested top hook/cover.
    3. **Thickness Changes (Cranking)**: Use `ColumnContinuityCalculator.GetCrankParams` (or a wall-specific equivalent) to check if the wall above is thinner. If the outer face steps inwards, apply a 1:6 crank slope to the vertical bars at the floor level.

## 3. User Interface (UI)

### [UPDATE] `StructuralRebar\UI\Panels\WallRebarPanel.xaml`
- Add a "Multi-Level Mode" checkbox at the top, identical to the `ColumnRebarPanel`.
- Add an information box `UI_MultiLevelInfo` that displays the detected stack.
- Add "Stack Settings":
  - `Splice Position` (e.g., Code Default above slab, Mid Height).
  - `Crank Position` (Upper Wall / Lower Wall).
- Add "Starter Bars" section at the bottom (similar to columns) to allow projecting into the foundation.

### [UPDATE] `StructuralRebar\UI\Panels\WallRebarPanel.xaml.cs`
- In `GetRequest()`, populate multi-level fields:
  ```csharp
  request.MultiLevel = (UI_Check_MultiLevel.IsChecked == true);
  request.EnableStarterBars = (UI_Check_Starters.IsChecked == true);
  request.StarterDevLength = ...
  ```
- Implement `UpdateStackInfo(List<Element> selectedElements)` to query `MultiLevelResolver` when the user selects walls in Revit and display the sorted levels.

## Implementation Steps
1. **Model Discovery**: Implement `FindWallStack` in `MultiLevelResolver` and test its accuracy on walls with different thicknesses but aligned centerlines/faces.
2. **UI Updates**: Add multi-level and starter bar controls to `WallRebarPanel`. Wire them up to the `RebarRequest`.
3. **Engine Orchestration**: Write `ProcessWallStack` to loop through walls and handle horizontal/corner rebars.
4. **Vertical Continuity**: Implement the lap splice extension and cranking logic for vertical bars specifically.
5. **Testing**: Build and test in Revit on a 3-story wall stack (with and without thickness variations).
