using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.General;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.General
{
    public class JoinAdvanceHandler : IExternalEventHandler
    {
        private JoinAdvanceView _view;

        public JoinAdvanceHandler(JoinAdvanceView view)
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

                if (leftCats == null || rightCats == null || (leftCats.Count == 0 && rightCats.Count == 0))
                {
                    _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = "⚠ No categories selected.");
                    return;
                }

                // 1. Collect elements in active view exactly as Python logic
                View activeView = doc.ActiveView;
                
                var leftElements = new FilteredElementCollector(doc, activeView.Id)
                    .WherePasses(new ElementMulticategoryFilter(leftCats))
                    .WhereElementIsNotElementType()
                    .ToElements();

                var rightElements = new FilteredElementCollector(doc, activeView.Id)
                    .WherePasses(new ElementMulticategoryFilter(rightCats))
                    .WhereElementIsNotElementType()
                    .ToElements();

                int leftCount = leftElements.Count;
                int rightCount = rightElements.Count;

                if (leftCount == 0 || rightCount == 0)
                {
                    _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"⚠ No elements found in active view (Priority: {leftCount}, Secondary: {rightCount}).");
                    return;
                }

                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"⏳ Processing (Priority: {leftCount}, Secondary: {rightCount})...");

                int processedCount = 0;
                Options geoOptions = new Options { DetailLevel = ViewDetailLevel.Fine };
                string opName = isJoin ? "Join Advance" : "Unjoin Advance";
                System.Text.StringBuilder log = new System.Text.StringBuilder();

                using (Transaction t = new Transaction(doc, opName))
                {
                    t.Start();

                    // Suppress warnings to match pyRevit's try-except behavior
                    FailureHandlingOptions failOpt = t.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new WarningSwallower());
                    t.SetFailureHandlingOptions(failOpt);

                    log.AppendLine($"--- { (isJoin ? "JOIN" : "UNJOIN") } ADVANCE SESSION LOG ---");
                    log.AppendLine($"Time: {DateTime.Now}");
                    log.AppendLine($"View: {activeView.Name}");
                    log.AppendLine("------------------------------------------");

                    if (isJoin)
                    {
                        // --- JOIN OPERATION MATCHING PYTHON PRIORITY LOGIC ---
                        foreach (var left in leftElements)
                        {
                            var solidsLeft = GetSolids(left, geoOptions);
                            if (solidsLeft.Count == 0) continue;

                            foreach (var right in rightElements)
                            {
                                if (left.Id == right.Id) continue;

                                try
                                {
                                    bool alreadyJoined = JoinGeometryUtils.AreElementsJoined(doc, left, right);
                                    
                                    if (!alreadyJoined)
                                    {
                                        // Intersection check as per python logic
                                        var solidsRight = GetSolids(right, geoOptions);
                                        bool intersectFound = false;
                                        foreach (var sLeft in solidsLeft)
                                        {
                                            foreach (var sRight in solidsRight)
                                            {
                                                try {
                                                    var inter = BooleanOperationsUtils.ExecuteBooleanOperation(sLeft, sRight, BooleanOperationsType.Intersect);
                                                    if (inter != null && inter.Volume > 0.000001)
                                                    {
                                                        JoinGeometryUtils.JoinGeometry(doc, left, right);
                                                        processedCount++;
                                                        
                                                        // Ensure Left (Priority) cuts Right
                                                        if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, left, right))
                                                        {
                                                            JoinGeometryUtils.SwitchJoinOrder(doc, left, right);
                                                        }
                                                        
                                                        log.AppendLine($"[JOINED] {left.Category.Name} [{left.Id}] to {right.Category.Name} [{right.Id}]");
                                                        intersectFound = true;
                                                        break;
                                                    }
                                                } catch { }
                                            }
                                            if (intersectFound) break;
                                        }
                                    }
                                    else
                                    {
                                        // Already joined, ensure correct order
                                        if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, left, right))
                                        {
                                            JoinGeometryUtils.SwitchJoinOrder(doc, left, right);
                                            processedCount++;
                                            log.AppendLine($"[SWITCHED] {left.Category.Name} [{left.Id}] / {right.Category.Name} [{right.Id}] - Priority Enforced.");
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    else
                    {
                        // --- UNJOIN OPERATION ---
                        foreach (var left in leftElements)
                        {
                            try
                            {
                                var joinedIds = JoinGeometryUtils.GetJoinedElements(doc, left);
                                foreach (var joinedId in joinedIds)
                                {
                                    if (rightCats.Contains(doc.GetElement(joinedId).Category.Id))
                                    {
                                        JoinGeometryUtils.UnjoinGeometry(doc, left, doc.GetElement(joinedId));
                                        processedCount++;
                                        log.AppendLine($"[UNJOINED] {left.Category.Name} [{left.Id}] / {doc.GetElement(joinedId).Category.Name} [{joinedId}]");
                                    }
                                }
                            }
                            catch { }
                        }
                    }

                    log.AppendLine("------------------------------------------");
                    log.AppendLine($"Total modifications: {processedCount}");

                    t.Commit();
                }

                _view.Dispatcher.Invoke(() => 
                {
                    _view.UI_Status_Text.Text = $"✅ {opName} completed. {processedCount} items modified.";
                    _view.SessionLog = log.ToString();
                });
            }
            catch (Exception ex)
            {
                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"❌ Error: {ex.Message}");
            }
        }

        private List<Solid> GetSolids(Element element, Options options)
        {
            var solids = new List<Solid>();
            var geom = element.get_Geometry(options);
            if (geom != null)
            {
                foreach (var obj in geom)
                {
                    if (obj is Solid s && s.Volume > 0) solids.Add(s);
                    else if (obj is GeometryInstance inst)
                    {
                        foreach (var instObj in inst.GetInstanceGeometry())
                        {
                            if (instObj is Solid instS && instS.Volume > 0) solids.Add(instS);
                        }
                    }
                }
            }
            return solids;
        }

        public string GetName() => "Join Advance Handler";
    }
}
