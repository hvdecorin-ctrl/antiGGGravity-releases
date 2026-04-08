<p align="center">
  <h1 align="center">antiGGGravity</h1>
  <p align="center">
    <strong>Professional BIM Productivity Suite for Autodesk Revit</strong>
  </p>
  <p align="center">
    <a href="https://www.autodesk.com/products/revit/overview"><img src="https://img.shields.io/badge/Revit-2022–2027-0696D7.svg?logo=autodesk&logoColor=white" alt="Revit 2022–2027" /></a>
    <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET_4.8_|_8.0_|_10.0-512BD4.svg?logo=dotnet&logoColor=white" alt=".NET" /></a>
    <img src="https://img.shields.io/badge/Tools-100+-E34F26.svg" alt="100+ Tools" />
    <img src="https://img.shields.io/badge/License-Proprietary-333333.svg" alt="License" />
  </p>
</p>

---

**antiGGGravity** is a high-performance C#/WPF add-in for Autodesk Revit, purpose-built for structural engineers, architects, and BIM coordinators. It delivers **100+ commands** across 8 ribbon panels—covering reinforcement automation, graphic overrides, view management, parametric model generation, and rapid visibility controls—all wrapped in a modern, glassmorphism-inspired interface with standardized `PremiumWindowChrome`.

---

## Table of Contents

- [Panel Overview](#panel-overview)
- [Rebar Panel](#-rebar-panel)
- [Model Panel](#-model-panel)
- [Project Audit Panel](#-project-audit-panel)
- [Overrides Panel](#-overrides-panel)
- [Management Panel](#-management-panel)
- [Visibility Graphic Panel](#%EF%B8%8F-visibility-graphic-panel)
- [General Panel](#%EF%B8%8F-general-panel)
- [AntiGravity Panel](#-antigravity-panel)
- [Technical Specifications](#-technical-specifications)
- [Installation & Deployment](#-installation--deployment)
- [Target Audience](#-target-audience)
- [Licensing](#-licensing)

---

## Panel Overview

| # | Panel | Focus Area | Commands |
|:-:|-------|-----------|:--------:|
| 1 | **Rebar** | **Flagship** · Parametric reinforcement · detailing · marking | 20 |
| 2 | **Model** | **Flagship** · Structural framing generation · parametric bracing | 4 |
| 3 | **Project Audit** | Project hygiene · tagging · text standards · family management | 6 |
| 4 | **Overrides** | Graphic overrides · CAD styling · transparency · filter management | 16 |
| 5 | **Management** | View transfer · sheet ops · crop control · 3D navigation · viewport alignment | 14 |
| 6 | **Visibility Graphic** | Category toggles · element filtering · highlight · quick selections | 22 |
| 7 | **General** | Geometry joins · dimensions · filled regions · walls · grids · rotation | 22 |
| 8 | **AntiGravity** | Licensing & activation | 2 |

---

## 🏗️ Rebar Panel

> *Automate structural reinforcement from design to documentation.*

#### Rebar Generation Suite

The flagship parametric reinforcement system. Each host type opens a dedicated WPF interface for configuring bar diameters, hook overrides, cover offsets, distribution spacing, and automatic rebar comments for scheduling.

##### 🔹 Foundation Rebar
*   **Strip & Pad Footings**: Automated reinforcement for all standard footing types.
*   **Pad Foundations (Varied Shapes)**: Specialized support for **irregularly shaped** foundations with automated Top and Bottom mat generation.
*   **Side Rebar**: Intelligent vertical/side reinforcement for deep pad foundations with configurable leg extensions.
*   **Bored Piles**: Specialized circular reinforcement engine for deep foundations.
*   **Parametric Controls**: Fine-tune bar diameters, spacing, and cover per host.
*   **Hook Management**: Full control over starting and ending hook types.

##### 🔹 Column Rebar
*   **Rectangular & Circular Profiles**: Intelligent layout generation based on column shape.
*   **Longitudinal Main Bars**: Precise control over bar counts and diameters in X and Y directions.
*   **Transverse Ties & Stirrups**: Configurable spacing with automated confinement zones (end zones).
*   **Spiral Reinforcement**: Specialized spiral/hoop creation for circular columns.
*   **Column Stack Splicing**: Automated 1:6 cranked bar generation across multi-level column stacks.
*   **Starter Bars**: Development of reinforcement into footings or lower columns with user-defined extensions.

##### 🔹 Wall Rebar
*   **Layer Configurations**: Support for "Centre", "Both Faces", "External Face", or "Internal Face" layouts.
*   **Vertical & Horizontal Bars**: Integrated engine for full wall reinforcement.
*   **Tension Lap Splices**: Automated 1:6 cranked or straight lap splices for multi-level walls.
*   **Wall Corners & Intersections**: Dedicated specialized tools for **L-Corner** and **U-Corner** reinforcement.
*   **U-Bars**: One-click generation of Top, Bottom, and End U-bars.
*   **Auto-Trim**: Intelligent bar trimming at intersecting wall faces and structural elements.
*   **Starter Bars**: Development into foundations or slabs below.

##### 🔹 Beam Rebar
*   **Single & Continuous Spans**: Support for individual members or multi-span continuous beams.
*   **Layer-Based Control**: Dedicated settings for **T1, T2, T3** (Top) and **B1, B2, B3** (Bottom) layers.
*   **Stirrup Confinement**: Automated stirrup distribution with zone-based spacing rules.
*   **Side/Skin Reinforcement**: Automatic side bar placement for deep beams.
*   **Lap Splice Logic**: Automated lap splices in tension/compression zones with 1:6 cranked offsets.
*   **Support Overrides**: Manual override of rebar counts and types at specific structural supports.

#### Design & Setup

| Command | Description |
|---------|-------------|
| **Design Rules** | View design codes, reference parameters, and calculation rules (NZS 3101 etc.). |
| **Pre-defined Shape** | Load special rebar shapes (HT, L, LL, SP, CT) from the project library. |

#### Rebar Palette

A floating toolkit providing persistent access to rebar sub-tools without navigating the ribbon:

| Sub-tool | Description |
|----------|-------------|
| **Set Obscured** | Set all rebars in the active view to obscured (hidden behind concrete). |
| **Set Unobscured** | Set all rebars to unobscured (visible over concrete). |
| **Show All Rebar** | Unhide all rebar elements in the active view. |
| **Hide All Rebar** | Hide all rebar elements in the active view. |
| **Select by Host** | Select rebars hosted on the selected structural elements. |
| **Delete by Host** | Delete rebars hosted on the selected structural elements. |
| **Hide by Host** | Hide rebars on selected hosts in the current view. |
| **Show by Host** | Unhide rebars on selected hosts in the current view. |
| **Isolate by Host** | Show only rebars on selected hosts, hide all others. |
| **Rebar Crank** | Apply a cranked lap splice (1:6 slope, 1×db offset) at a picked location along a bar. |
| **Rebar Split** | Split a bar into two segments with a straight lap splice at a picked point. |

#### Quantity Takeoff

| Command | Description |
|---------|-------------|
| **Rebar Q'ty Host Category** | Rebar quantity summary grouped by host category (beam, wall, etc.). |
| **Rebar Q'ty Host Mark** | Rebar quantity summary grouped by host mark (element ID). |

#### Marking & Naming

| Command | Description |
|---------|-------------|
| **Assign Mark** | Auto-generate and assign a unique mark to selected structural elements. |
| **Create ElementName** | Create a shared parameter to store a custom element name. |
| **Assign ElementName** | Auto-generate and assign an Element Name to selected elements. |
| **Remove Extg Mark** | Clear the mark value from selected elements. |
| **Remove Extg ElementName** | Clear the Element Name value from selected elements. |

---

## 🏢 Model Panel

> *Turn hours of manual placement into seconds of intelligent automation.*

The Model Panel goes beyond documentation—it **builds your structural model for you**. Instead of placing individual framing members one-by-one, these tools analyze geometry and generate complete parametric assemblies in a single operation.

#### Roof Framing Engine

| Command | Description |
|---------|-------------|
| **Roof Framing** | Select a roof or slab → choose your member family → define spacing → instant purlin and rafter generation across the entire surface. Reads host geometry, places members with correct orientation and structural intent. |

#### Bracing Generation Suite

| Pattern | Description |
|---------|-------------|
| **X-Brace** | Crossed diagonal bracing between structural bays. Select two columns/grid intersections and both diagonals are created with proper connections. |
| **K-Brace** | Chevron (inverted-V) bracing with a configurable apex point. Calculates member angles, lengths, and connection geometry automatically. |
| **H-Frame** | Horizontal multi-brace configurations (girts, struts) between columns at specified elevations. Ideal for industrial steel structures and portal frames. |

> **Why it matters:** Manual bracing placement requires calculating angles, trimming members, and adjusting connections for every single brace. The Model Panel eliminates this entirely—select your bays, choose a pattern, and the complete bracing assembly is generated with structural accuracy.

---

## 🔎 Project Audit Panel

> *Keep your model clean, organized, and audit-ready.*

| Command | Description |
|---------|-------------|
| **Project Folder** | Open file explorer at the central model location. |
| **Wipe Empty Tags** | Scan and delete all tags with blank or null values in the current view. |
| **Resolve Overlaps** | Detect and fix overlapping text notes and tags. |
| **Project TextStyle** | Batch-align text notes and leaders, or convert between text styles project-wide. |
| **Title on Sheets** | Apply a consistent title block across all sheets (e.g. Section, Detail). |
| **Load More Type** | Add additional types to an already-loaded family without full reload. |

---

## 🎨 Overrides Panel

> *Take full control of graphic presentation without opening Visibility/Graphics.*

#### Dimensions & Text

| Command | Description |
|---------|-------------|
| **Dim Fake** | Override dimension text to display custom values while preserving actual dimensions. |
| **Text Audit** | Audit and report all text elements in the view for consistency. |
| **Text Upper** | Batch-convert selected text annotations to uppercase for drawing standards. |

#### CAD Override Presets

| Preset | Style |
|--------|-------|
| **Black / Half / Trans** | Black lines · halftone · transparent fill |
| **Orange / Half / Trans** | Orange lines · halftone · transparent fill |
| **Blue / Half / Trans** | Blue lines · halftone · transparent fill |
| **Green / Half / Trans** | Green lines · halftone · transparent fill |
| **Purple-Blue / Hidden / Half** | Purple-blue · hidden lines · halftone |
| **Project CAD Override** | Apply a saved CAD override style across all views |

#### Transparency, Match & Reset

| Command | Description |
|---------|-------------|
| **60% Transparency** | Set surface transparency to 60% on selected elements. |
| **100% Transparency** | Set surface transparency to fully transparent. |
| **Match Overrides** | Eyedropper—copy graphic overrides from a source element to others. |
| **Reset Selected** | Clear all graphic overrides from the current selection. |
| **Reset All Overrides** | Clear all graphic overrides in the active view. |

#### View Filters

| Command | Description |
|---------|-------------|
| **Disable Filters** | Temporarily disable all view filters without removing them. |
| **Enable Filters** | Re-enable all previously disabled view filters. |
| **Remove Filters** | Permanently remove all filters from the active view. |

---

## 📐 Management Panel

> *Streamline view, sheet, and viewport workflows at scale.*

#### Transfer & Renaming

| Command | Description |
|---------|-------------|
| **Transfer Family** | Transfer live views, drafting views, sheets, and families between Revit documents. |
| **Rename Views (Active Sheet)** | Batch-rename all viewports on the currently active sheet. |
| **Rename Views (All Sheets)** | Apply a consistent naming standard across every sheet in the project. |
| **Renumber Viewports** | Sequentially re-number viewport detail numbers on a sheet. |

#### View Placement & Sheets

| Command | Description |
|---------|-------------|
| **Add Selected View** | Place the selected view onto a target sheet with precise positioning. |
| **Add Views** | Batch-place multiple views onto sheets in a single operation. |
| **Duplicate Sheets** | Clone sheets with titleblock, viewport layout, and configurations. |

#### Crop & Navigation

| Command | Description |
|---------|-------------|
| **Set Crop View** | Interactively set or adjust the crop region boundary on section/plan views. |
| **Crop Region** | Toggle crop region visibility for all views placed on any sheet. |
| **Zoom To** | Instantly zoom to the bounding box of selected elements. |

#### 3D View Controls

| Command | Description |
|---------|-------------|
| **Auto 3D** | Generate a default 3D view of selected elements with automatic section box. |
| **Toggle 3D** | Toggle the section box on/off in the active 3D view. |
| **Sectbox** | Toggle section box visibility without resizing it. |

#### Alignment

| Command | Description |
|---------|-------------|
| **Align Schematic** | Align viewports to a consistent position across multiple sheets. |

---

## 👁️ Visibility Graphic Panel

> *Control what you see in seconds, not minutes.*

#### Smart Filtering & Selection

| Command | Description |
|---------|-------------|
| **Quick Filter** | Filter and color-code elements by a chosen parameter value. |
| **Highlight** | Highlight pre-selected elements—fades all others to 80% transparency. |
| **Quick VG** | Streamlined batch visibility toggle panel—replaces the native VG dialog workflow. |
| **Quick Pick** | Category-based element picker with Quick Pick buttons, auto-finish box selection, and persistent Custom Favorites row. Supports 3D/2D-only filtering. |

#### Category Toggles

| Command | Scope |
|---------|-------|
| **Toggle All Elements** | Master switch—show/hide all element categories |
| **Toggle 2D Elements** | Isolate annotation elements |
| **Toggle 3D Elements** | Isolate model categories |
| **Toggle Structural Packs** | All structural categories as a group |
| **Toggle Foundations** | Foundation elements |
| **Toggle Walls** | Wall elements |
| **Toggle Floors (Slabs)** | Floor and slab elements |
| **Toggle Columns** | Structural columns |
| **Toggle Framing** | Beams, braces, trusses |
| **Toggle Roof** | Roof elements |
| **Toggle Connections** | Structural connections |
| **Toggle Rebar** | Reinforcement bars |
| **Toggle Grids** | Grid lines |
| **Toggle Levels** | Level datums |
| **Toggle Ref Planes** | Reference planes |
| **Toggle Scope Box** | Scope boxes |
| **Toggle CAD Links** | Imported/linked CAD files |
| **Toggle Revit Links** | Linked Revit models |

---

## ⚙️ General Panel

> *The Swiss Army knife for everyday Revit productivity.*

#### Geometry Joins

| Command | Scope | Description |
|---------|-------|-------------|
| **Allow Join (Selection)** | Selected | Allow geometry join on selected elements. |
| **Allow Join (View)** | Active view | Allow geometry join on all elements in the active view. |
| **Disallow Join (Selection)** | Selected | Disallow geometry join on selected elements. |
| **Disallow Join (View)** | Active view | Disallow geometry join on all elements in the active view. |
| **Beam Reset** | Selected | Reset beam start/end extensions and justification to defaults. |
| **Join Advance** | Selected | Toggle join/unjoin geometry between intersecting elements with an intuitive dialog. |
| **Cut Geometry** | Selected | Select one element and automatically cut all intersecting elements by it. |

#### Element Operations

| Command | Description |
|---------|-------------|
| **Flip Elements** | Batch-flip the facing orientation of selected elements. |
| **Rotate Multiple** | Rotate multiple selected elements simultaneously around their individual centers. |

#### Transfer & Filters

| Command | Description |
|---------|-------------|
| **Transfer Templates** | Copy view templates from one project to another. |
| **Copy Filters** | Copy view filters from a source view to multiple target views. |
| **Filters Legend** | Auto-generate a color-coded legend from active view filters. |

#### Grids

| Command | Description |
|---------|-------------|
| **Toggle All Grids** | Show or hide all grid lines in the active view. |
| **Grid 3D & 2D** | Switch selected grids between 3D extents and 2D view-specific display. |

#### Filled Regions

| Command | Description |
|---------|-------------|
| **Merge Regions** | Combine overlapping filled regions into a single unified region. |
| **Regions → Floors** | Convert filled region boundaries directly into floor elements. |
| **Regions → Ceilings** | Convert filled region boundaries into ceiling elements. |
| **Change LineStyle** | Batch-modify the line style of filled region boundaries. |

#### Wall Tools

| Command | Description |
|---------|-------------|
| **Match Top** | Copy the top constraint from a source wall to target walls. |
| **Match Base** | Copy the base constraint from a source wall to target walls. |
| **Match Both** | Copy both top and base constraints to target walls. |
| **Modify Constraints** | Manually set top and base constraints for selected walls. |

#### Dimensioning

| Command | Description |
|---------|-------------|
| **Auto Dims** | Auto-dimension grids, walls, columns, and foundations in the current plan view. |
| **Dim Audit** | Find and fix overlapping or unreadable dimensions in the active view. |

---

## 🔑 AntiGravity Panel

> *Licensing and activation.*

| Command | Description |
|---------|-------------|
| **Hardware ID** | Copy your machine's unique hardware identifier for license activation requests. |
| **License Status** | Check and display the current license activation status. |

---

## 🛠️ Technical Specifications

| Specification | Detail |
|---------------|--------|
| **Target Platforms** | Revit 2022 · 2023 · 2024 · 2025 · 2026 · 2027 |
| **Runtime** | .NET Framework 4.8 (R22–R24) · .NET 8.0 (R25–R26) · .NET 10.0 (R27) |
| **Language** | C# 12 / WPF / XAML |
| **Architecture** | SDK-style multi-targeting with per-version build configurations |
| **UI Framework** | Centralized XAML resource dictionaries (`Pre_BrandStyles.xaml`) with `PremiumWindowChrome` |
| **Ribbon** | YAML-driven panel configuration loaded from embedded resources |
| **Configuration** | User preferences persisted via JSON in `%AppData%` |
| **Licensing** | Hardware-bound activation with embedded or external key validation |

---

## 📦 Installation & Deployment

### Developer Workflow

```bash
# 1. Build for debugging (does NOT deploy to Revit folders)
dotnet build antiGGGravity.csproj -c Debug

# 2. Load via Revit Add-In Manager from:
#    bin\Debug\net8.0-windows\antiGGGravity.dll

# 3. Full deployment (Revit must be closed)
dotnet build antiGGGravity.csproj -c Debug -p:DeployToRevit=true
```

### Multi-Version Build

```bash
# Build for all supported Revit versions
dotnet build -c R22    # → net48
dotnet build -c R23    # → net48
dotnet build -c R24    # → net48
dotnet build -c R25    # → net8.0-windows
dotnet build -c R26    # → net8.0-windows
dotnet build -c R27    # → net10.0-windows
```

### Production Distribution

```bash
# Build with embedded license key
dotnet build -c R26 -p:EmbedLicense=true
```

Deploy `antiGGGravity.addin` and the output folder to:
```
%ProgramData%\Autodesk\Revit\Addins\{YEAR}\
```

---

## 🎯 Target Audience

### Structural Engineers

antiGGGravity was **built for structural engineers first**. The **Rebar Panel** is the primary flagship toolset—automating reinforcement across foundations (footings, bored piles, and varied shape pads), columns, walls, and beams with deep parametric control over hooks, covers, spacing, and multi-level splicing. The **Model Panel** follows as the next flagship, eliminating hours of manual placement for **Roof Framing** and **Bracing assemblies**. Combined with the Visibility Graphic panel's structural packs and the Cut/Join Geometry tools, structural engineers gain hours back on every project.

### Architects & Drafters

The **Quick Filter** and **Quick VG** panels are flagship drafting tools—they replace the tedious native Visibility/Graphics workflow with one-click category toggles, parameter-based color coding, and element highlighting. **Roof Framing** is equally valuable for architects—generating structural framing layouts on complex roof geometry that would otherwise require painstaking manual placement. Architects also benefit from the Overrides Panel (transparency overrides, CAD styling presets), the Management Panel (batch view/sheet management, viewport alignment, crop controls), and the Filled Region tools (convert sketched regions to floors or ceilings for early area studies).

### BIM Coordinators

**Transfer Family** is the flagship coordination tool—transferring live views, drafting views, sheets, view templates, and families between Revit documents in a single operation. Coordinators also rely on the Project Audit Panel for model cleanup (wiping empty tags, auditing text, resolving overlaps), the View Filters ecosystem (Copy Filters across views, auto-generate Filters Legend for coordination meetings), and the Toggle tools for rapidly switching between linked models, CAD imports, and category groups during reviews. **Quick Pick** with its persistent Custom Favorites row becomes a daily workhorse for isolating specific categories.

---

## 📜 Licensing

This project is a proprietary component of the **antiGGGravity** extension ecosystem.  
All rights reserved. Unauthorized distribution or reverse engineering is prohibited.
