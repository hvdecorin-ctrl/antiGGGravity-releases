# antiGGGravity | Revit Engineering Ecosystem

[![Revit 2025](https://img.shields.io/badge/Revit-2025-blue.svg)](https://www.autodesk.com/products/revit/overview)
[![Revit 2026](https://img.shields.io/badge/Revit-2026-blue.svg)](https://www.autodesk.com/products/revit/overview)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blueviolet.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)

**antiGGGravity** is a high-performance C# Add-in for Autodesk Revit, engineered for structural specialists, architects, and BIM coordinators. It delivers a premium suite of **70+ tools** spanning rebar automation, graphic overrides, view management, model generation, and rapid visibility controls—all wrapped in a modern, glassmorphism-inspired interface.

---

## �️ Panel Overview

| Panel | Focus | Tools |
|---|---|---|
| **Project Audit** | Project hygiene, tagging, family management | 8 |
| **Overrides** | Graphic overrides, color coding, transparency | 15 |
| **Management** | View, sheet, crop, and viewport control | 13 |
| **Visibility Graphic** | Category & element type visibility toggles | 20 |
| **Rebar** | Parametric reinforcement generation | 3 |
| **Model** | Structural model generation (framing, bracing) | 4 |
| **General** | Geometry, selection, filters, walls, regions | 25+ |

---

## 📋 Detailed Command Reference

### 🔎 Project Audit Panel
*Keep your model clean, organized, and audit-ready.*

| Command | Description |
|---|---|
| **Project Folder** | Instantly opens the file explorer at the central model location—no more navigating through nested directories. |
| **Wipe Empty Tags** | Scans and removes tags with blank or null values, eliminating visual clutter from your sheets. |
| **Title on Active View** | Stamps the view title onto the active view for quick identification during reviews. |
| **Title on Sheets** | Batch-applies title metadata across all sheet views in one click. |
| **Drafter Text Off / On** | Toggle drafter-specific text annotations on or off project-wide—ideal for submission cleanup. |
| **Family Loading** | Smart family loader with category-aware selection. |
| **Load More Type** | Add additional family types to already-loaded families without reloading. |
| **Duplicate Families** | Clone existing families with unique naming for design iteration. |

---

### 🎨 Overrides Panel
*Take full control of graphic presentation without touching Visibility/Graphics.*

| Command | Description |
|---|---|
| **Color Splasher** | Visualize any parameter's values as color-coded overrides—perfect for clash status, phases, or mark values. |
| **DimFake** | Override dimension text to display custom values while preserving actual geometry dimensions. |
| **Text Audit** | Scan and audit all text in the project for consistency and standards compliance. |
| **Text Upper** | Batch-convert selected text annotations to uppercase for drawing standards. |
| **CAD Override Styles** | 5 pre-built graphic override presets (Black, Orange, Blue, Green, Purple themes) for imported CAD backgrounds. Apply transparency, halftone, and color in one click. |
| **Project CAD Override** | Apply a standardized CAD import graphic override across the entire project. |
| **60% / 100% Transparency** | Instantly set surface transparency on selected elements to 60% or fully transparent. |
| **Match Overrides** | Eyedropper tool—pick a source element and apply its graphic override to others. |
| **Reset Selected / All** | Strip graphic overrides from selected elements or the entire active view. |
| **Disable / Enable / Remove Filters** | Batch manage view filters: temporarily disable them, re-enable, or permanently remove from the view. |

---

### � Management Panel
*Streamline view, sheet, and viewport workflows at scale.*

| Command | Description |
|---|---|
| **Rename Views (Active Sheet)** | Batch-rename all views placed on the currently active sheet based on a naming convention. |
| **Rename Views (All Sheets)** | Apply a consistent naming standard to views across every sheet in the project. |
| **Renumber Viewports** | Sequentially re-number viewport detail numbers on a sheet for clean presentation. |
| **Add Selected View** | Place the currently selected view onto a target sheet with precise positioning. |
| **Add Views** | Batch-place multiple views onto sheets in a single operation. |
| **Duplicate Sheets** | Clone sheets with their titleblock, viewport layout, and configurations. |
| **Set Crop View** | Interactively set or adjust the crop region boundary on section and plan views. |
| **Toggle Crop** | One-click crop region visibility toggle for the active view. |
| **Zoom To** | Instantly zoom to the bounding box of selected elements—no more manual pan-and-zoom. |
| **Auto 3D** | Generate a default 3D view of selected elements with automatic orientation and section box. |
| **Toggle 3D** | Toggle the section box on/off in the active 3D view. |
| **Toggle Section Box** | Toggle the visibility of the section box outline in the active 3D view. |
| **Align Schematic** | Precisely align viewports across multiple sheets for consistent drawing packages. |

---

### 👁️ Visibility Graphic Panel
*Control what you see in seconds, not minutes.*

| Command | Description |
|---|---|
| **Toggle All Elements** | Master switch—show or hide all element categories in one action. |
| **Toggle 2D / 3D Elements** | Isolate either 2D annotations or 3D model elements with a single click. |
| **Toggle Structural Packs** | Show/hide all structural categories as a group (foundations, framing, columns, rebar). |
| **Toggle Foundations** | Isolate or hide all structural foundation elements. |
| **Toggle Walls** | Quickly show/hide all wall elements. |
| **Toggle Floors (Slabs)** | Toggle floor and slab visibility. |
| **Toggle Columns** | Show/hide structural columns. |
| **Toggle Framing** | Show/hide structural framing (beams, braces, trusses). |
| **Toggle Roof** | Toggle roof element visibility. |
| **Toggle Connections** | Show/hide structural connection elements. |
| **Toggle Rebar** | Show/hide reinforcement bars and rebar sets. |
| **Toggle Grids** | Toggle grid line visibility. |
| **Toggle Levels** | Toggle level datum visibility. |
| **Toggle Ref Planes** | Show/hide reference planes. |
| **Toggle Scope Box** | Toggle scope box visibility. |
| **Toggle CAD Links** | Show/hide imported CAD files in the active view. |
| **Toggle Revit Links** | Show/hide linked Revit models in the active view. |
| **Quick VG** | Open the Quick Visibility Graphic manager—a streamlined, category-level batch toggle interface that replaces the native VG dialog workflow. |

---

### 🏗️ Rebar Panel
*Automate structural reinforcement from design to documentation.*

| Command | Description |
|---|---|
| **Rebar Suite** | The flagship tool—a unified parametric reinforcement generator supporting **Columns, Beams, Walls, Strip Footings, Footing Pads, and Wall Corners** (U-Bar & L-Bar). Features include smart hook overrides, configurable cover offsets, distribution spacing, and automatic rebar comments for scheduling. |
| **Rebar Palette** | A floating toolkit providing quick access to rebar visibility toggles, selection helpers, and host-based rebar tools—always available without navigating the ribbon. |
| **Rebar Quantity** | Generate an instant summary of rebar quantities organized by host category and bar diameter—ideal for quick takeoff checks and material estimation. |

---

### 🏢 Model Panel — Parametric Structure Generator
*Turn hours of manual placement into seconds of intelligent automation.*

The Model Panel is a **flagship toolset** that goes beyond documentation—it actually **builds your structural model for you**. Instead of placing individual framing members one-by-one, these tools analyze your geometry and generate complete parametric assemblies in a single operation.

#### 🏠 Roof Framing Engine
> Select a roof or slab → choose your member family → define spacing → **instant purlin and rafter generation across the entire surface.**

Automatically distributes structural framing members along roof and slab faces with full control over spacing, offsets, and member sizes. Whether it's a simple gable or a complex hip roof, the engine reads the host geometry and places members with correct orientation and structural intent. What would take an engineer 30+ minutes of repetitive placement is done in under 10 seconds.

#### 🔩 Bracing Generation Suite
A complete parametric bracing system for steel structures:

| Pattern | Description |
|---|---|
| **X-Brace** | Generates crossed diagonal bracing between structural bays. Select two columns or grid intersections, and the tool creates both diagonals with proper connections—the backbone of lateral stability systems. |
| **K-Brace** | Creates chevron (inverted-V) bracing with a configurable apex point. The tool calculates member angles, lengths, and connection geometry automatically—critical for seismic and wind-resistant design. |
| **H-Frame** | Produces horizontal multi-brace configurations (girts, struts) between columns at specified elevations. Ideal for industrial steel structures, portal frames, and equipment platforms. |

> 💡 **Why it matters**: Manual bracing placement requires calculating angles, reference plan, trimming members, and adjusting connections for every single brace. The Model Panel eliminates this entirely—select your bays, choose a pattern, and the complete bracing assembly is generated with structural accuracy.

---

### ⚙️ General Panel
*The Swiss Army knife for everyday Revit productivity.*

| Command | Description |
|---|---|
| **Quick Pick** | Category-based element picker with **Quick Pick buttons**, **auto-finish 1-shot box selection**, and a **Custom Favorites row** that persists across sessions. Supports 3D-only and 2D-only filtering. |
| **Allow / Disallow Join** | Control element join behavior on selected elements or the entire view. Prevent unwanted geometry merges at beam-column-wall intersections. |
| **Beam Reset** | Reset beam join settings to their default state, resolving common framing display issues. |
| **Join Geometry** | Batch join or unjoin geometry between intersecting elements with an intuitive dialog. |
| **Cut Geometry** | Select a single element (beam, column, etc.) and automatically cut all intersecting elements by it—dramatically faster than the native one-by-one approach. |
| **Toggle All Grids** | Master toggle for all grid line visibility. |
| **Grid 3D & 2D** | Switch grid representation between 3D extents and 2D view-specific display. |
| **Flip Elements** | Batch-flip the facing orientation of selected elements (beams,walls, doors, windows). |
| **Transfer Templates** | Transfer view templates between views for consistent presentation. |
| **Copy Filters** | Synchronize view filters from a source view to multiple target views—essential for maintaining drawing standards. |
| **Filters Legend** | Auto-generate a color-coded legend sheet from active view filters for presentation and coordination. |
| **Rotate Multiple** | Rotate multiple selected elements simultaneously around their individual centers. |
| **Merge Regions** | Combine overlapping filled regions into a single unified region. |
| **Regions → Floors** | Convert filled region boundaries directly into floor elements—great for early-stage area studies. |
| **Regions → Ceilings** | Convert filled region boundaries into ceiling elements. |
| **Change Line Style** | Batch-modify the line style of filled region boundaries. |
| **Wall Match Top / Base / Both** | Copy the top constraint, base constraint, or both from a source wall to target walls—instant wall height standardization. |
| **Modify Wall Constraints** | Fine-tune wall top/base offsets and constraints through a dedicated dialog. |

---

### 🔑 AntiGravity Panel
*Licensing and diagnostics.*

| Command | Description |
|---|---|
| **Hardware ID** | Copy your machine's unique hardware identifier to the clipboard for license activation requests. |
| **License Status** | Check and display the current license activation status. |
| **Inspect Resources** | Developer/debug tool to list all embedded resources in the add-in assembly. |

---

## 🛠️ Technical Specifications

| Specification | Detail |
|---|---|
| **Target Platforms** | Revit 2025, Revit 2026 |
| **Runtime** | .NET 8.0 (Core) |
| **Language** | C# / WPF |
| **UI Framework** | Centralized XAML resource dictionaries (`Pre_BrandStyles.xaml`) with standardized `PremiumWindowChrome` |
| **Configuration** | User preferences persisted via JSON in `%AppData%` |

---

## 📦 Installation & Deployment

### For Developers (Standard Workflow)
1. **Build for Debugging**: Run a standard build to generate the DLL:
   `dotnet build antiGGGravity.csproj -c Debug`
   *(This does NOT auto-deploy to Revit folders, avoiding file-lock issues during active sessions).*
2. **Load via Add-in Manager**: Use the Revit Add-In Manager to load and debug the DLL directly from:
   `bin\Debug\net8.0-windows\antiGGGravity.dll`
3. **Deployment**: Only run the full deployment when Revit is closed or a restart is planned:
   `dotnet build antiGGGravity.csproj -c Debug -p:DeployToRevit=true`
   *(Or use the `/deploy` workflow).*

### For Production
1. Build the solution in **Release** configuration.
2. Deploy the `antiGGGravity.addin` manifest to:
   - `%ProgramData%\Autodesk\Revit\Addins\2025\`
   - `%ProgramData%\Autodesk\Revit\Addins\2026\`

---

## 🎯 Who Is This For?

### 🏗️ Structural Engineers
antiGGGravity was **built for structural engineers first**. The **Rebar Suite** automates the most tedious part of structural detailing—placing reinforcement bars across columns, beams, walls, and footings with parametric control over hooks, covers, spacing, and corner conditions. Combined with the **Visibility Graphic** panel's structural packs and the **Cut Geometry** / **Join Geometry** tools, structural engineers gain hours back on every project.

### 🏠 Architects
Architects benefit from the **Overrides Panel** (Color Splasher for design option visualization, quick transparency overrides), the **Management Panel** (batch view/sheet management, viewport alignment, crop controls), and the **Filled Region tools** (convert sketched regions to floors or ceilings for early area studies). The **Quick VG** tool alone replaces dozens of clicks in the native Visibility/Graphics dialog.

### 🤝 BIM Coordinators
Coordinators will find the **Project Audit Panel** indispensable for model cleanup (wiping empty tags, auditing text), the **View Filters Ecosystem** (copy filters across views, generate legends for coordination meetings), and the **Toggle tools** for rapidly switching between linked models, CAD imports, and category groups during clash reviews. The **Pick Elements** tool with its custom favorites row becomes a daily workhorse for isolating specific categories during model reviews.

---

## 📜 Licensing
This project is an integral component of the **AntiGravity** extension ecosystem. All rights reserved.
