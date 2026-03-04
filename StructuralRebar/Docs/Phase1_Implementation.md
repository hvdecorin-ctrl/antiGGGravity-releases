# Implementation Plan: Phase 1 — Multi-Level Column Rebar

## Goal

Extend the Column rebar tool to reinforce a **stack of columns across multiple levels** with a single Generate operation. Vertical bars auto-splice between levels. Starter bars optionally project into the foundation. Cross-section preview shows live bar arrangement.

---

## Proposed Changes

### DTO Layer

#### [MODIFY] [RebarRequest.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/DTO/RebarRequest.cs)

Add new properties:

```csharp
// === MULTI-LEVEL ===
public bool MultiLevel { get; set; }

// === STARTER BARS ===
public bool EnableStarterBars { get; set; }
public string StarterBarTypeName { get; set; }
public string StarterHookEndName { get; set; }
public double StarterDevLength { get; set; }  // feet

// === SPLICE ===
public string SplicePosition { get; set; } = "Above Slab";  // "Above Slab" or "Mid Height"
```

---

### Multi-Level Discovery

#### [NEW] [MultiLevelResolver.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/Geometry/MultiLevelResolver.cs)

Static class with:

- `FindColumnStack(Document doc, FamilyInstance column) → List<FamilyInstance>` 
  - Collects all structural columns in the document
  - Filters to columns whose **XY center** is within tolerance (50mm) of the given column
  - Sorts by **base elevation** (ascending — bottom level first)
  - Returns the ordered stack including the input column

**Algorithm:**
```
1. Get input column's XY center from Transform.Origin (flatten Z)
2. Collect all BuiltInCategory.OST_StructuralColumns
3. For each candidate: get XY center, check distance < tolerance
4. Sort matches by BoundingBox.Min.Z ascending
5. Return sorted list
```

---

### Continuity Calculator

#### [NEW] [ColumnContinuityCalculator.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/Calculators/ColumnContinuityCalculator.cs)

Static class with:

- `GetStarterBarLength(barDia, code) → double`
  - Uses `LapSpliceCalculator.CalculateCompressionLapLength()` (column bars in compression)
  - Returns development length in feet

- `GetSpliceOffsetAboveSlab(barDia, code) → double`
  - Standard practice: splice starts ~50mm above floor slab level
  - Splice length = compression lap from `LapSpliceCalculator`
  - Returns offset in feet from column base

- `GetCrankParams(lowerWidth, upperWidth, barDia) → (offset, run, needed)`
  - If upper column is narrower: `offset = (lowerWidth - upperWidth) / 2 - cover difference`
  - `run = 6 × barDia` (1:6 slope, reuse `LapSpliceCalculator.GetCrankRun`)
  - `needed = offset > barDia` (only crank if meaningful offset)

---

### Engine Updates

#### [MODIFY] [RebarEngine.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/Engine/RebarEngine.cs)

**New public method:**

```csharp
public (int Processed, int Total) GenerateColumnStackRebar(
    List<FamilyInstance> stack, RebarRequest request)
```
- Wraps all work in a single transaction "Generate Multi-Level Column Rebar"
- Calls `ProcessColumnStack(stack, request)`

**New private method:**

```csharp
private bool ProcessColumnStack(List<FamilyInstance> stack, RebarRequest request)
```

Logic:
```
for i = 0 to stack.Count - 1:
    column = stack[i]
    host = ColumnGeometryModule.Read(column)
    
    // 1. Delete existing rebar if requested
    if request.RemoveExisting: DeleteExistingRebar(column)
    
    // 2. Ties — reuse existing ProcessColumn tie logic
    GenerateTies(column, host, request, definitions)
    
    // 3. Vertical bars
    if i == 0 (bottom column):
        - Standard vertical bars
        - If EnableStarterBars: add starter bar definitions extending below column base
        - Top of bar: extend splice length into column above (if exists)
    
    if i == middle columns:
        - Vertical bars starting at splice offset above base
        - Top: extend splice length into column above
        - Crank if cross-section changes from column below
    
    if i == last (top column):
        - Vertical bars starting at splice offset above base
        - Top: standard termination (hook or extension as configured)
        - Crank if cross-section changes from column below
    
    // 4. Place rebar
    PlaceRebar(column, definitions)
```

> [!IMPORTANT]
> Each column's rebar is hosted on its own `FamilyInstance` element. Bars that *project* beyond the column (starter bars, splice extensions) are created as part of that column's rebar set but with curves extending past the host boundaries.

---

### UI Updates

#### [MODIFY] [ColumnRebarPanel.xaml](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/UI/Panels/ColumnRebarPanel.xaml)

Add three new sections to the existing XAML:

**1. Top — Multi-Level Toggle + Stack Info Card**
```xml
<!-- Multi-Level Mode (above existing content) -->
<CheckBox x:Name="UI_Check_MultiLevel" Content="Multi-Level Mode" />

<Border x:Name="UI_MultiLevelInfo" Background="#EDF6FF" 
        BorderBrush="#C8DFF0" Visibility="Collapsed">
    <StackPanel>
        <TextBlock x:Name="UI_StackInfo" Text="Select columns to detect stack"/>
        <ComboBox x:Name="UI_Combo_SplicePos">
            <ComboBoxItem Content="Above Slab (Code Default)" IsSelected="True"/>
            <ComboBoxItem Content="Mid Height"/>
        </ComboBox>
    </StackPanel>
</Border>
```

**2. Center — Cross-Section Preview**

Between the two existing panels, add a `Border` containing an `Image` control that renders via a WPF `DrawingVisual`:
```xml
<Border Background="#FAFAFA" BorderBrush="#EEEEEE" CornerRadius="8" Padding="10">
    <StackPanel>
        <TextBlock Text="Cross Section" FontWeight="Bold"/>
        <Image x:Name="UI_CrossSection" Height="160" Stretch="Uniform"/>
        <TextBlock x:Name="UI_CrossSectionLabel" Text="3×3 · 600×600" 
                   HorizontalAlignment="Center"/>
    </StackPanel>
</Border>
```

**3. Bottom — Starter Bars Card**
```xml
<Border Background="#FAFAFA" BorderBrush="#EEEEEE" CornerRadius="8" Padding="15">
    <StackPanel>
        <CheckBox x:Name="UI_Check_Starters" Content="Enable Starter Bars (into Foundation)"/>
        <Grid Visibility="{Binding IsChecked, ElementName=UI_Check_Starters, ...}">
            <!-- Bar Type ComboBox | Hook End ComboBox | Dev Length TextBox -->
        </Grid>
    </StackPanel>
</Border>
```

**Layout change:** Current 2-column grid becomes 3-column: `Left | Center(preview) | Right`

#### [MODIFY] [ColumnRebarPanel.xaml.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/UI/Panels/ColumnRebarPanel.xaml.cs)

- `LoadData()` — populate starter bar type and hook combos
- `LoadSettings()` / `SaveSettings()` — persist MultiLevel, starter bar settings
- `GetRequest()` — populate new `RebarRequest` fields
- `DrawCrossSection(countX, countY)` — new method using `DrawingVisual` to render rebar dots + tie outline
- `UpdateStackInfo(List<string> levelInfo)` — new method to fill the info card text
- Wire `TextChanged` events on count fields to trigger `DrawCrossSection()` refresh

---

### Handler Updates

#### [MODIFY] [RebarGenerateHandler.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/RebarGenerateHandler.cs)

Update `ProcessColumns()`:

```csharp
if (request.MultiLevel)
{
    // User selects ONE column, resolve the full stack
    var stack = MultiLevelResolver.FindColumnStack(doc, columns.First());
    var (processed, total) = engine.GenerateColumnStackRebar(stack, request);
    return $"Successfully reinforced {processed} columns across {total} levels.";
}
else
{
    // Existing single-column flow (unchanged)
    var (processed, total) = engine.GenerateColumnRebar(columns, request);
    return $"Successfully reinforced {processed} of {total} columns.";
}
```

---

## File Summary

| File | Action | Key Changes |
|------|--------|-------------|
| [RebarRequest.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/DTO/RebarRequest.cs) | MODIFY | +6 properties (MultiLevel, starter bars, splice) |
| [MultiLevelResolver.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/Geometry/MultiLevelResolver.cs) | NEW | Column stack discovery by XY position |
| [ColumnContinuityCalculator.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/Calculators/ColumnContinuityCalculator.cs) | NEW | Starter length, splice offset, crank params |
| [RebarEngine.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/Engine/RebarEngine.cs) | MODIFY | +`GenerateColumnStackRebar()`, +`ProcessColumnStack()` |
| [ColumnRebarPanel.xaml](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/UI/Panels/ColumnRebarPanel.xaml) | MODIFY | +Multi-level section, +cross-section preview, +starter bars |
| [ColumnRebarPanel.xaml.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/UI/Panels/ColumnRebarPanel.xaml.cs) | MODIFY | +DrawCrossSection(), +GetRequest() updates, +settings |
| [RebarGenerateHandler.cs](file:///c:/Users/DELL/source/repos/antiGGGravity/StructuralRebar/Core/RebarGenerateHandler.cs) | MODIFY | +Multi-level branch in ProcessColumns() |

---

## Verification Plan

### Build
- Run `/build` workflow to compile the add-in

### Manual Revit Testing (4 scenarios)

> [!IMPORTANT]
> These tests require a Revit model with structural columns placed across multiple levels. Please confirm you have (or can quickly create) a test model with:
> - 3+ levels (e.g., Ground, Level 1, Level 2)
> - Columns at the same grid position stacked across levels
> - Ideally: at least one stack where the upper column is narrower than the lower
> - A structural foundation below the bottom column

**Test 1 — Single Column (No Regression)**
1. Open Rebar Suite → Column → leave Multi-Level **unchecked**
2. Generate rebar on a single column
3. ✅ Verify: rebar matches current behavior exactly

**Test 2 — Multi-Level Stack (3 levels)**
1. ☑ Multi-Level Mode → select one column from a 3-level stack
2. Generate
3. ✅ Verify: all 3 columns get rebar. Vertical bars splice between levels.

**Test 3 — Starter Bars**
1. ☑ Multi-Level + ☑ Starter Bars → configure bar type + hook
2. Generate
3. ✅ Verify: bottom column has bars extending below into foundation

**Test 4 — Cross Section Preview**
1. Change Count X and Count Y values
2. ✅ Verify: cross-section drawing updates to show correct bar positions

**Test 5 — Size Change Between Levels**
1. Set up stack where L1 is 600×600 and L2 is 400×400
2. Generate with Multi-Level
3. ✅ Verify: bars crank at the transition between levels
