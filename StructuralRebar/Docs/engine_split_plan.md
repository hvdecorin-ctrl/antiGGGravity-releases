# Rebar Engine Modularization Plan (Revised)

> [!IMPORTANT]
> The primary goal of splitting the rebar engine is to minimize changes to established logic and ensure that all tools remain perfectly stable throughout the process.

This plan outlines the "one-by-one" extraction of rebar logic from the monolithic `RebarEngine.cs` into isolated classes, following your specific priority order.

## 1. Bored Pile (TEMPLATE)
- **Status**: [ ] Next Session
- **Files**: `BoredPileEngine.cs`
- **Goal**: Extract the current "perfect" bored pile logic and create the shared `IRebarEngine` interface.

## 2. Foundation
- **Status**: [ ] Planned
- **Files**: `FootingEngine.cs`, `PadShapeEngine.cs`
- **Host Types**: `FootingPad`, `StripFooting`, `PadShape`

## 3. Column
- **Status**: [ ] Planned
- **Files**: `ColumnEngine.cs`
- **Host Types**: `Column` (Vertical and Slanted/LCS)

## 4. Wall
- **Status**: [ ] Planned
- **Files**: `WallEngine.cs`, `WallCornerEngine.cs`
- **Host Types**: `Wall`, `WallCornerL`, `WallCornerU`

## 5. Beam
- **Status**: [ ] Planned
- **Files**: `BeamEngine.cs`
- **Host Types**: `Beam`, `BeamAdvance`

---
**Core Benefit**: Each engine will be physically isolated in its own file. Editing the `ColumnEngine` logic will have **zero** risk of affecting `Bored Pile` or `Wall` code.
