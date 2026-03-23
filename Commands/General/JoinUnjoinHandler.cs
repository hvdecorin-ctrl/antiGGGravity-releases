using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.General;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.General
{
    public class JoinUnjoinHandler : IExternalEventHandler
    {
        private JoinUnjoinView _view;

        public JoinUnjoinHandler(JoinUnjoinView view)
        {
            _view = view;
        }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = "Processing...");

                bool isJoin = false;
                List<ElementId> leftCats = null;
                List<ElementId> rightCats = null;

                // Read UI values on the UI thread
                _view.Dispatcher.Invoke(() =>
                {
                    isJoin = _view.IsJoinOperation;
                    leftCats = _view.AllLeftItems.Where(c => c.IsChecked).Select(c => c.Category.Id).ToList();
                    rightCats = _view.AllRightItems.Where(c => c.IsChecked).Select(c => c.Category.Id).ToList();
                });

                // Collect elements in active view matching selected categories
                var collector = new FilteredElementCollector(doc, doc.ActiveView.Id).WhereElementIsNotElementType();
                var allElements = collector.ToElements();

                var leftElements = allElements.Where(e => e.Category != null && leftCats.Contains(e.Category.Id)).ToList();
                var rightElements = allElements.Where(e => e.Category != null && rightCats.Contains(e.Category.Id)).ToList();

                if (!leftElements.Any() || !rightElements.Any())
                {
                    _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = "⚠ No elements found for selected categories.");
                    return;
                }

                // Build a set of right element IDs for quick lookup
                var rightElementIds = new HashSet<ElementId>(rightElements.Select(e => e.Id));

                int processedCount = 0;
                Options geoOptions = new Options { DetailLevel = ViewDetailLevel.Fine };

                using (Transaction t = new Transaction(doc, isJoin ? "Join Advance" : "Unjoin Advance"))
                {
                    t.Start();

                    // Suppress warnings
                    FailureHandlingOptions failOpt = t.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new WarningSwallower());
                    t.SetFailureHandlingOptions(failOpt);

                    if (isJoin)
                    {
                        // --- JOIN OPERATION ---
                        foreach (var left in leftElements)
                        {
                            var solidsLeft = GetSolids(left, geoOptions);
                            if (!solidsLeft.Any()) continue;

                            foreach (var right in rightElements)
                            {
                                if (left.Id == right.Id) continue;

                                try
                                {
                                    bool alreadyJoined = JoinGeometryUtils.AreElementsJoined(doc, left, right);
                                    bool needsPriorityFix = false;

                                    if (!alreadyJoined)
                                    {
                                        // Intersection check using solids (matching original Python)
                                        var solidsRight = GetSolids(right, geoOptions);
                                        bool intersectFound = false;

                                        foreach (Solid sL in solidsLeft)
                                        {
                                            foreach (Solid sR in solidsRight)
                                            {
                                                try
                                                {
                                                    using (Solid inter = BooleanOperationsUtils.ExecuteBooleanOperation(sL, sR, BooleanOperationsType.Intersect))
                                                    {
                                                        if (inter != null && inter.Volume > 0.000001)
                                                        {
                                                            JoinGeometryUtils.JoinGeometry(doc, left, right);
                                                            intersectFound = true;
                                                            needsPriorityFix = true;
                                                            break;
                                                        }
                                                    }
                                                }
                                                catch { continue; }
                                            }
                                            if (intersectFound) break;
                                        }
                                    }
                                    else
                                    {
                                        // Already joined, but might need priority fix
                                        needsPriorityFix = true;
                                    }

                                    // Enforce priority: left (Priority) ALWAYS cuts right (Secondary)
                                    if (needsPriorityFix && !JoinGeometryUtils.IsCuttingElementInJoin(doc, left, right))
                                    {
                                        JoinGeometryUtils.SwitchJoinOrder(doc, left, right);
                                        processedCount++;
                                    }
                                    else if (needsPriorityFix)
                                    {
                                        // Successfully joined or already joined with correct order
                                        processedCount++;
                                    }
                                }
                                catch { continue; }
                            }
                        }
                    }
                    else
                    {
                        // --- UNJOIN OPERATION ---
                        foreach (var left in leftElements)
                        {
                            var joinedIds = JoinGeometryUtils.GetJoinedElements(doc, left);
                            foreach (var joinedId in joinedIds)
                            {
                                if (rightElementIds.Contains(joinedId))
                                {
                                    var right = doc.GetElement(joinedId);
                                    if (right == null) continue;

                                    if (JoinGeometryUtils.AreElementsJoined(doc, left, right))
                                    {
                                        JoinGeometryUtils.UnjoinGeometry(doc, left, right);
                                        processedCount++;
                                    }
                                }
                            }
                        }
                    }

                    t.Commit();
                }

                string opName = isJoin ? "Join Advance" : "Unjoin Advance";
                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"✅ {opName} completed. {processedCount} items modified.");
            }
            catch (Exception ex)
            {
                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"❌ Error: {ex.Message}");
            }
        }

        private List<Solid> GetSolids(Element element, Options options)
        {
            var solids = new List<Solid>();
            try
            {
                GeometryElement geoElem = element.get_Geometry(options);
                if (geoElem == null) return solids;

                foreach (GeometryObject geoObj in geoElem)
                {
                    if (geoObj is Solid solid && solid.Volume > 0)
                    {
                        solids.Add(solid);
                    }
                    else if (geoObj is GeometryInstance instance)
                    {
                        foreach (GeometryObject instObj in instance.GetInstanceGeometry())
                        {
                            if (instObj is Solid instSolid && instSolid.Volume > 0)
                            {
                                solids.Add(instSolid);
                            }
                        }
                    }
                }
            }
            catch { }
            return solids;
        }

        public string GetName()
        {
            return "Join/Unjoin Handler";
        }
    }
}
