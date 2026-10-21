# antiGGGravity — Professional BIM Toolkit for Revit
![Banner](https://raw.githubusercontent.com/hvdecorin-ctrl/antiGGGravity/main/Resources/banner.png)

> **The definitive command guide and functional documentation for the antiGGGravity BIM Toolkit.**
> Compatible with Revit 2022, 2023, 2024, 2025, 2026, and 2027.

---

## 📑 Table of Contents
1. [Introduction](#-introduction)
2. [Structural Rebar Panel](#-structural-rebar-panel)
3. [Structural Model Panel](#-structural-model-panel)
4. [Project Audit Panel](#-project-audit-panel)
5. [Graphic Overrides Panel](#-graphic-overrides-panel)
6. [Management Panel](#-management-panel)
7. [Visibility & Graphics Panel](#-visibility--graphics-panel)
8. [General Productivity Panel](#-general-productivity-panel)
9. [Installation & Licensing](#-installation--licensing)

---

## 🚀 Introduction
**antiGGGravity** is a high-performance productivity suite designed by structural engineers for the AEC industry. It automates the "low-value" repetitive tasks that consume up to 40% of a BIM professional's day, allowing you to focus on engineering and design.

With **160+ specialized commands**, this toolkit bridges the gap between Revit's native functionality and the speed required for large-scale commercial project delivery.

**Headline Workflows**
- **3D Rebar Model to Shop Drawing in 1 Click** — Beam, Column, Wall and Foundation shop drawings with tags, bar bending schedules and mass summaries.
- **Auto Documentation** — Auto G&A, Auto Plan, Auto 3D, Auto Detail, Auto Tag and Auto Dims build a presentation set straight from the model.
- **Steel Framing & Connections** — Generate portal-frame sheds and apply steel connections across the model.

---

## 🏗️ Structural Rebar Panel
*Our flagship suite of parametric reinforcement tools. Automate complex detailing in seconds.*

| Command | Description | Key Features |
|:---|:---|:---|
| **Design Rules** | Centralized design code configuration. | Set default covers, lap lengths, and bar standards (AS/BS/US). |
| **Pre-defined Shape** | Load standard rebar shape families. | Instantly loads HT, L, LL, SP, and CT shapes into your project. |
| **Foundation Rebar** | Parametric reinforcement for footings. | Supports Strip Footings, Pad Footings, and Bored Piles. |
| **Wall Rebar** | Intelligent wall reinforcement engine. | Handles vertical/horizontal bars, stack splicing, and L/U corners. |
| **Column Rebar** | Rapid column reinforcement. | Rectangular and circular profiles with ties and confinement zones. |
| **Beam Rebar** | Multi-layer beam detailing. | Standard and Advance modes, continuous spans, auto-support detection, and 6-layer control. |
| **Slab Rebar** *(Preview)* | Mat reinforcement for slabs & raft foundations. | Span-direction-aware two-way mats, opening avoidance, and auto-grouped rebar sets. Curtailment and native Area Reinforcement output are coming in a future release. |
| **Rebar Presets** | Named presets inside every rebar window. | Save, load and delete named settings per tool, and Export / Import them as `.json` files to share with your team. |
| **Rebar Palette** | Floating persistent toolbar. | Quick access to rebar visibility, host-based tools, **Crank Bar** (auto-lap) and **Split Bar**. |
| **Rebar Setting** | Rebar visibility and host tools. | Obscured / Unobscured display, Hide / Show All, and Hide / Show / Isolate / Select / Delete rebar by host element. |
| **Rebar Q'ty** | Rebar quantity summaries. | Group quantities by Host Category, Host Mark, or Rebar Partition. |
| **Metadata Suite** | Smart parameter management. | Auto-assign Element Names, create the ElementName parameter, clear Name / Mark / Comments values, and sync Mark or ElementName to Partition. |
| **Apply Bar Marks** | Number beam rebar for shop drawings. | Numbers rebar per beam Mark in drawing order (top bars left to right, then bottom, side bars, stirrups); identical bars share one mark. |

### 📐 Rebar Shop — Shop Drawing Suite
*Select the host elements and create the drawing from the rebar already in your model, placed on a new or existing sheet. Re-run to update in place.*

| Command | Description | Key Features |
|:---|:---|:---|
| **Beam Shop Drawing** | Drawing for one continuous beam line (several lines per run). | Long section, cross sections at supports and midspans, 3D view, span divisions (L/3, L/4), stirrup zone and lap dimensions, rebar tags with optional leaders, merged identical sections, bar bending schedule with mass summary, and notes block. |
| **Column Shop Drawing** | Drawing for one or more column stacks. | Elevations of the whole stack (footing to roof) showing the existing rebar. |
| **Wall Shop Drawing** | Drawing for one or more wall stacks. | Elevation of the whole wall stack (footing to roof), vertical sections, dimensions, range annotations, tags, bar bending schedule and notes. |
| **Footing Shop Drawing** | One drawing per pad footing type (same Mark and bars). | Bottom-mat plan, sections, 3D view, bar bending schedule and notes. |
| **Strip Footing Shop Drawing** | One drawing per strip footing type, size and bars. | Cross section, optional elevation along the strip, 3D view, bar bending schedule and notes. Supports Wall Foundations, line-based foundation families and long pads. |
| **Pile Shop Drawing** | One drawing per bored pile type. | Elevation of the full cage, cross sections, 3D view, bar bending schedule and notes. |

All shop drawing windows use tabbed settings (Views, Dimensions, Tags, Sheet & Output), built-in dimension styles and named presets.

---

## 📐 Structural Model Panel
*Generative modeling tools for structural framing, connections and analysis handoff.*

| Command | Description |
|:---|:---|
| **Roof Framing** | Select any Roof or Floor and generate a full purlin/rafter assembly with spacing control. |
| **Auto Frame** | Generate a parametric steel portal-frame shed — columns, rafters, purlins, side rails, gable girts and bracing — with a live 3D preview. |
| **Advance Frame** | Same generator as Auto Frame, plus advanced options (under active development). |
| **Auto Connection** | Scan existing columns and framing and apply steel connections at column bases, column-to-beam knees and beam-to-beam joints. Auto Scan or Manual Pick, with Force Connect to bridge small gaps. Needs the connection families loaded in the project. |
| **Reset Steel Member** | Return steel columns and framing that have steel connections back to native Revit behaviour (attach, cut and cope work again). |
| **Column Attach** | Attach column tops or bases to the nearest beam and/or slab, or detach them while keeping their position. Uses the selection, otherwise the columns in the active view. |
| **X-Brace** | Generate crossed diagonal bracing between structural columns with auto-angle calculation. |
| **K-Brace** | Generate K-shaped (chevron) bracing with a central apex point. |
| **H-Frame** | Generate parallel horizontal braces spanning multiple structural bays. |

### Build Analytical *(Revit 2026 and 2027)*
*Prepare the analytical model for handoff to Robot Structural Analysis or Space Gass.*

| Command | Description |
|:---|:---|
| **Generate Analytical Model** | Builds the analytical model for the selected columns and framing, or for the active view if nothing is selected. |
| **Check Analytical Model** | Audits the analytical model against a 7-point checklist (coverage, connectivity, consistency, classification, section, supports, stray members) and lists issues to fix. |
| **Export to Robot Structural Analysis** | Exports nodes and bars to Robot, tagging each bar with a Revit ID so members can be traced between Revit and Robot. |
| **Apply Basic Loads** | Adds a Dead self-weight case, or tributary-width-distributed uniform Dead / Live / Wind / Snow load cases, to the connected Robot session. |

---

## 🔍 Project Audit Panel
*Keep your models lean, clean, and professional.*

| Command | Description |
|:---|:---|
| **Project Folder** | Instantly open Windows Explorer at the central model's file location. |
| **Resolve Overlaps** | Detect and fix overlapping text notes and tags for better readability. |
| **Project TextStyle** | Batch align text notes and their leaders, or convert between text styles. |
| **Title on Sheets** | Apply a consistent title block across all sheets (e.g. Section, Detail). |
| **Load More Type** | Add additional types to an existing family without using the Type Catalog. |
| **Wipe Suite** | One-click purge of empty tags, model components, unused filters and templates, subcategories, and CAD links. |

---

## 🎨 Graphic Overrides Panel
*Control your view presentation without fighting the native Revit dialogs.*

| Command | Description |
|:---|:---|
| **Dim Fake** | A safety-first tool to override dimension text with custom values or "dummy" text. |
| **Text Tools** | Audit all text elements in the view, or batch-convert selected text to uppercase. |
| **CAD Presets** | Instant override presets for linked CAD: Black / Hidden / Transparent, Purple, Orange, Blue or Green, plus a saved Project CAD Override applied across all views. |
| **Transparency** | Set 40%, 60% or 100% surface transparency on selected elements with one click. |
| **Match Overrides** | Select a source element and paint its graphic overrides onto multiple targets. |
| **Reset Tools** | Quickly clear all overrides from a selection or the entire active view. |
| **Filter Toggles** | Temporarily disable, re-enable, or permanently remove all View Filters from the active view. |

---

## 📂 Management Panel
*Automate the documentation grind: Sheets, Views, viewports, IFC coordination, and model upgrades.*

| Command | Description | Key Features |
|:---|:---|:---|
| **Family Manager** | A powerful browser to transfer views, sheets, and templates between projects. | Selective copy/transfer of system families, view templates, sheets, and views across open Revit models. |
| **Sheet Maker** | Batch sheet creation and management. | Rapid multi-sheet generation from templates or lists, with real-time preview and sheet parameter population. |
| **Renumber Sheets** | Prefix-based sheet renumbering engine. | Batch renumber sheets by replacing prefix series (e.g. S2- to S5-) across the entire project or selected sheet sets. |
| **Drawing Register** *(Under Development)* | Drawing sheet tracking and registers. | Manage drawing registers and revision histories. |
| **Align Schematic** | Viewport alignment tool. | Perfectly align viewports to the exact same XY coordinate across multiple sheets. |
| **Rename Tools** | Batch-rename and renumber viewports. | Rename by naming rule, strictly rename to match the Detail Number, or auto-renumber sequentially based on sheet XY coordinates. |
| **Add Views** | Batch view placement on sheets. | Place a selected view, or batch-place multiple views, onto target sheets in a single operation. |
| **View & 3D Controls** | Crop and viewport navigation tools. | Set crop region interactively, toggle crop visibility project-wide, **Zoom To** any element in the selection, show/hide the section box, and align it to a picked face. |
| **Auto BreakLine** | Automatic crop boundary breaklines. | Automatically places detail components wherever model elements cross the crop boundary; choose the break line family with Set Family. |
| **IFC Export** | High-precision discipline IFC exporter. | Export discipline-specific IFC models (Structural, Architectural, MEP, or Custom 3D View filters) with locked zero-drift coordinates (Shared Coordinate or Project Internal) and automated file naming. |
| **IFC Cleaner** | IFC geometry optimizer and sanitizer. | Purge and optimize heavy IFC files (strip unnecessary geometry such as doors, furniture, sanitary fittings) to prevent Revit from hanging during import. |
| **Revit Upgrade** | Version migration tools. | Duplicate a .rvt file for every target Revit version (R22–R27), or batch upgrade an entire folder of files in-place to the current running Revit version. |

### 🤖 Auto Drafting — Presentation Set from the Model
*Build views, sheets, tags and dimensions in batches.*

| Command | Description |
|:---|:---|
| **Auto G&A** | One-click General Arrangement set from the company standard: 3D presentation (S0), plans (S1), sections and elevations (S2), all tagged. |
| **Auto Plan** | Batch-create Floor Plan and Structural Framing Plan views for every level, and place each on its own sheet with auto-numbering. |
| **Auto 3D** | Create a 3D view auto-fitted to selected elements, or batch-generate 3D views by Level, Grid, Workset or Selection with standard isometric views, templates and sheet placement. |
| **Auto Detail** | Auto-generate elevations (and sheets) for selected walls, beams or rooms. |
| **Auto Tag** | Tag beams, columns, walls and foundations in the active plan, section or elevation with clash-free placement and free-end leaders. |
| **Auto Dims** | Auto-dimension grids, walls, columns and foundations in the current plan view. |
| **Dim Audit** | Find and fix overlapping or unreadable dimensions in the active view. |

---

## 👁️ Visibility & Graphics Panel
*22 commands for lightning-fast category management.*

| Command | Description |
|:---|:---|
| **Quick Filter** | Instant parameter-based colour coding for model auditing and coordination. |
| **Highlight** | Focus on your selection by turning the rest of the model 80% transparent. |
| **Quick VG** | A streamlined panel replacing Visibility/Graphics for all major categories. |
| **Quick Pick** | Quickly select elements by type, category, or parameter value, with custom favourites. |
| **Category Toggles** | 2D/3D master toggles, plus instant show/hide for Foundations, Walls, Floors, Columns, Framing, Roof, Connections, Structural Packs, MEP Pack, Rebar, Grids, Levels, Reference Planes and Scope Boxes. |
| **Link Toggles** | One-click visibility for all Linked Revit Models and Linked/Imported CAD. |

---

## 🛠️ General Productivity Panel
*The "Swiss Army Knife" of everyday Revit tools.*

| Command | Description |
|:---|:---|
| **Print PDF** | Batch export sheets to PDF or DWG with automatic naming, sorting, and Print Set selection. |
| **Export CAD** | Export selected sheets to a single DWG file with one layout per sheet. |
| **Export Dim Styles** | Pick dimension styles from the project and export their settings to a JSON file. |
| **Revision Tools** | Set or remove revisions on sheets, create Revision Sets, show/hide current revision clouds, reset hidden clouds, and delete selected revision clouds. |
| **Join & Cut** | Join Advance (toggle join/unjoin), Cut Geometry, and **Auto Notch** — select main beams to notch the intersecting secondary beams automatically. |
| **Framing Tools** | Allow/Disallow Join (selection or view), **Beam Reset** (default extensions and justification), and **Fix Off-Axis Framing** for beams and braces with tiny level mismatches. |
| **Flip Elements** | Batch-flip the facing or orientation of selected elements. |
| **Rotate Multiple** | Rotate a large group of elements individually around their own center points. |
| **Grid Tools** | Toggle grid bubbles and switch selected grids between 2D and 3D extents. |
| **Filter Tools** | Copy view filters between views and auto-generate a colour-coded Filters Legend. |
| **Region Tools** | Merge filled regions, convert them to Floors/Ceilings, or change LineStyles. |
| **Wall Constraints** | Match top, base or both constraints of selected walls to a reference wall, or set them manually with Modify Constraints. |

---

## 🔑 Installation & Licensing
### Installation
1. Close all Revit sessions.
2. Run `install.bat`.
3. Restart Revit. The **antiGGGravity** tab will appear in your ribbon.

### Activation
*   **Trial or Full Version**: Click **Request License** in the License panel, choose a trial or full license, and click **Send**. Use **Hardware ID** to copy your machine ID, and **Check Update** to download new versions.
*   You can also send your **Hardware ID** to `antiGGGravity.info@gmail.com`.

---
© 2026 antiGGGravity. All rights reserved. Revit® is a registered trademark of Autodesk, Inc.
