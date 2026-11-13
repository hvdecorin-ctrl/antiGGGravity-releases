# antiGGGravity — Professional BIM Toolkit for Revit
![Banner](https://raw.githubusercontent.com/hvdecorin-ctrl/antiGGGravity/main/Resources/banner.png)

> **The definitive command guide and functional documentation for the antiGGGravity BIM Toolkit.**
> Compatible with Revit 2022, 2023, 2024, 2025, 2026, and 2027.
> *Guide version: 2.2.37*

---

## 📑 Table of Contents
1. [Introduction](#-introduction)
2. [Project Audit Panel](#-project-audit-panel)
3. [Overrides Panel](#-overrides-panel)
4. [Management Panel](#-management-panel)
5. [Visibility Graphic Panel](#-visibility-graphic-panel)
6. [Rebar Panel](#-rebar-panel)
7. [Model Panel](#-model-panel)
8. [General Panel](#-general-panel)
9. [AntiGGGravity Panel (License & Updates)](#-antigggravity-panel-license--updates)
10. [Installation & Licensing](#-installation--licensing)

---

## 🚀 Introduction
**antiGGGravity** is a high-performance productivity suite designed by structural engineers for the AEC industry. It automates the "low-value" repetitive tasks that consume up to 40% of a BIM professional's day, allowing you to focus on engineering and design.

With **160+ specialized commands** across 8 ribbon panels, this toolkit bridges the gap between Revit's native functionality and the speed required for large-scale commercial project delivery.

---

## 🔍 Project Audit Panel
*Keep your models lean, clean, and professional.*

| Command | Description |
|:---|:---|
| **Project Folder** | Open Windows Explorer at the central model's file location. |
| **Audit ▸ Project TextStyle** | Batch align text notes and their leaders, or convert between text styles. |
| **Audit ▸ Title on Sheets** | Apply a consistent title block across all sheets (e.g. Section, Detail). |
| **Wipe ▸ Wipe Empty Tags** | Delete all tags that have no content in the current view. |
| **Wipe ▸ Wipe Model Components** | Multi-select model elements to permanently remove (constraints, groups, links, etc.). |
| **Wipe ▸ Wipe Unused Filters** | Find and purge filters not used in any view or template. |
| **Wipe ▸ Wipe Unused Templates** | Find and purge view templates not assigned to any view. |
| **Wipe ▸ Wipe SubCategories** | Purge custom subcategories from the project. |
| **Wipe ▸ Wipe CADs** | Find and purge imported/linked CAD files and their unused categories. |
| **Load More Type** | Load additional types from an existing family without using the Type Catalog. |

---

## 🎨 Overrides Panel
*Control your view presentation without fighting the native Revit dialogs.*

| Command | Description |
|:---|:---|
| **Dim Fake** | Override dimension text with a custom value or "dummy" text. |
| **Text Edit ▸ Text Audit** | Audit and report all text elements in the view. |
| **Text Edit ▸ Text Upper** | Convert selected text elements to uppercase. |
| **Cad Override** | Preset styles for linked/imported CAD: Black / Hidden / Trans, Purple / Hidden / Trans, Orange / Trans, Blue / Trans, Green / Trans, plus **Project CAD Override** to apply a saved style across all views. |
| **Transparency** | Set 40%, 60% or 100% surface transparency on selected elements. |
| **Match & Reset ▸ Match Overrides** | Copy graphic overrides from a source element onto selected targets. |
| **Match & Reset ▸ Reset Selected / Reset All Overrides** | Clear overrides from the selection, or from the entire active view. |
| **Reset Filters** | Disable, re-enable, or permanently remove all view filters in the active view. |

---

## 📂 Management Panel
*Automate the documentation grind: families, sheets, views, auto drafting, IFC coordination, and model upgrades.*

| Command | Description | Key Features |
|:---|:---|:---|
| **Family Manager** | Selective family manager and transfer tool. | Transfer views, sheets, templates and system families between open Revit projects. |
| **Drawing Manager ▸ Sheet Maker** | Batch sheet creation and management. | Multi-sheet generation from templates or lists, rename/renumber existing sheets. |
| **Drawing Manager ▸ Renumber Sheets** | Prefix-based sheet renumbering. | Replace prefix series (e.g. S2- → S5-) across the project or selected sheets. |
| **Drawing Manager ▸ Drawing Register** | Drawing register management. | *Under development.* |
| **Align Schematic** | Viewport alignment. | Align viewports to a consistent XY position across multiple sheets. |
| **Rename ▸ Rename View / Rename (Override)** | Batch-rename viewports/views. | Rename by naming rule, or strictly match the Detail Number (overriding name collisions). |
| **Rename ▸ Renumber Viewports / (Auto)** | Viewport renumbering. | Sequential renumbering on the active sheet, manually or automatically from XY coordinates and rule. |
| **Add Views** | Place views on sheets. | Add Selected View to a chosen sheet, or batch-place multiple views. |
| **Set Crop View** | Set the crop region boundary for the active view. | |
| **Crop Region** | Toggle crop region visibility. | Applies to all views placed on any sheet in the project. |
| **Zoom To** | Selection navigator. | Lists the current selection and jumps to any element in its best view. |
| **Break Line ▸ Auto BreakLine / Set Family** | Automatic crop-boundary breaklines. | Places break-line detail components where elements cross the crop boundary; choose the detail family. |
| **Auto 3D** | Quick 3D view. | Creates a 3D view auto-fitted to the selected elements. |
| **Toggle 3D ▸ Toggle 3D / Align SectBox** | Section box tools. | Show/hide the section box, or align it to a picked face. |
| **Sectbox** | Toggle section box visibility without resizing it. | |
| **Auto Drafting ▸ Auto G&A** | One-click General Arrangement set. | 3D presentation (S0), plans (S1), sections and elevations (S2), all tagged, from the company standard. |
| **Auto Drafting ▸ Auto Plan** | Batch plan views and sheets per level. | Floor Plan and Structural Framing Plan views for every level, each on its own sheet with auto-numbering. |
| **Auto Drafting ▸ Auto 3D** | Batch 3D view generator. | By Level, Grid, Workset or Selection, with standard isometrics, templates and sheet placement. |
| **Auto Drafting ▸ Auto Detail** | Auto elevations and sheets. | For selected walls, beams or rooms. |
| **Auto Drafting ▸ Auto Tag** | Clash-free tagging. | Tags beams, columns, walls and foundations in plan, section or elevation with free-end leaders. |
| **Auto Drafting ▸ Annotation Audit** | Annotation clash checker. | Scans tags, text notes and dimensions (active view, view set or project), lists clashes and moves them (Fix Selected / Fix All). |
| **Auto Drafting ▸ Section Gap** | *Experimental.* | Splits selected section lines in a plan and pulls the gap toward the heads; drives the mouse, so park the cursor first to calibrate. |
| **Auto Drafting ▸ Auto Dims** | Automatic dimensioning. | Grids, walls, columns and foundations in the current plan view. |
| **IFC Manager ▸ IFC Export** | High-precision discipline IFC exporter. | Structural, Architectural, MEP or Custom 3D View filters with locked zero-drift coordinates. |
| **IFC Manager ▸ IFC Cleaner** | IFC optimizer. | Strip unnecessary geometry (doors, furniture, vanity) to stop Revit hanging on import. |
| **IFC Manager ▸ Upgrade Revit Version** | Version duplication. | Duplicate a .rvt with `(Rxx)` in its name for all target versions (R22–R27); re-run to overwrite. |
| **IFC Manager ▸ Upgrade Revit Package** | Batch upgrade. | Upgrade all Revit files in a folder in-place to the running Revit version. |

---

## 👁️ Visibility Graphic Panel
*Lightning-fast category and workset visibility management.*

| Command | Description |
|:---|:---|
| **Quick Filter** | Instant parameter-based colour coding for model auditing and coordination. |
| **Highlight** | Fade all other model elements to 80% transparency to focus on your selection. |
| **Workset Manager** | Create worksets and move whole categories into them (reusable lists from file), then toggle workset visibility live in the active view. |
| **Quick VG** | Streamlined panel replacing Visibility/Graphics for all major categories. |
| **Quick Pick** | Select elements by type, category or parameter value, with custom favourites. |
| **Visibility ▸ Master toggles** | Toggle All Elements, 2D Elements, 3D Elements, Structural Packs, MEP Pack. |
| **Visibility ▸ Model categories** | Toggle Foundations, Walls, Floors (Slabs), Columns, Framing, Roof, Connections, Rebar. |
| **Visibility ▸ Datums** | Toggle Grids, Levels, Ref Planes, Scope Box. |
| **Visibility ▸ Links** | Toggle CAD Links and Revit Links. |

---

## 🏗️ Rebar Panel
*Our flagship suite of parametric reinforcement tools. Automate complex detailing in seconds.*

| Command | Description | Key Features |
|:---|:---|:---|
| **Design Rules** | Centralized design code configuration. | View design codes, reference parameters and calculation rules (covers, laps, bar standards). |
| **Pre-defined Shape** | Load special rebar shape families. | HT, L, LL, SP and CT shapes from the project library. |
| **Foundation Rebar** | Parametric footing reinforcement. | Strip Footings and Pad Footings. |
| **Wall Rebar** | Wall reinforcement engine. | Walls and wall corners. |
| **Column Rebar** | Rapid column reinforcement. | Structural columns. |
| **Beam Rebar** | Multi-layer beam detailing. | Standard and Advance modes. |
| **Slab Rebar** *(Preview)* | Mat reinforcement for slabs & raft foundations. | Continuous mats only; curtailment and openings are not yet supported. |
| **Rebar Shop ▸ Beam Shop Drawing** | Beam shop drawing. | Long section, cross sections at supports/midspans and a 3D view of existing rebar, on a new or existing sheet. |
| **Rebar Shop ▸ Column Shop Drawing** | Column shop drawing. | Full-stack elevations (footing to roof) of existing rebar. |
| **Rebar Shop ▸ Wall Shop Drawing** | Wall shop drawing. | Full-stack elevation with bar bending schedule and notes. |
| **Rebar Shop ▸ Footing Shop Drawing** | Pad footing shop drawing. | One drawing per footing type: bottom-mat plan, sections, 3D view, BBS and notes. |
| **Rebar Shop ▸ Strip Footing Shop Drawing** | Strip footing shop drawing. | Cross section, optional elevation along the strip, 3D view, BBS and notes. |
| **Rebar Shop ▸ Pile Shop Drawing** | Bored pile shop drawing. | Full cage elevation, cross sections, 3D view, BBS and notes. |
| **Rebar Palette** | Floating persistent toolbar. | Rebar visibility, selection and host-based tools in one palette. |
| **Rebar Setting** | Visibility, host and editing tools (also in the Palette). | Obscured / Unobscured, Hide / Show All, Hide / Show / Isolate / Select / Delete Host, **Crank Bar**, **Split Bar**. |
| **Rebar Q'ty** | Rebar quantity summaries. | Group by Host Category, Host Mark, or Partition. |
| **Assign Name** | Element Name management. | Assign Name (auto-generate), Create ElementName parameter, Remove Extg Value (clear Name/Mark/Comments). |
| **Rebar Partition** | Partition from host parameters. | Mark → Partition, ElementName → Partition, and **Apply Bar Marks** (numbers beam rebar per Mark and orders marks for shop drawings; identical bars share one mark). |

---

## 📐 Model Panel
*Generative modeling, steel connections and analytical model tools.*

| Command | Description |
|:---|:---|
| **Roof Framing** | Generate purlins and rafters on roof or slab faces (pre-select roofs/floors or pick faces). |
| **Frame Generator ▸ Auto Frame** | Parametric steel portal-frame shed: columns, rafters, purlins, side rails, gable girts and bracing, with live 3D preview. |
| **Frame Generator ▸ Advance Frame** | Same generator as Auto Frame plus advanced options under active development. |
| **Frame Generator ▸ Auto Connection** | Scan existing columns and framing and apply steel connections at bases, knees and beam-to-beam joints. |
| **Frame Generator ▸ Reset Steel Member** | Return steel members with connections to native Revit behaviour (attach, cut, cope). Needs a concrete column/framing family loaded. |
| **Frame Generator ▸ Beam Reset** | Reset beam start/end extensions and justification to defaults. |
| **Frame Generator ▸ Fix Off-Axis Framing** | Auto-fix beams/braces Revit flags as "slightly off axis" when the cause is a tiny start/end level mismatch; lists anything else for manual review. |
| **Frame Generator ▸ Column Attach** | Attach column tops/bases to the nearest beam and/or slab, or detach while keeping position. |
| **Bracing ▸ X-Brace / K-Brace / H-Frame** | Crossed bracing between two columns, K-shaped bracing with apex, and parallel horizontal braces across multiple bays. |
| **Bracing ▸ Auto X-Brace** | Scan the active view (or selected columns and parallel beams) for adjacent column bays, preview them, and place an X-brace in each bay you keep ticked. Bays that already have a diagonal brace are skipped. |
| **Build Analytical ▸ Generate Analytical Model** | Build the analytical model for selected (or view) columns and framing. |
| **Build Analytical ▸ Check Analytical Model** | Audit against a 7-point checklist (coverage, connectivity, consistency, classification, section, supports, stray members). |
| **Build Analytical ▸ Export to Robot Structural Analysis** | Export nodes and bars via RobotOM with a Revit ID correlation table for two-way traceability. |
| **Build Analytical ▸ Apply Basic Loads** | Add Dead self-weight, or tributary-width Dead/Live/Wind/Snow load cases, to the connected Robot session (requires Export to Robot first). |

---

## 🛠️ General Panel
*The "Swiss Army Knife" of everyday Revit tools.*

| Command | Description |
|:---|:---|
| **Export Manager ▸ Print PDF** | Batch export sheets to PDF or DWG with automatic sorting and Print Set selection. |
| **Export Manager ▸ Export CAD** | Export selected sheets to a single DWG with one layout per sheet. |
| **Export Manager ▸ Export Dim Styles** | Export selected dimension style settings to a JSON file. |
| **Revision** | Set Rev, Remove Rev, Create Rev Set, Show Current Rev, Reset Rev Clouds, Remove Cloud. |
| **Join Advance** | Toggle join/unjoin geometry between two selected elements. |
| **Geometry Issue** | **Cut Geometry** (cut all intersecting elements) and **Auto Notch** (notch secondary beams against selected main beams). |
| **Flip Elements** | Batch-flip facing or orientation of selected elements. |
| **Framing Issue** | **Allow Join** / **Disallow Join** — act on the selected elements; with nothing selected, on all structural framing in the active view. |
| **View Filters** | Copy Filters between views; generate a colour-coded Filters Legend. |
| **Grid Bubble** | Toggle grid bubbles, switch grids between 2D/3D extents, and flip **Level Head** left/right. |
| **Rotate Multiple** | Rotate multiple selected elements by a specified angle. |
| **Filled Regions** | Merge regions, convert to Floors or Ceilings, or change LineStyle. |
| **Walls** | Match Top, Match Base, Match Both constraints to a reference wall, or Modify Constraints manually. |

---

## 🔐 AntiGGGravity Panel (License & Updates)

| Command | Description |
|:---|:---|
| **License ▸ Hardware ID** | Copy your machine ID for license activation. |
| **License ▸ Request License** | Submit a request for a trial or full license. |
| **License ▸ Check Update** | Check for new versions and download updates. |

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
