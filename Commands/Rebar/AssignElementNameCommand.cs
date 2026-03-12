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
    /// Tool 3: Assigns the shared parameter "Element Name" on structural elements using TypeMark-Number or CustomName-Number.
    /// Logic is identical to AssignMarkCommand but targets "Element Name" shared parameter.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AssignElementNameCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        private const string ParamName = "Element Name";

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
                TaskDialog.Show("Assign Element Name", "Please open a project first.");
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
            var namingMode = (AssignMarkView.NamingMode)dialog.SelectedNamingMode;
            var numberingRule = (AssignMarkView.NumberingRule)dialog.SelectedNumberingRule;
            var scope = (AssignMarkView.ScopeOption)dialog.SelectedScope;
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

        private Result RunAutoMode(Document doc,
            AssignMarkView.NamingMode namingMode,
            AssignMarkView.ScopeOption scope,
            string prefixSource,
            string customPrefix,
            BuiltInCategory[] categories)
        {
            var allElements = CollectStructuralElements(doc, scope == AssignMarkView.ScopeOption.ActiveView, categories);

            if (allElements.Count == 0)
            {
                TaskDialog.Show("Assign Element Name", "No structural elements found in the selected scope.");
                return Result.Succeeded;
            }

            var groups = GroupByPrefix(doc, allElements, namingMode, prefixSource, customPrefix);
            int totalUpdated = 0;

            using (Transaction t = new Transaction(doc, "Assign Element Name (Auto)"))
            {
                t.Start();

                foreach (var kvp in groups)
                {
                    string prefix = kvp.Key;
                    var elems = kvp.Value;

                    if (namingMode == AssignMarkView.NamingMode.TypeMarkXY)
                    {
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

                        var sortedX = SortLeftToRight(doc, xElems);
                        int xCounter = 1;
                        foreach (var elem in sortedX)
                        {
                            string name = $"{prefix}-X{xCounter}";
                            if (SetParam(elem, name)) totalUpdated++;
                            xCounter++;
                        }

                        var sortedY = SortBottomToTop(doc, yElems);
                        int yCounter = 1;
                        foreach (var elem in sortedY)
                        {
                            string name = $"{prefix}-Y{yCounter}";
                            if (SetParam(elem, name)) totalUpdated++;
                            yCounter++;
                        }

                        var sortedNoDir = SortLeftToRight(doc, noDir);
                        int nCounter = 1;
                        foreach (var elem in sortedNoDir)
                        {
                            string name = $"{prefix}-{nCounter}";
                            if (SetParam(elem, name)) totalUpdated++;
                            nCounter++;
                        }
                    }
                    else
                    {
                        var sorted = SortLeftToRight(doc, elems);
                        int counter = 1;
                        foreach (var elem in sorted)
                        {
                            string name = $"{prefix}-{counter}";
                            if (SetParam(elem, name)) totalUpdated++;
                            counter++;
                        }
                    }
                }

                t.Commit();
            }

            TaskDialog.Show("Assign Element Name",
                $"Auto-naming complete!\n\n" +
                $"  ✓ Updated: {totalUpdated} elements\n" +
                $"  📦 Groups: {groups.Count} unique prefixes");

            return Result.Succeeded;
        }

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
                "Click structural elements one by one to assign names.\n\n" +
                "  • Each click assigns the next number (1, 2, 3...)\n" +
                "  • Press ESC or right-click to finish.\n\n" +
                modeDesc);

            var xyCounters = new Dictionary<string, int>();
            int counter = 1;
            int updated = 0;
            var filter = new StructuralElementSelectionFilter(categories);

            using (Transaction t = new Transaction(doc, "Assign Element Name (Manual)"))
            {
                t.Start();

                while (true)
                {
                    try
                    {
                        Reference pickedRef = uiDoc.Selection.PickObject(ObjectType.Element, filter, $"Click element #{counter} (ESC to finish)");
                        if (pickedRef == null) break;

                        Element elem = doc.GetElement(pickedRef.ElementId);
                        if (elem == null) continue;

                        string prefix;
                        if (namingMode == AssignMarkView.NamingMode.TypeMark || namingMode == AssignMarkView.NamingMode.TypeMarkXY)
                        {
                            prefix = GetPrefix(doc, elem, prefixSource);
                            if (string.IsNullOrWhiteSpace(prefix)) continue;
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
                                string key = $"{prefix}-{dir}";
                                if (!xyCounters.ContainsKey(key)) xyCounters[key] = 0;
                                xyCounters[key]++;
                                name = $"{prefix}-{dir}{xyCounters[key]}";
                            }
                            else
                            {
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

                        if (SetParam(elem, name))
                        {
                            updated++;
                            counter++;
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException) { break; }
                }

                if (updated > 0) t.Commit();
                else t.RollBack();
            }

            return Result.Succeeded;
        }

        private bool SetParam(Element elem, string value)
        {
            Parameter param = elem.LookupParameter(ParamName);
            if (param != null && !param.IsReadOnly)
            {
                param.Set(value);
                return true;
            }
            return false;
        }

        private List<Element> CollectStructuralElements(Document doc, bool activeViewOnly, BuiltInCategory[] categories)
        {
            var result = new List<Element>();
            foreach (var bic in categories)
            {
                FilteredElementCollector collector = activeViewOnly ? new FilteredElementCollector(doc, doc.ActiveView.Id) : new FilteredElementCollector(doc);
                result.AddRange(collector.OfCategory(bic).WhereElementIsNotElementType().ToList());
            }
            return result;
        }

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
            return map.TryGetValue(tag, out var bic) ? new[] { bic } : TargetCategories;
        }

        private Dictionary<string, List<Element>> GroupByPrefix(Document doc, List<Element> elements, AssignMarkView.NamingMode namingMode, string prefixSource, string customPrefix)
        {
            var groups = new Dictionary<string, List<Element>>();
            foreach (var elem in elements)
            {
                string prefix = (namingMode == AssignMarkView.NamingMode.TypeMark || namingMode == AssignMarkView.NamingMode.TypeMarkXY) 
                    ? GetPrefix(doc, elem, prefixSource) 
                    : customPrefix;
                if (string.IsNullOrWhiteSpace(prefix)) continue;
                if (!groups.ContainsKey(prefix)) groups[prefix] = new List<Element>();
                groups[prefix].Add(elem);
            }
            return groups;
        }

        private string GetPrefix(Document doc, Element elem, string prefixSource)
        {
            if (prefixSource == "TypeMark")
            {
                var p = elem.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                if (p != null && p.HasValue && !string.IsNullOrWhiteSpace(p.AsString())) return p.AsString();
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element type = doc.GetElement(typeId);
                    if (type != null)
                    {
                        var tp = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_MARK);
                        if (tp != null && tp.HasValue && !string.IsNullOrWhiteSpace(tp.AsString())) return tp.AsString();
                        return type.Name;
                    }
                }
                return null;
            }
            if (prefixSource == "TypeComments")
            {
                ElementId typeId = elem.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element type = doc.GetElement(typeId);
                    if (type != null)
                    {
                        var p = type.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS);
                        if (p != null && p.HasValue && !string.IsNullOrWhiteSpace(p.AsString())) return p.AsString();
                        return type.Name;
                    }
                }
                return null;
            }
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
            return elements.OrderBy(e => {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.X;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).X;
                var bb = e.get_BoundingBox(null);
                return bb != null ? (bb.Min.X + bb.Max.X) / 2.0 : 0.0;
            }).ThenBy(e => {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.Y;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).Y;
                var bb = e.get_BoundingBox(null);
                return bb != null ? (bb.Min.Y + bb.Max.Y) / 2.0 : 0.0;
            }).ToList();
        }

        private List<Element> SortBottomToTop(Document doc, List<Element> elements)
        {
            return elements.OrderBy(e => {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.Y;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).Y;
                var bb = e.get_BoundingBox(null);
                return bb != null ? (bb.Min.Y + bb.Max.Y) / 2.0 : 0.0;
            }).ThenBy(e => {
                var loc = e.Location;
                if (loc is LocationPoint lp) return lp.Point.X;
                if (loc is LocationCurve lc) return lc.Curve.GetEndPoint(0).X;
                var bb = e.get_BoundingBox(null);
                return bb != null ? (bb.Min.X + bb.Max.X) / 2.0 : 0.0;
            }).ToList();
        }

        private string GetElementDirection(Element elem)
        {
            var loc = elem.Location;
            if (loc is LocationCurve lc)
            {
                XYZ start = lc.Curve.GetEndPoint(0);
                XYZ end = lc.Curve.GetEndPoint(1);
                return Math.Abs(end.X - start.X) >= Math.Abs(end.Y - start.Y) ? "X" : "Y";
            }
            return null;
        }
    }
}
