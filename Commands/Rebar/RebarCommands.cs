using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Utilities;

using antiGGGravity.Views.Rebar;

using antiGGGravity.Commands;

namespace antiGGGravity.Commands.Rebar
{
    // --- HELPER CLASSES ---
    public class HostSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // 1. Exclude 2D elements (ViewSpecific)
            if (elem.ViewSpecific) return false;

            // 2. Exclude Rebar
            if (elem is Autodesk.Revit.DB.Structure.Rebar) return false;
            
            // 3. Ensure it has a category (safety)
            if (elem.Category == null) return false;

            return true;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }

    // --- SELECTION TOOLS (Column A) ---

    [Transaction(TransactionMode.Manual)]
    public class SetObscuredCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            var collector = new FilteredElementCollector(doc, activeView.Id);
            var rebars = collector.OfCategory(BuiltInCategory.OST_Rebar)
                                  .WhereElementIsNotElementType()
                                  .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                                  .ToList();

            if (rebars.Count == 0)
            {
                TaskDialog.Show("Rebar", "No rebar elements found in the active view.");
                return Result.Succeeded;
            }

            using (Transaction t = new Transaction(doc, "Set Rebars Obscured"))
            {
                t.Start();
                int count = 0;
                foreach (var rebar in rebars)
                {
                    try
                    {
                        // SetUnobscuredInView(view, false) -> Obscured (hidden by concrete)
                        rebar.SetUnobscuredInView(activeView, false);

                        // Note: In Revit 2023+, solidity is controlled by Detail Level (Fine).
                        // SetSolidInView is not needed or removed in newer API.
                        count++;
                    }
                    catch { /* Ignore errors for individual bars */ }
                }
                t.Commit();
                // TaskDialog.Show("Rebar", $"Set {count} rebars to Obscured (Wireframe).");
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class SetUnobscuredCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            var collector = new FilteredElementCollector(doc, activeView.Id);
            var rebars = collector.OfCategory(BuiltInCategory.OST_Rebar)
                                  .WhereElementIsNotElementType()
                                  .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                                  .ToList();

            if (rebars.Count == 0)
            {
                TaskDialog.Show("Rebar", "No rebar elements found in the active view.");
                return Result.Succeeded;
            }

            using (Transaction t = new Transaction(doc, "Set Rebars Unobscured"))
            {
                t.Start();
                int count = 0;
                foreach (var rebar in rebars)
                {
                    try
                    {
                        // SetUnobscuredInView(view, true) -> Unobscured (visible over concrete)
                        rebar.SetUnobscuredInView(activeView, true);
                        
                        // Note: In Revit 2023+, solidity is controlled by Detail Level (Fine).
                        count++;
                    }
                    catch { /* Ignore */ }
                }
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ShowRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            var collector = new FilteredElementCollector(doc, activeView.Id); // Changed to Active View only for performance/safety?
            // Python Script: FilteredElementCollector(revit.doc).OfCategory... -> Whole Doc.
            // Then active_view.UnhideElements(rebar_ids).
            // UnhideElements takes IDs. If ID is not hidden in view, it does nothing.
            // If ID is not in view extent, it does nothing.
            // So iterating whole doc is inefficient but matches script.
            // Optimization: Iterate only hidden elements in view? No, API doesn't allow easy "GetHiddenElements".
            // So we collect all Rebar in Doc.
            
            var allRebars = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_Rebar)
                                .WhereElementIsNotElementType()
                                .ToElementIds();

            if (allRebars.Count == 0) return Result.Succeeded;

            using (Transaction t = new Transaction(doc, "Show All Rebars"))
            {
                t.Start();
                activeView.UnhideElements(allRebars);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class HideRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View activeView = doc.ActiveView;

            // Hide only rebar in active view logic
            var rebars = new FilteredElementCollector(doc, activeView.Id)
                             .OfCategory(BuiltInCategory.OST_Rebar)
                             .WhereElementIsNotElementType()
                             .ToElementIds();

            if (rebars.Count == 0) return Result.Succeeded;

            using (Transaction t = new Transaction(doc, "Hide Rebar"))
            {
                t.Start();
                activeView.HideElements(rebars);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class SelectRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            List<ElementId> hostIds = new List<ElementId>();

            // 1. Check Pre-selection
            var selection = uidoc.Selection.GetElementIds();
            if (selection.Count > 0)
            {
                foreach (var id in selection)
                {
                    Element el = doc.GetElement(id);
                    if (el != null && !el.ViewSpecific && !(el is Autodesk.Revit.DB.Structure.Rebar))
                    {
                        hostIds.Add(id);
                    }
                }
            }
            
            // 2. If no valid pre-selection, Prompt User
            if (hostIds.Count == 0)
            {
                try
                {
                    var picked = uidoc.Selection.PickElementsByRectangle(new HostSelectionFilter(), "Drag a box to select host elements");
                    foreach (var el in picked)
                    {
                        hostIds.Add(el.Id);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            if (hostIds.Count == 0) return Result.Cancelled;

            // 3. Find Hosted Rebar
            var allRebars = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_Rebar)
                                .WhereElementIsNotElementType()
                                .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                                .ToList();

            List<ElementId> hostedRebarIds = new List<ElementId>();
            foreach (var rebar in allRebars)
            {
                if (hostIds.Contains(rebar.GetHostId()))
                {
                    hostedRebarIds.Add(rebar.Id);
                }
            }

            if (hostedRebarIds.Count == 0)
            {
                TaskDialog.Show("Rebar", "No rebar found on selected host elements.");
                return Result.Succeeded;
            }

            // 4. Select Rebar
            uidoc.Selection.SetElementIds(hostedRebarIds);
            // TaskDialog.Show("Rebar", $"Selected {hostedRebarIds.Count} rebars from {hostIds.Count} hosts.");

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class SelectDeleteRebarCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
             UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            List<ElementId> hostIds = new List<ElementId>();

            // 1. Check Pre-selection
            var selection = uidoc.Selection.GetElementIds();
            if (selection.Count > 0)
            {
                foreach (var id in selection)
                {
                    Element el = doc.GetElement(id);
                    if (el != null && el.Category != null && el.Category.Id.Value != (long)BuiltInCategory.OST_Rebar)
                    {
                        hostIds.Add(id);
                    }
                }
            }
            
            // 2. If no valid pre-selection, Prompt User
            if (hostIds.Count == 0)
            {
                try
                {
                    var picked = uidoc.Selection.PickElementsByRectangle(new HostSelectionFilter(), "Drag a box to select host elements for rebar deletion");
                    foreach (var el in picked)
                    {
                        hostIds.Add(el.Id);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            if (hostIds.Count == 0) return Result.Cancelled;

            // 3. Find Hosted Rebar
            var allRebars = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_Rebar)
                                .WhereElementIsNotElementType()
                                .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                                .ToList();

            List<ElementId> hostedRebarIds = new List<ElementId>();
            foreach (var rebar in allRebars)
            {
                if (hostIds.Contains(rebar.GetHostId()))
                {
                    hostedRebarIds.Add(rebar.Id);
                }
            }

            if (hostedRebarIds.Count == 0)
            {
                TaskDialog.Show("Rebar", "No rebar found on selected host elements.");
                return Result.Succeeded;
            }

            // 4. Delete Rebar
            using (Transaction t = new Transaction(doc, "Delete Hosted Rebar"))
            {
                t.Start();
                doc.Delete(hostedRebarIds);
                t.Commit();
            }
            
            TaskDialog.Show("Rebar", $"Deleted {hostedRebarIds.Count} rebars from {hostIds.Count} hosts.");

            return Result.Succeeded;
        }
    }

    // --- SETTING TOOLS (Column B) ---

    [Transaction(TransactionMode.Manual)]
    public class HideRebarByHostCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // 1. Check Pre-selection
            var selectionIds = uidoc.Selection.GetElementIds();
            List<Element> preSelectedHosts = new List<Element>();
            if (selectionIds.Count > 0)
            {
                foreach (var eid in selectionIds)
                {
                    Element elem = doc.GetElement(eid);
                    if (elem != null && !elem.ViewSpecific && !(elem is Autodesk.Revit.DB.Structure.Rebar))
                    {
                        preSelectedHosts.Add(elem);
                    }
                }
            }

            if (preSelectedHosts.Count > 0)
            {
                // If pre-selection exists, just process it and exit
                HideRebarsOfHosts(doc, activeView, preSelectedHosts);
            }
            else
            {
                // 2. No pre-selection, enter continuous pick mode
                while (true)
                {
                    try
                    {
                        Reference refElem = uidoc.Selection.PickObject(
                            ObjectType.Element, 
                            new HostSelectionFilter(),
                            "Pick a host element to hide its rebar (Press ESC to finish)"
                        );
                        Element hostElem = doc.GetElement(refElem);
                        HideRebarsOfHosts(doc, activeView, new List<Element> { hostElem });
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break; // User pressed ESC
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }

            return Result.Succeeded;
        }

        private void HideRebarsOfHosts(Document doc, View view, List<Element> hostElems)
        {
            if (hostElems.Count == 0) return;

            var rebarsInView = new FilteredElementCollector(doc, view.Id)
                                .OfCategory(BuiltInCategory.OST_Rebar)
                                .WhereElementIsNotElementType()
                                .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                                .ToList();

            var hostIds = new HashSet<ElementId>(hostElems.Select(e => e.Id));
            List<ElementId> idsToHide = new List<ElementId>();

            foreach (var r in rebarsInView)
            {
                if (hostIds.Contains(r.GetHostId()))
                {
                    idsToHide.Add(r.Id);
                }
            }

            if (idsToHide.Count > 0)
            {
                using (Transaction t = new Transaction(doc, "Hide Rebars for Selected Hosts"))
                {
                    t.Start();
                    try
                    {
                        view.HideElements(idsToHide);
                        t.Commit();
                    }
                    catch
                    {
                        t.RollBack();
                    }
                }
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ShowRebarByHostCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // 1. Check Pre-selection
            var selectionIds = uidoc.Selection.GetElementIds();
            List<Element> preSelectedHosts = new List<Element>();
            if (selectionIds.Count > 0)
            {
                foreach (var eid in selectionIds)
                {
                    Element elem = doc.GetElement(eid);
                    if (elem != null && !elem.ViewSpecific && !(elem is Autodesk.Revit.DB.Structure.Rebar))
                    {
                        preSelectedHosts.Add(elem);
                    }
                }
            }

            if (preSelectedHosts.Count > 0)
            {
                UnhideRebarsOfHosts(doc, activeView, preSelectedHosts);
            }
            else
            {
                // 2. No pre-selection, enter continuous pick mode
                while (true)
                {
                    try
                    {
                        Reference refElem = uidoc.Selection.PickObject(
                            ObjectType.Element, 
                            new HostSelectionFilter(),
                            "Pick a host element to show its rebar (Press ESC to finish)"
                        );
                        Element hostElem = doc.GetElement(refElem);
                        UnhideRebarsOfHosts(doc, activeView, new List<Element> { hostElem });
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }

            return Result.Succeeded;
        }

        private void UnhideRebarsOfHosts(Document doc, View view, List<Element> hostElems)
        {
            if (hostElems.Count == 0) return;

            var rebars = new FilteredElementCollector(doc)
                            .OfCategory(BuiltInCategory.OST_Rebar)
                            .WhereElementIsNotElementType()
                            .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                            .ToList();

            var hostIds = new HashSet<ElementId>(hostElems.Select(e => e.Id));
            List<ElementId> idsToShow = new List<ElementId>();

            foreach (var r in rebars)
            {
                if (hostIds.Contains(r.GetHostId()))
                {
                    idsToShow.Add(r.Id);
                }
            }

            if (idsToShow.Count > 0)
            {
                using (Transaction t = new Transaction(doc, "Unhide Rebars for Selected Hosts"))
                {
                    t.Start();
                    try
                    {
                        view.UnhideElements(idsToShow);
                        t.Commit();
                    }
                    catch
                    {
                        t.RollBack();
                    }
                }
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ShowRebarByHostOnlyCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // 1. Check Pre-selection
            var selectionIds = uidoc.Selection.GetElementIds();
            List<Element> preSelectedHosts = new List<Element>();
            if (selectionIds.Count > 0)
            {
                foreach (var eid in selectionIds)
                {
                    Element elem = doc.GetElement(eid);
                    if (elem != null && !elem.ViewSpecific && !(elem is Autodesk.Revit.DB.Structure.Rebar))
                    {
                        preSelectedHosts.Add(elem);
                    }
                }
            }

            if (preSelectedHosts.Count > 0)
            {
                IsolateRebarsForHosts(doc, activeView, preSelectedHosts);
            }
            else
            {
                // 2. Continuous Pick Mode
                while (true)
                {
                    try
                    {
                        Reference refElem = uidoc.Selection.PickObject(
                            ObjectType.Element, 
                            new HostSelectionFilter(),
                            "Pick a host to ISOLATE its rebar (Press ESC to finish)"
                        );
                        Element hostElem = doc.GetElement(refElem);
                        IsolateRebarsForHosts(doc, activeView, new List<Element> { hostElem });
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
            }

            return Result.Succeeded;
        }

        private void IsolateRebarsForHosts(Document doc, View view, List<Element> hostElems)
        {
            if (hostElems.Count == 0) return;

            var allRebars = new FilteredElementCollector(doc)
                                .OfCategory(BuiltInCategory.OST_Rebar)
                                .WhereElementIsNotElementType()
                                .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                                .ToList();

            var hostIds = new HashSet<ElementId>(hostElems.Select(e => e.Id));
            List<ElementId> idsToShow = new List<ElementId>();
            List<ElementId> idsToHide = new List<ElementId>();

            foreach (var r in allRebars)
            {
                if (hostIds.Contains(r.GetHostId()))
                    idsToShow.Add(r.Id);
                else
                    idsToHide.Add(r.Id);
            }

            using (Transaction t = new Transaction(doc, "Show Reo By Host Only"))
            {
                t.Start();
                try
                {
                    if (idsToShow.Count > 0) view.UnhideElements(idsToShow);
                    if (idsToHide.Count > 0) view.HideElements(idsToHide);
                    t.Commit();
                }
                catch
                {
                    t.RollBack();
                }
            }
        }
    }

    // --- REINFORCING TOOLS (Column C) ---














}
