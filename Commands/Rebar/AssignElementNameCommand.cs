using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.Rebar;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Tool 2: Assigns the built-in Mark parameter on structural elements using TypeMark-Number or CustomName-Number.
    /// Revit auto-propagates host Mark → rebar Host Mark.
    /// Supports Auto (Left→Right) and Manual (click-to-number) modes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignElementNameCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        private static readonly BuiltInCategory[] TargetCategories = new[]
        {
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_GenericModel
        };

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc?.Document;

            if (doc == null)
            {
                TaskDialog.Show("Assign Mark", "Please open a project first.");
                return Result.Cancelled;
            }

            // Show options dialog
            var dialog = new AssignElementNameView();
            
            // Set Revit as owner
            try
            {
                var process = System.Diagnostics.Process.GetCurrentProcess();
                var wrapper = new System.Windows.Interop.WindowInteropHelper(dialog);
                wrapper.Owner = process.MainWindowHandle;
            }
            catch { }

            dialog.ShowDialog();

            if (!dialog.UserConfirmed) return Result.Cancelled;

            // Get user choices
            var namingMode = dialog.SelectedNamingMode;
            var numberingRule = dialog.SelectedNumberingRule;
            var scope = dialog.SelectedScope;
            string customPrefix = dialog.CustomNamePrefix;

            // Manual mode: user clicks elements one by one
            if (numberingRule == AssignElementNameView.NumberingRule.Manual)
            {
                return RunManualMode(uiDoc, doc, namingMode, customPrefix);
            }

            // Auto mode: collect and sort left-to-right
            return RunAutoMode(doc, namingMode, scope, customPrefix);
        }

        /// <summary>
        /// Auto mode: collects all structural elements, groups by TypeMark, sorts left→right, numbers sequentially.
        /// </summary>
        private Result RunAutoMode(Document doc,
            AssignElementNameView.NamingMode namingMode,
            AssignElementNameView.ScopeOption scope,
            string customPrefix)
        {
            // Collect elements
            var allElements = CollectStructuralElements(doc, scope == AssignElementNameView.ScopeOption.ActiveView);

            if (allElements.Count == 0)
            {
                TaskDialog.Show("Assign Mark", "No structural elements found in the selected scope.");
                return Result.Succeeded;
            }

            // Group by prefix (TypeMark or custom)
            var groups = GroupByPrefix(doc, allElements, namingMode, customPrefix);

            int totalUpdated = 0;

            using (Transaction t = new Transaction(doc, "Assign Mark (Auto)"))
            {
                t.Start();

                foreach (var kvp in groups)
                {
                    string prefix = kvp.Key;
                    var elems = kvp.Value;

                    // Sort left → right (by X coordinate), then bottom → top (by Y)
                    var sorted = SortLeftToRight(doc, elems);

                    int counter = 1;
                    foreach (var elem in sorted)
                    {
                        string name = $"{prefix}-{counter}";
                        // Write to the built-in Mark parameter
                        Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                        if (param != null && !param.IsReadOnly)
                        {
                            param.Set(name);
                            totalUpdated++;
                        }
                        counter++;
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Assign Mark",
                $"Auto-naming complete!\n\n" +
                $"  ✓ Updated: {totalUpdated} elements\n" +
                $"  📦 Groups: {groups.Count} unique prefixes\n\n" +
                $"Revit will auto-propagate Mark → rebar Host Mark.");

            return Result.Succeeded;
        }

        /// <summary>
        /// Manual mode: user clicks elements one by one to assign sequential numbers.
        /// </summary>
        private Result RunManualMode(UIDocument uiDoc, Document doc,
            AssignElementNameView.NamingMode namingMode,
            string customPrefix)
        {
            TaskDialog.Show("Manual Numbering",
                "Click structural elements one by one to assign numbers.\n\n" +
                "  • Each click assigns the next number (1, 2, 3...)\n" +
                "  • Press ESC or right-click to finish.\n\n" +
                (namingMode == AssignElementNameView.NamingMode.TypeMark
                    ? "Prefix: element's Type Mark"
                    : $"Prefix: \"{customPrefix}\""));

            int counter = 1;
            int updated = 0;

            // Build a multi-category filter for selection
            var filter = new StructuralElementSelectionFilter();

            using (Transaction t = new Transaction(doc, "Assign Mark (Manual)"))
            {
                t.Start();

                while (true)
                {
                    try
                    {
                        Reference pickedRef = uiDoc.Selection.PickObject(
                            ObjectType.Element,
                            filter,
                            $"Click element #{counter} (ESC to finish)");

                        if (pickedRef == null) break;

                        Element elem = doc.GetElement(pickedRef.ElementId);
                        if (elem == null) continue;

                        // Determine prefix
                        string prefix;
                        if (namingMode == AssignElementNameView.NamingMode.TypeMark)
                        {
                            prefix = GetTypeMark(doc, elem);
                            if (string.IsNullOrWhiteSpace(prefix))
                            {
                                TaskDialog.Show("Warning",
                                    $"Element {elem.Id} has no Type Mark. Skipping.\n" +
                                    "Please set a Type Mark in the element's type properties first.");
                                continue;
                            }
                        }
                        else
                        {
                            prefix = customPrefix;
                        }

                        string name = $"{prefix}-{counter}";
                        // Write to the built-in Mark parameter
                        Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
                        if (param != null && !param.IsReadOnly)
                        {
                            param.Set(name);
                            updated++;
                            counter++;
                        }
                        else
                        {
                            TaskDialog.Show("Warning",
                                $"Element {elem.Id}: Mark parameter is read-only or not found.");
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        // User pressed ESC
                        break;
                    }
                }

                if (updated > 0)
                    t.Commit();
                else
                    t.RollBack();
            }

            if (updated > 0)
            {
                TaskDialog.Show("Assign Mark",
                    $"Manual numbering complete!\n\n  ✓ Updated: {updated} elements\n\nRevit will auto-propagate Mark → rebar Host Mark.");
            }

            return Result.Succeeded;
        }

        // --- HELPER METHODS ---

        private List<Element> CollectStructuralElements(Document doc, bool activeViewOnly)
        {
            var result = new List<Element>();

            foreach (var bic in TargetCategories)
            {
                FilteredElementCollector collector;
                if (activeViewOnly)
                    collector = new FilteredElementCollector(doc, doc.ActiveView.Id);
                else
                    collector = new FilteredElementCollector(doc);

                var elems = collector
                    .OfCategory(bic)
                    .WhereElementIsNotElementType()
                    .ToList();

                result.AddRange(elems);
            }

            return result;
        }

        private Dictionary<string, List<Element>> GroupByPrefix(Document doc, List<Element> elements,
            AssignElementNameView.NamingMode namingMode, string customPrefix)
        {
            var groups = new Dictionary<string, List<Element>>();

            foreach (var elem in elements)
            {
                string prefix;
                if (namingMode == AssignElementNameView.NamingMode.TypeMark)
                {
                    prefix = GetTypeMark(doc, elem);
                    if (string.IsNullOrWhiteSpace(prefix))
                        continue; // Skip elements without TypeMark
                }
                else
                {
                    prefix = customPrefix;
                }

                if (!groups.ContainsKey(prefix))
                    groups[prefix] = new List<Element>();

                groups[prefix].Add(elem);
            }

            return groups;
        }

        private string GetTypeMark(Document doc, Element elem)
        {
            // Try instance Type Mark first
            var typeMarkParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
            if (typeMarkParam != null && typeMarkParam.HasValue)
            {
                string val = typeMarkParam.AsString();
                if (!string.IsNullOrWhiteSpace(val)) return val;
            }

            // Try from the element's type
            ElementId typeId = elem.GetTypeId();
            if (typeId != ElementId.InvalidElementId)
            {
                Element type = doc.GetElement(typeId);
                if (type != null)
                {
                    var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                    if (p != null && p.HasValue)
                    {
                        string val = p.AsString();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }

                    // Fallback: use type name
                    return type.Name;
                }
            }

            return null;
        }

        private List<Element> SortLeftToRight(Document doc, List<Element> elements)
        {
            return elements.OrderBy(e =>
            {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.X;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).X;
                // Fallback: use bounding box center
                var bb = e.get_BoundingBox(null);
                if (bb != null) return (bb.Min.X + bb.Max.X) / 2.0;
                return 0.0;
            })
            .ThenBy(e =>
            {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.Y;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).Y;
                var bb = e.get_BoundingBox(null);
                if (bb != null) return (bb.Min.Y + bb.Max.Y) / 2.0;
                return 0.0;
            })
            .ToList();
        }
    }

    /// <summary>
    /// Selection filter that only allows structural elements.
    /// </summary>
    public class StructuralElementSelectionFilter : ISelectionFilter
    {
        private static readonly HashSet<BuiltInCategory> _allowed = new HashSet<BuiltInCategory>
        {
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_GenericModel
        };

        public bool AllowElement(Element elem)
        {
            if (elem?.Category == null) return false;
            return _allowed.Contains((BuiltInCategory)elem.Category.Id.Value);
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
