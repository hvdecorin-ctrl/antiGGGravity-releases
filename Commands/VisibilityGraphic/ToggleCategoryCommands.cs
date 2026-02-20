using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using antiGGGravity.Commands;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    // --- BASIC CATEGORIES ---
    
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleWallsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Walls; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleFloorsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Floors; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleRebarCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Rebar; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleColumnsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_StructuralColumns; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleFramingCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_StructuralFraming; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleFoundationsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_StructuralFoundation; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleGridsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Grids; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleLevelsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Levels; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleRoofsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Roofs; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleStairsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Stairs; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleLinksCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_RvtLinks; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleSectionBoxesCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_SectionBox; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleConnectionsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_StructConnections; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleRefPlanesCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_CLines; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleScopeBoxesCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_VolumeOfInterest; }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class ToggleSlabsCommand : ToggleCategoryBaseCommand { protected override BuiltInCategory Category => BuiltInCategory.OST_Floors; } // Slab is usually Floor

}
