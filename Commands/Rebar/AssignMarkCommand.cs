using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.Rebar;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.Rebar
{
    /// <summary>
    /// Tool 2: Assigns the built-in Mark parameter on structural elements using TypeMark-Number or CustomName-Number.
    /// Revit auto-propagates host Mark → rebar Host Mark.
    /// Supports Auto (Left→Right) and Manual (click-to-number) modes.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignMarkCommand : BaseCommand
    {

        private static readonly BuiltInCategory[] TargetCategories = new[]
        {
            BuiltInCategory.OST_StructuralFoundation,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_StructuralColumns,
            BuiltInCategory.OST_StructuralFraming,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_Stairs,
            BuiltInCategory.OST_GenericModel,
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Windows,
            BuiltInCategory.OST_Rooms
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
            var dialog = new AssignMarkView();
            
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
            string prefixSource = dialog.SelectedPrefixSource;
            string categoryTag = dialog.SelectedCategoryTag;

            // Resolve category filter
            var categories = ResolveCategoryTag(categoryTag);

            // Manual mode: user clicks elements one by one
            if (numberingRule == AssignMarkView.NumberingRule.Manual)
            {
                return RunManualMode(uiDoc, doc, namingMode, prefixSource, customPrefix, categories);
            }

            // Auto mode: collect and sort left-to-right
            return RunAutoMode(doc, namingMode, scope, prefixSource, customPrefix, categories);
        }

        /// <summary>
        /// Auto mode: collects all structural elements, groups by TypeMark, sorts left→right, numbers sequentially.
        /// For TypeMarkXY mode, sub-groups by direction (X=horizontal, Y=vertical) within each TypeMark group.
        /// </summary>
        private Result RunAutoMode(Document doc,
            AssignMarkView.NamingMode namingMode,
            AssignMarkView.ScopeOption scope,
            string prefixSource,
            string customPrefix,
            BuiltInCategory[] categories)
        {
            // Collect elements
            var allElements = CollectStructuralElements(doc, scope == AssignMarkView.ScopeOption.ActiveView, categories);

            if (allElements.Count == 0)
            {
                TaskDialog.Show("Assign Mark", "No structural elements found in the selected scope.");
                return Result.Succeeded;
            }

            // Group by prefix (TypeMark or custom)
            var groups = GroupByPrefix(doc, allElements, namingMode, prefixSource, customPrefix);

            int totalUpdated = 0;

            using (Transaction t = new Transaction(doc, "Assign Mark (Auto)"))
            {
                t.Start();

                foreach (var kvp in groups)
                {
                    string prefix = kvp.Key;
                    var elems = kvp.Value;

                    if (namingMode == AssignMarkView.NamingMode.TypeMarkXY)
                    {
                        // Sub-group by direction: X (horizontal), Y (vertical), or null (no direction)
                        var xElems = new List<Element>();
                        var yElems = new List<Element>();
                        var noDir = new List<Element>();

                        foreach (var elem in elems)
                        {
                            string dir = GetElementDirection(elem);
                            if (dir == "X") xElems.Add(elem);
                            else if (dir == "Y") yElems.Add(elem);
                            else noDir.Add(elem);
                        }

                        // X-direction: sort left→right, name as Prefix-X1, Prefix-X2...
                        var sortedX = SortLeftToRight(doc, xElems);
                        int xCounter = 1;
                        foreach (var elem in sortedX)
                        {
                            string name = $"{prefix}-X{xCounter}";
                            Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                            if (param != null && !param.IsReadOnly)
                            {
                                param.Set(name);
                                totalUpdated++;
                            }
                            xCounter++;
                        }

                        // Y-direction: sort bottom→top, name as Prefix-Y1, Prefix-Y2...
                        var sortedY = SortBottomToTop(doc, yElems);
                        int yCounter = 1;
                        foreach (var elem in sortedY)
                        {
                            string name = $"{prefix}-Y{yCounter}";
                            Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                            if (param != null && !param.IsReadOnly)
                            {
                                param.Set(name);
                                totalUpdated++;
                            }
                            yCounter++;
                        }

                        // No direction (columns, etc): fall back to normal Prefix-1, Prefix-2...
                        var sortedNoDir = SortLeftToRight(doc, noDir);
                        int nCounter = 1;
                        foreach (var elem in sortedNoDir)
                        {
                            string name = $"{prefix}-{nCounter}";
                            Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                            if (param != null && !param.IsReadOnly)
                            {
                                param.Set(name);
                                totalUpdated++;
                            }
                            nCounter++;
                        }
                    }
                    else
                    {
                        // Standard mode: sort left → right, number sequentially
                        var sorted = SortLeftToRight(doc, elems);

                        int counter = 1;
                        foreach (var elem in sorted)
                        {
                            string name = $"{prefix}-{counter}";
                            Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
                            if (param != null && !param.IsReadOnly)
                            {
                                param.Set(name);
                                totalUpdated++;
                            }
                            counter++;
                        }
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Assign Mark",
                $"Auto-naming complete!\n\n" +
                $"  ✓ Updated: {totalUpdated} elements\n" +
                $"  📦 Groups: {groups.Count} unique prefixes");

            return Result.Succeeded;
        }

        /// <summary>
        /// Manual mode: user clicks elements one by one to assign sequential numbers.
        /// </summary>
        private Result RunManualMode(UIDocument uiDoc, Document doc,
            AssignMarkView.NamingMode namingMode,
            string prefixSource,
            string customPrefix,
            BuiltInCategory[] categories)
        {
            string modeDesc = namingMode == AssignMarkView.NamingMode.TypeMark
                    ? $"Prefix source: {prefixSource}"
                    : namingMode == AssignMarkView.NamingMode.TypeMarkXY
                        ? "Prefix: element's Type Mark + X/Y direction"
                        : $"Prefix: \"{customPrefix}\"";

            TaskDialog.Show("Manual Numbering",
                "Click structural elements one by one to assign numbers.\n\n" +
                "  • Each click assigns the next number (1, 2, 3...)\n" +
                "  • Press ESC or right-click to finish.\n\n" +
                modeDesc);

            // For TypeMarkXY manual mode, track counters per direction per prefix
            var xyCounters = new Dictionary<string, int>(); // key = "Prefix-X" or "Prefix-Y" or "Prefix"
            int counter = 1;
            int updated = 0;

            // Build a multi-category filter for selection
            var filter = new StructuralElementSelectionFilter(categories);

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
                        if (namingMode == AssignMarkView.NamingMode.TypeMark ||
                            namingMode == AssignMarkView.NamingMode.TypeMarkXY)
                        {
                            prefix = GetPrefix(doc, elem, prefixSource);
                            if (string.IsNullOrWhiteSpace(prefix))
                            {
                                TaskDialog.Show("Warning",
                                    $"Element {elem.Id} has no valid prefix for source '{prefixSource}'. Skipping.");
                                continue;
                            }
                        }
                        else
                        {
                            prefix = customPrefix;
                        }

                        string name;
                        if (namingMode == AssignMarkView.NamingMode.TypeMarkXY)
                        {
                            string dir = GetElementDirection(elem);
                            if (dir != null)
                            {
                                // Has direction: Prefix-X1 or Prefix-Y1
                                string key = $"{prefix}-{dir}";
                                if (!xyCounters.ContainsKey(key)) xyCounters[key] = 0;
                                xyCounters[key]++;
                                name = $"{prefix}-{dir}{xyCounters[key]}";
                            }
                            else
                            {
                                // No direction: fall back to Prefix-1
                                string key = prefix;
                                if (!xyCounters.ContainsKey(key)) xyCounters[key] = 0;
                                xyCounters[key]++;
                                name = $"{prefix}-{xyCounters[key]}";
                            }
                        }
                        else
                        {
                            name = $"{prefix}-{counter}";
                        }
                        // Write to the built-in Mark (or Room Number) parameter
                        Parameter param = elem.get_Parameter(BuiltInParameter.ALL_MODEL_MARK) ?? elem.get_Parameter(BuiltInParameter.ROOM_NUMBER);
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
                    $"Manual numbering complete!\n\n  ✓ Updated: {updated} elements");
            }

            return Result.Succeeded;
        }

        // --- HELPER METHODS ---

        private List<Element> CollectStructuralElements(Document doc, bool activeViewOnly, BuiltInCategory[] categories)
        {
            var result = new List<Element>();

            foreach (var bic in categories)
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

        /// <summary>
        /// Resolves a category tag string to an array of BuiltInCategory values.
        /// Returns all target categories if tag is null ("All Categories").
        /// </summary>
        private BuiltInCategory[] ResolveCategoryTag(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return TargetCategories;

            var map = new Dictionary<string, BuiltInCategory>
            {
                { "OST_StructuralFoundation", BuiltInCategory.OST_StructuralFoundation },
                { "OST_Walls", BuiltInCategory.OST_Walls },
                { "OST_Floors", BuiltInCategory.OST_Floors },
                { "OST_StructuralColumns", BuiltInCategory.OST_StructuralColumns },
                { "OST_StructuralFraming", BuiltInCategory.OST_StructuralFraming },
                { "OST_Roofs", BuiltInCategory.OST_Roofs },
                { "OST_Stairs", BuiltInCategory.OST_Stairs },
                { "OST_GenericModel", BuiltInCategory.OST_GenericModel },
                { "OST_Doors", BuiltInCategory.OST_Doors },
                { "OST_Windows", BuiltInCategory.OST_Windows },
                { "OST_Rooms", BuiltInCategory.OST_Rooms }
            };

            if (map.TryGetValue(tag, out var bic))
                return new[] { bic };

            return TargetCategories;
        }

        private Dictionary<string, List<Element>> GroupByPrefix(Document doc, List<Element> elements,
            AssignMarkView.NamingMode namingMode, string prefixSource, string customPrefix)
        {
            var groups = new Dictionary<string, List<Element>>();

            foreach (var elem in elements)
            {
                string prefix;
                if (namingMode == AssignMarkView.NamingMode.TypeMark ||
                    namingMode == AssignMarkView.NamingMode.TypeMarkXY)
                {
                    prefix = GetPrefix(doc, elem, prefixSource);
                    if (string.IsNullOrWhiteSpace(prefix))
                        continue; // Skip elements without valid prefix
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

        private string GetPrefix(Document doc, Element elem, string prefixSource)
        {
            // For TypeMark, try instance first, else type
            if (prefixSource == "TypeMark")
            {
                var typeMarkParam = elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                if (typeMarkParam != null && typeMarkParam.HasValue && !string.IsNullOrWhiteSpace(typeMarkParam.AsString()))
                    return typeMarkParam.AsString();

                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element type = doc.GetElement(typeId);
                    if (type != null)
                    {
                        var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                        if (p != null && p.HasValue && !string.IsNullOrWhiteSpace(p.AsString()))
                            return p.AsString();
                        return type.Name; // Fallback
                    }
                }
                return null;
            }

            // For TypeComments
            if (prefixSource == "TypeComments")
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element type = doc.GetElement(typeId);
                    if (type != null)
                    {
                        var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                        if (p != null && p.HasValue && !string.IsNullOrWhiteSpace(p.AsString()))
                            return p.AsString();
                        return type.Name; // Fallback
                    }
                }
                return null;
            }

            // For TypeName
            if (prefixSource == "TypeName")
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element type = doc.GetElement(typeId);
                    if (type != null) return type.Name;
                }
                return null;
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

        /// <summary>
        /// Sort elements bottom → top (by Y coordinate), then left → right (by X).
        /// Used for Y-direction elements in TypeMarkXY mode.
        /// </summary>
        private List<Element> SortBottomToTop(Document doc, List<Element> elements)
        {
            return elements.OrderBy(e =>
            {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.Y;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).Y;
                var bb = e.get_BoundingBox(null);
                if (bb != null) return (bb.Min.Y + bb.Max.Y) / 2.0;
                return 0.0;
            })
            .ThenBy(e =>
            {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.X;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).X;
                var bb = e.get_BoundingBox(null);
                if (bb != null) return (bb.Min.X + bb.Max.X) / 2.0;
                return 0.0;
            })
            .ToList();
        }

        /// <summary>
        /// Determines the plan-view direction of an element.
        /// Returns "X" for horizontal, "Y" for vertical, or null for point-based elements.
        /// Uses the angle of the element's curve relative to the X-axis (within ±45°).
        /// </summary>
        private string GetElementDirection(Element elem)
        {
            var loc = elem.Location;
            if (loc is LocationCurve lc)
            {
                XYZ start = lc.Curve.GetEndPoint(0);
                XYZ end = lc.Curve.GetEndPoint(1);
                double dx = Math.Abs(end.X - start.X);
                double dy = Math.Abs(end.Y - start.Y);

                // If mostly horizontal (dx >= dy) → X, else Y
                return dx >= dy ? "X" : "Y";
            }

            // LocationPoint or other → no direction
            return null;
        }
    }

    /// <summary>
    /// Selection filter that only allows structural elements.
    /// </summary>
    public class StructuralElementSelectionFilter : ISelectionFilter
    {
        private readonly HashSet<BuiltInCategory> _allowed;

        public StructuralElementSelectionFilter(BuiltInCategory[] categories)
        {
            _allowed = new HashSet<BuiltInCategory>(categories);
        }

        public bool AllowElement(Element elem)
        {
            if (elem?.Category == null) return false;
            return _allowed.Contains((BuiltInCategory)elem.Category.Id.GetIdValue());
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
