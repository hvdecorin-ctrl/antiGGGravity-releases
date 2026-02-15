using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB.Structure;

namespace antiGGGravity.Commands.General
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class AllowJoinCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selectedIds = uidoc.Selection.GetElementIds();
            if (!selectedIds.Any())
            {
                TaskDialog.Show("Allow Join", "Please select elements to allow join.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Allow Join"))
            {
                t.Start();
                foreach (ElementId id in selectedIds)
                {
                    Element el = doc.GetElement(id);
                    if (el == null) continue;

                    if (el.Category == null) continue;
                    long catVal = el.Category.Id.Value;

                    if (catVal == (long)BuiltInCategory.OST_StructuralFraming)
                    {
                        try 
                        { 
                            StructuralFramingUtils.AllowJoinAtEnd((FamilyInstance)el, 0); 
                            StructuralFramingUtils.AllowJoinAtEnd((FamilyInstance)el, 1); 
                        } catch { }
                    }
                    else if (catVal == (long)BuiltInCategory.OST_Walls)
                    {
                        try 
                        { 
                            WallUtils.AllowWallJoinAtEnd((Wall)el, 0); 
                            WallUtils.AllowWallJoinAtEnd((Wall)el, 1); 
                        } catch { }
                    }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class AllowJoinAllCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var framing = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            if (!framing.Any())
            {
                TaskDialog.Show("Allow Join", "No structural framing found in current view.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Allow Join - All Framing in View"))
            {
                t.Start();
                foreach (var f in framing)
                {
                    try 
                    { 
                        StructuralFramingUtils.AllowJoinAtEnd(f, 0); 
                        StructuralFramingUtils.AllowJoinAtEnd(f, 1); 
                    } catch { }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DisAllowJoinCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var selectedIds = uidoc.Selection.GetElementIds();
            if (!selectedIds.Any())
            {
                TaskDialog.Show("Disallow Join", "Please select elements to disallow join.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Disallow Join"))
            {
                t.Start();
                foreach (ElementId id in selectedIds)
                {
                    Element el = doc.GetElement(id);
                    if (el == null) continue;

                    if (el.Category == null) continue;
                    long catVal = el.Category.Id.Value;

                    if (catVal == (long)BuiltInCategory.OST_StructuralFraming)
                    {
                        try 
                        { 
                            StructuralFramingUtils.DisallowJoinAtEnd((FamilyInstance)el, 0); 
                            StructuralFramingUtils.DisallowJoinAtEnd((FamilyInstance)el, 1); 
                        } catch { }
                    }
                    else if (catVal == (long)BuiltInCategory.OST_Walls)
                    {
                        try 
                        { 
                            WallUtils.DisallowWallJoinAtEnd((Wall)el, 0); 
                            WallUtils.DisallowWallJoinAtEnd((Wall)el, 1); 
                        } catch { }
                    }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class DisAllowJoinAllCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var framing = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            if (!framing.Any())
            {
                TaskDialog.Show("Disallow Join", "No structural framing found in current view.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Disallow Join - All Framing in View"))
            {
                t.Start();
                foreach (var f in framing)
                {
                    try 
                    { 
                        StructuralFramingUtils.DisallowJoinAtEnd(f, 0); 
                        StructuralFramingUtils.DisallowJoinAtEnd(f, 1); 
                    } catch { }
                }
                t.Commit();
            }
            return Result.Succeeded;
        }
    }

    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class BeamResetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var frames = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .ToList();

            int removed_copes = 0;
            int reset_ext_count = 0;
            int reset_cutback_count = 0;

            double offsetValue = -10.0 / 304.8;
            double cutbackValue = 10.0 / 304.8;

            using (Transaction t = new Transaction(doc, "Remove Coping + Reset Extensions"))
            {
                t.Start();
                foreach (var f in frames)
                {
                    // 1) Coping
                    /* 
                    try 
                    { 
                        if (Autodesk.Revit.DB.Structure.CopingUtils.HasCoping(f)) 
                        { 
                            Autodesk.Revit.DB.Structure.CopingUtils.RemoveCoping(f); 
                            removed_copes++; 
                        } 
                    } catch { } 
                    */
                    // TODO: Revit 2026 check for CopingUtils location

                    // 2) Extensions
                    reset_ext_count += ResetParams(f, new[] { "Start Extension", "End Extension" }, offsetValue);

                    // 3) Cutbacks
                    reset_cutback_count += ResetParams(f, new[] { "Start Join Cutback", "End Join Cutback" }, cutbackValue);
                }
                t.Commit();
            }

            TaskDialog.Show("Remove Coping + Reset Extensions", 
                $"Copes removed: {removed_copes}\nExtensions reset: {reset_ext_count}\nCutbacks reset: {reset_cutback_count}");

            return Result.Succeeded;
        }

        private int ResetParams(FamilyInstance fi, string[] names, double val)
        {
            int count = 0;
            foreach (string name in names)
            {
                Parameter p = fi.LookupParameter(name);
                if (p != null && !p.IsReadOnly)
                {
                    try { p.Set(val); count++; } catch { }
                }
            }
            return count;
        }
    }

}
