---
description: Convert pyRevit Python tools to C# for Revit addin - strictly match original behavior
---

# pyRevit Python to C# Conversion Workflow

## Overview
This workflow converts pyRevit Python (.py) tools into C# for the antiGGGravity Revit addin. The conversion must **strictly comply with the original Python code** to maintain identical functionality.

## Prerequisites
- Source pyRevit extension: `C:\Users\DELL\AppData\Roaming\pyRevit\Extensions\antiGGGGravity.extension`
- Target C# project: `C:\Users\DELL\source\repos\antiGGGravity`
- Revit API references for 2025/2026

---

## Step 1: Analyze Python Source

1. **Locate the tool folder** in the pyRevit extension (e.g., `*.pushbutton\script.py`)
2. **Study the Python script thoroughly**:
   - Identify imports and Revit API classes used
   - Map all functions and their purposes
   - Note all user inputs (forms, dialogs)
   - Document all calculations and element creation logic
   - Identify any pyRevit-specific helpers (`rpw`, `forms`, etc.)

### Key Python → C# Mappings

| Python (pyRevit/IronPython) | C# (.NET) |
|-----------------------------|-----------|
| `from Autodesk.Revit.DB import *` | `using Autodesk.Revit.DB;` |
| `from pyrevit import forms` | WPF Window (XAML + code-behind) |
| `doc = __revit__.ActiveUIDocument.Document` | `UIDocument uidoc = commandData.Application.ActiveUIDocument;` |
| `with Transaction(doc, "name") as t:` | `using (Transaction t = new Transaction(doc, "name"))` |
| `XYZ(x, y, z)` | `new XYZ(x, y, z)` |
| `FilteredElementCollector(doc)` | `new FilteredElementCollector(doc)` |
| `list comprehension [x for x in items]` | `.Where(x => ...).ToList()` LINQ |
| `dict.get("key", default)` | `dict.TryGetValue("key", out val) ? val : default` |
| `forms.alert("message")` | `TaskDialog.Show("Title", "message");` |
| `forms.pick_option(...)` | WPF ComboBox or RadioButtons |
| `uidoc.Selection.PickObjects(...)` | `uidoc.Selection.PickObjects(ObjectType.Element, ...)` |

---

## Step 2: Create C# Command Class

1. **Create command file** in `Commands\` folder:
   ```csharp
   [Transaction(TransactionMode.Manual)]
   public class ToolNameCommand : IExternalCommand
   {
       public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
       {
           UIDocument uidoc = commandData.Application.ActiveUIDocument;
           Document doc = uidoc.Document;
           
           try
           {
               // Implementation matching Python logic
               return Result.Succeeded;
           }
           catch (Exception ex)
           {
               message = ex.Message;
               return Result.Failed;
           }
       }
   }
   ```

2. **Register command** in appropriate registry file or ribbon setup

---

## Step 3: Create WPF View (if dialog exists)

If the Python tool uses `forms.WPFWindow` or similar:

1. **Create XAML file** in `Views\ToolNameView.xaml`
2. **Create code-behind** in `Views\ToolNameView.xaml.cs`

### Premium View Pattern (MANDATORY):

#### 1. XAML Template (`Views\ToolNameView.xaml`):
```xml
<Window x:Class="antiGGGravity.Views.ToolNameView"
        WindowStyle="None" AllowsTransparency="True" Background="Transparent"
        WindowChrome.WindowChrome="{StaticResource PremiumWindowChrome}">
    
    <!-- Resources are merged in code-behind via SharedResources to prevent latency -->
    
    <Border Style="{StaticResource PremiumBorderStyle}">
        <Grid>
            <Grid.RowDefinitions>
                <RowDefinition Height="55"/> <!-- Standard Header Height -->
                <RowDefinition Height="*"/>
            </Grid.RowDefinitions>
            
            <!-- HEADER -->
            <Border Grid.Row="0" Background="{StaticResource BrandGradientBrush}" CornerRadius="12,12,0,0">
                <!-- Header Content (Logo, Title, Close Button) -->
            </Border>
            
            <!-- CONTENT -->
            <Grid Grid.Row="1" Margin="{StaticResource PremiumContentMargin}">
                <!-- Tool UI Here -->
            </Grid>
        </Grid>
    </Border>
</Window>
```

#### 2. Code-Behind Template (`Views\ToolNameView.xaml.cs`):
```csharp
using antiGGGravity.Utilities;

public partial class ToolNameView : Window
{
    public ToolNameView(Document doc)
    {
        // 1. Merge shared resources BEFORE InitializeComponent (Zero-Latency)
        this.Resources.MergedDictionaries.Add(SharedResources.GlobalResources);
        
        InitializeComponent();
        
        // 2. Standard Init
        _doc = doc;
        LoadData();
        LoadSettings();
    }
}
```

---

## Step 4: Convert Logic - Match Python Exactly

### Critical Rule
When converting calculations and logic, **do not simplify or optimize** - replicate the Python logic step-by-step to ensure identical behavior.

### Common Patterns:

**Element Location Extraction:**
```csharp
// Python: curve = element.Location.Curve
Curve pathCurve = null;
if (element.Location is LocationCurve locCurve)
    pathCurve = locCurve.Curve;

// Python: transform for FamilyInstance
if (element is FamilyInstance fi)
{
    Transform trans = fi.GetTransform();
    XYZ dir = trans.BasisX.Normalize();
}

// Python: rotation for LocationPoint  
if (element.Location is LocationPoint locPt)
{
    double rot = locPt.Rotation;
    XYZ dir = new XYZ(Math.Cos(rot), Math.Sin(rot), 0);
}
```

**Parameter Access:**
```csharp
// Python: param = element.LookupParameter("Name")
Parameter param = element.LookupParameter("Name");
if (param != null && param.HasValue)
{
    double value = param.AsDouble();
    string text = param.AsString();
}
```

---

## Step 5: Settings Persistence

All views should remember their last-used settings:

```csharp
using antiGGGravity.Utilities;

// In LoadSettings():
UI_Text_Value.Text = SettingsManager.Get(VIEW_NAME, "Value", "default");
UI_Check_Option.IsChecked = SettingsManager.GetBool(VIEW_NAME, "Option", true);

// In SaveSettings():
SettingsManager.Set(VIEW_NAME, "Value", UI_Text_Value.Text);
SettingsManager.Set(VIEW_NAME, "Option", (UI_Check_Option.IsChecked == true).ToString());
SettingsManager.SaveAll();  // IMPORTANT: Call at end
```

---

## Step 6: Build and Deploy

// turbo-all
```bash
# Build
cd C:\Users\DELL\source\repos\antiGGGravity
dotnet build -c Release

# Deploy
Copy-Item -Path "Resources\antiGGGravity.addin" -Destination "C:\ProgramData\Autodesk\Revit\Addins\2026\antiGGGravity.addin" -Force
```

---

## Checklist

- [ ] Python script fully analyzed
- [ ] Command class created with `[Transaction(TransactionMode.Manual)]`
- [ ] WPF View created (if dialog needed)
- [ ] Settings persistence implemented
- [ ] All logic matches Python exactly (no optimization)
- [ ] Build succeeds
- [ ] Tested in Revit matches Python behavior
