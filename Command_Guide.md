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

With **130+ specialized commands**, this toolkit bridges the gap between Revit's native functionality and the speed required for large-scale commercial project delivery.

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
| **Beam Rebar** | Multi-layer beam detailing. | Continuous spans, auto-support detection, and 6-layer control. |
| **Slab Rebar** *(Preview)* | Mat reinforcement for slabs & raft foundations. | Span-direction-aware two-way mats, opening avoidance, and auto-grouped rebar sets. Curtailment and native Area Reinforcement output are coming in a future release. |
| **Rebar Palette** | Floating persistent toolbar. | Quick access to **Crank rebar** (auto-lap) and **Split rebar** tools. |
| **Quantity Tools** | Automated rebar scheduling metadata. | Group quantities by Host Category, Mark, or Partition. |
| **Metadata Suite** | Smart parameter management. | Auto-assign Element Names, create the ElementName parameter, clear existing values, and sync Mark/ElementName to Partition. |

---

## 📐 Structural Model Panel
*Generative modeling tools for structural framing and bracing systems.*

| Command | Description |
|:---|:---|
| **Roof Framing** | Select any Roof or Floor and generate a full purlin/rafter assembly with spacing control. |
| **X-Brace** | Generate crossed diagonal bracing between structural columns with auto-angle calculation. |
| **K-Brace** | Generate K-shaped (chevron) bracing with a central apex point. |
| **H-Frame** | Generate parallel horizontal braces spanning multiple structural bays. |

---

## 🔍 Project Audit Panel
*Keep your models lean, clean, and professional.*

| Command | Description |
|:---|:---|
| **Project Folder** | Instantly open Windows Explorer at the central model's file location. |
| **Resolve Overlaps** | Intelligently offsets overlapping text notes and tags for better readability. |
| **Project TextStyle** | Batch-convert or align all text styles in the project to your office standard. |
| **Title on Sheets** | Apply consistent title block naming and numbering across all sheets. |
| **Load More Type** | Add additional types to an existing family without using the Type Catalog. |
| **Wipe Suite** | One-click purge of empty tags, model components, unused filters/templates, subcategories, and CAD links. |

---

## 🎨 Graphic Overrides Panel
*Control your view presentation without fighting the native Revit dialogs.*

| Command | Description |
|:---|:---|
| **Dim Fake** | A safety-first tool to override dimension text with custom values or "dummy" text. |
| **Text Tools** | Audit all text elements in the view, or batch-convert selected text to uppercase. |
| **CAD Presets** | Instant presets for Linked CAD: Black/Half/Trans, Orange, Blue, Green, or Purple. |
| **Transparency** | Set 60% or 100% surface transparency on selected elements with one click. |
| **Match Overrides** | Select a source element and paint its graphic overrides onto multiple targets. |
| **Reset Tools** | Quickly clear all overrides from a selection or the entire active view. |
| **Filter Toggles** | Temporarily disable, re-enable, or permanently remove all View Filters from the active view. |

---

## 📂 Management Panel
*Automate the documentation grind: Sheets, Views, viewports, IFC coordination, and model upgrades.*

| Command | Description | Key Features |
|:---|:---|:---|
| **Family Manager** | A powerful browser to transfer views, sheets, and templates between projects. | Selective copy/transfer of system families, view templates, sheets, and views across open Revit models. |
| **Level View & Sheet Generator** | Batch generator for plan views and sheets per level. | Batch-create Floor Plan and Structural Framing Plan views for selected levels, generate a sheet per view with chosen titleblocks, and auto-place/center views with configurable naming and numbering rules. |
| **Sheet Maker** | Batch sheet creation and management. | Rapid multi-sheet generation from templates or lists, with real-time preview and sheet parameter population. |
| **Renumber Sheets** | Prefix-based sheet renumbering engine. | Batch renumber sheets by replacing prefix series (e.g. S2- to S5-) across the entire project or selected sheet sets. |
| **Drawing Register** | Drawing sheet tracking and registers. | Export and manage drawing sheet registers and revision histories. |
| **IFC Export** | High-precision discipline IFC exporter. | Export discipline-specific IFC models (Structural, Architectural, MEP, or Custom 3D View filters) with locked zero-drift coordinates (Shared Coordinate or Project Internal) and automated file naming. |
| **IFC Cleaner** | IFC geometry optimizer and sanitizer. | Purge and optimize heavy IFC files (strip unnecessary geometry such as doors, furniture, sanitary fittings) to prevent Revit from hanging during import. |
| **Rename Tools** | Batch-rename viewports/views. | Rename by Detail Number, custom naming rules, or auto-renumber sequentially based on sheet XY coordinates. |
| **Add Views** | Batch view placement on sheets. | Place a selected view, or batch-place multiple views, onto target sheets in a single operation. |
| **View & 3D Controls** | Crop and viewport navigation tools. | Set crop region interactively, toggle crop visibility project-wide, zoom to selection, Auto 3D, and section-box tools. |
| **Auto Place Break line** | Automatic crop boundary breaklines. | Automatically places detail components wherever model elements cross the crop boundary. |
| **Align Schematic** | Viewport alignment tool. | Perfectly align viewports to the exact same XY coordinate across multiple sheets. |
| **Revit Upgrade** | Version migration tools. | Duplicate a .rvt file for every target Revit version (R22–R27), or batch upgrade an entire folder of files in-place to the current running Revit version. |

---

## 👁️ Visibility & Graphics Panel
*22 commands for lightning-fast category management.*

| Command | Description |
|:---|:---|
| **Quick Filter** | Instant parameter-based colour coding for model auditing and coordination. |
| **Highlight** | Focus on your selection by turning the rest of the model 80% transparent. |
| **Quick VG** | A streamlined panel replacing Visibility/Graphics for all major categories. |
| **Quick Pick** | Category-based element picker with custom favourites for fast selection workflows. |
| **Category Toggles** | 2D/3D Master Toggles, plus instant show/hide for Foundations, Walls, Rebar, etc. |
| **Link Toggles** | One-click visibility for all Linked Revit Models and Linked/Imported CAD. |

---

## 🛠️ General Productivity Panel
*The "Swiss Army Knife" of everyday Revit tools.*

| Command | Description |
|:---|:---|
| **Print PDF** | Batch export sheets to PDF with automatic naming, sorting, and Print Set selection. |
| **Export CAD** | Export selected sheets to DWG with one layout per sheet and automatic naming. |
| **Revision Tools** | Set or remove revisions on sheets, create Revision Sets, show/hide current revision clouds, reset hidden clouds, and delete selected revision clouds. |
| **Join & Cut** | Join Advance with cut-priority control, Cut Geometry, and Allow/Disallow Join (selection or view). |
| **Flip Elements** | Batch-flip the facing or orientation of selected elements. |
| **Auto Dims** | Automatic dimensioning of Grids, Walls, Columns, and Foundations. |
| **Rotate Multiple** | Rotate a large group of elements individually around their own center points. |
| **Grid Tools** | Toggle grid bubbles and switch selected grids between 2D and 3D extents. |
| **Filter Tools** | Copy view filters between views and auto-generate a colour-coded Filters Legend. |
| **Region Tools** | Merge filled regions, convert them to Floors/Ceilings, or change LineStyles. |
| **Wall Constraints** | Match top/base constraints of selected walls to a reference wall instantly. |

---

## 🔑 Installation & Licensing
### Installation
1. Close all Revit sessions.
2. Run `install.bat`.
3. Restart Revit. The **antiGGGravity** tab will appear in your ribbon.

### Activation
*   **Trial**: Click **Request License** -> Select "30-Day Trial" -> Click **Send**.
*   **Full Version**: Send your **Hardware ID** (found in the antiGGGravity panel) to `antiGGGravity.info@gmail.com`.

---
© 2026 antiGGGravity. All rights reserved. Revit® is a registered trademark of Autodesk, Inc.
