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

                // Collect elements project-wide
                var globalCollector = new FilteredElementCollector(doc).WhereElementIsNotElementType();
                var allProjectElements = globalCollector.ToElements();

                // Audit for potential Column category confusion
                ElementId archColId = new ElementId(BuiltInCategory.OST_Columns);
                ElementId struColId = new ElementId(BuiltInCategory.OST_StructuralColumns);
                int archColCount = allProjectElements.Count(e => e.Category != null && e.Category.Id == archColId);
                int struColCount = allProjectElements.Count(e => e.Category != null && e.Category.Id == struColId);

                // Find elements matching selected categories global project-wide
                var leftElementsGlobal = allProjectElements.Where(e => e.Category != null && leftCats.Contains(e.Category.Id)).ToList();
                var rightElementsGlobal = allProjectElements.Where(e => e.Category != null && rightCats.Contains(e.Category.Id)).ToList();

                // Filter by proximity to active view manually
                List<Element> leftElements = new List<Element>();
                List<Element> rightElements = new List<Element>();
                
                string viewFilterInfo = "Filter: None";
                try {
                    View view = doc.ActiveView;
                    BoundingBoxXYZ cropBox = view.CropBox;
                    Outline viewOutline = new Outline(cropBox.Min, cropBox.Max);
                    var viewFilter = new BoundingBoxIntersectsFilter(viewOutline);
                    
                    leftElements = leftElementsGlobal.Where(e => viewFilter.PassesFilter(e)).ToList();
                    rightElements = rightElementsGlobal.Where(e => viewFilter.PassesFilter(e)).ToList();
                    viewFilterInfo = $"View Filter: Found {leftElements.Count}/{leftElementsGlobal.Count} Priority, {rightElements.Count}/{rightElementsGlobal.Count} Secondary elements in view area.";
                } catch {
                    leftElements = leftElementsGlobal;
                    rightElements = rightElementsGlobal;
                    viewFilterInfo = "View Filter: Failed (Using Global Collection)";
                }

                int leftCount = leftElements.Count;
                int rightCount = rightElements.Count;

                if (leftCount == 0 || rightCount == 0)
                {
                    _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"⚠ No elements found (Found {leftCount} Priority, {rightCount} Secondary). Audit: {struColCount} Structural, {archColCount} Architectural columns found in project.");
                    return;
                }

                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"⏳ Processing (Priority: {leftCount}, Secondary: {rightCount})...");

                // Build a set of right element IDs for quick lookup
                var rightElementIds = new HashSet<ElementId>(rightElements.Select(e => e.Id));

                int processedCount = 0;
                Options geoOptions = new Options { DetailLevel = ViewDetailLevel.Fine, IncludeNonVisibleObjects = true };
                string opName = isJoin ? "Join Advance" : "Unjoin Advance";
                System.Text.StringBuilder sb = new System.Text.StringBuilder();

                using (Transaction t = new Transaction(doc, opName))
                {
                    t.Start();

                    // Suppress warnings
                    FailureHandlingOptions failOpt = t.GetFailureHandlingOptions();
                    failOpt.SetFailuresPreprocessor(new WarningSwallower());
                    t.SetFailureHandlingOptions(failOpt);

                    sb.AppendLine($"--- { (isJoin ? "JOIN" : "UNJOIN") } ADVANCED SESSION LOG ---");
                    sb.AppendLine($"Time: {DateTime.Now}");
                    sb.AppendLine(viewFilterInfo);
                    sb.AppendLine($"Audit: Project has {struColCount} Structural Columns, {archColCount} Architectural Columns.");
                    sb.AppendLine("------------------------------------------");

                    if (isJoin)
                    {
                        // --- JOIN OPERATION ---
                        foreach (var left in leftElements)
                        {
                            string leftName = GetElementName(left);
                            string leftPhase = GetPhaseName(left);
                            string leftElev = GetElevInfo(left);
                            string leftDO = left.DesignOption?.Name ?? "Main Model";
                            
                            // Get Column Solid for deep intersection detection
                            Solid leftSolid = GetUnionSolid(left, geoOptions);
                            ElementIntersectsSolidFilter solidFilter = null;
                            if (leftSolid != null && leftSolid.Volume > 0)
                            {
                                solidFilter = new ElementIntersectsSolidFilter(leftSolid);
                            }
                            
                            // Bounding Box Pre-filter (expanded slightly for tolerance)
                            BoundingBoxIntersectsFilter bbFilter = null;
                            if (GetGlobalBoundingBox(left, out XYZ minPt, out XYZ maxPt))
                            {
                                // Expand by 0.1ft (30mm) for detection tolerance
                                Outline outline = new Outline(minPt - new XYZ(0.1, 0.1, 0.1), maxPt + new XYZ(0.1, 0.1, 0.1));
                                bbFilter = new BoundingBoxIntersectsFilter(outline);
                            }

                            foreach (var right in rightElements)
                            {
                                if (left.Id == right.Id) continue;
                                string rightName = GetElementName(right);
                                string rightPhase = GetPhaseName(right);
                                string rightDO = right.DesignOption?.Name ?? "Main Model";

                                try
                                {
                                    bool areJoined = JoinGeometryUtils.AreElementsJoined(doc, left, right);
                                    
                                    if (!areJoined)
                                    {
                                        // Detection fallback: check if they actually intersect in the model
                                        bool doIntersect = false;
                                        bool useFallback = false;
                                        string skipReason = "";

                                        if (leftPhase != rightPhase && leftPhase != "None" && rightPhase != "None")
                                        {
                                            skipReason = $"Phase mismatch ({leftPhase} vs {rightPhase})";
                                        }
                                        else if (leftDO != rightDO)
                                        {
                                            skipReason = $"Design Option mismatch ({leftDO} vs {rightDO})";
                                        }
                                        else if (bbFilter != null && !bbFilter.PassesFilter(right))
                                        {
                                            skipReason = $"Bounding boxes do not touch. Left Z: {leftElev}, Right Z: {GetElevInfo(right)}";
                                        }
                                        else if (solidFilter != null && !solidFilter.PassesFilter(right))
                                        {
                                            // Fallback: If Bounding Boxes overlap but Solids don't, try to join anyway 
                                            // because nested geometry might be misleading the solid filter.
                                            useFallback = true;
                                            doIntersect = true;
                                        }
                                        else if (solidFilter == null && bbFilter != null && bbFilter.PassesFilter(right))
                                        {
                                            // Fallback: No solids found at all but BBs overlap
                                            useFallback = true;
                                            doIntersect = true;
                                        }
                                        else if (solidFilter == null && bbFilter == null)
                                        {
                                            skipReason = "No geometry found (BB or Solid).";
                                        }
                                        else
                                        {
                                            doIntersect = true;
                                        }

                                        if (doIntersect)
                                        {
                                            try {
                                                JoinGeometryUtils.JoinGeometry(doc, left, right);
                                                processedCount++;

                                                string prefix = useFallback ? "[JOINED (Fallback)]" : "[JOINED]";

                                                // Ensure correct join order after new join
                                                if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, left, right))
                                                {
                                                    JoinGeometryUtils.SwitchJoinOrder(doc, left, right);
                                                    sb.AppendLine($"{prefix} {leftName} joined to {rightName} (Order enforced).");
                                                }
                                                else
                                                {
                                                    sb.AppendLine($"{prefix} {leftName} joined to {rightName}.");
                                                }
                                            } catch (Exception) {
                                                if (!useFallback) sb.AppendLine($"[SKIP] {leftName} / {rightName}: Elements cannot be joined (Revit restriction).");
                                                // If fallback failed, just ignore it silently as it was a "best effort" guess.
                                            }
                                        }
                                        else
                                        {
                                            sb.AppendLine($"[SKIP] {leftName} / {rightName}: {skipReason}");
                                        }
                                    }
                                    else
                                    {
                                        // Elements are already joined. Ensure the 'Priority' element (left) is the cutting one.
                                        if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, left, right))
                                        {
                                            JoinGeometryUtils.SwitchJoinOrder(doc, left, right);
                                            processedCount++;
                                            sb.AppendLine($"[SWITCHED] {leftName} already joined to {rightName}. Order switched.");
                                        }
                                        else
                                        {
                                            sb.AppendLine($"[OK] {leftName} already joined to {rightName} (Correct order).");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                { 
                                    sb.AppendLine($"[ERROR] Unexpected error for {leftName} / {rightName}: {ex.Message}");
                                    continue; 
                                }
                            }
                        }
                    }
                    else
                    {
                        // --- UNJOIN OPERATION ---
                        // Much more robust: Ask Revit what it's JOINED to directly.
                        HashSet<string> unjoinedPairs = new HashSet<string>();
                        foreach (var left in leftElementsGlobal)
                        {
                            string leftName = GetElementName(left);
                            try
                            {
                                var joinedIds = JoinGeometryUtils.GetJoinedElements(doc, left);
                                foreach (var joinedId in joinedIds)
                                {
                                    Element joinedElem = doc.GetElement(joinedId);
                                    if (joinedElem != null && joinedElem.Category != null && rightCats.Contains(joinedElem.Category.Id))
                                    {
                                        // Sort IDs to prevent double-counting A and B
                                        string pairKey = left.Id.ToString() + "-" + joinedElem.Id.ToString();
                                        string reverseKey = joinedElem.Id.ToString() + "-" + left.Id.ToString();
                                        
                                        if (!unjoinedPairs.Contains(pairKey) && !unjoinedPairs.Contains(reverseKey))
                                        {
                                            JoinGeometryUtils.UnjoinGeometry(doc, left, joinedElem);
                                            unjoinedPairs.Add(pairKey);
                                            processedCount++;
                                            sb.AppendLine($"[UNJOINED] {leftName} / {GetElementName(joinedElem)}");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                sb.AppendLine($"[ERROR] Unjoin failed for {leftName}: {ex.Message}");
                            }
                        }
                    }

                    sb.AppendLine("------------------------------------------");
                    sb.AppendLine($"Total modifications: {processedCount}");

                    t.Commit();
                }

                string logResults = sb.ToString();
                _view.Dispatcher.Invoke(() => 
                {
                    _view.UI_Status_Text.Text = $"✅ {opName} completed. {processedCount} items modified.";
                    _view.SessionLog = logResults;
                });
            }
            catch (Exception ex)
            {
                _view.Dispatcher.Invoke(() => _view.UI_Status_Text.Text = $"❌ Error: {ex.Message}");
            }
        }

        private string GetElementName(Element e)
        {
            if (e == null) return "Unknown";
            string name = e.Name;
            if (string.IsNullOrEmpty(name)) name = e.Category?.Name ?? "Unnamed";
            return $"{name} [ID: {e.Id}]";
        }

        private bool GetGlobalBoundingBox(Element e, out XYZ min, out XYZ max)
        {
            min = XYZ.Zero;
            max = XYZ.Zero;
            try {
                var bb = e.get_BoundingBox(null);
                if (bb != null) {
                    Transform t = bb.Transform;
                    var corners = new[] {
                        t.OfPoint(new XYZ(bb.Min.X, bb.Min.Y, bb.Min.Z)),
                        t.OfPoint(new XYZ(bb.Min.X, bb.Min.Y, bb.Max.Z)),
                        t.OfPoint(new XYZ(bb.Min.X, bb.Max.Y, bb.Min.Z)),
                        t.OfPoint(new XYZ(bb.Min.X, bb.Max.Y, bb.Max.Z)),
                        t.OfPoint(new XYZ(bb.Max.X, bb.Min.Y, bb.Min.Z)),
                        t.OfPoint(new XYZ(bb.Max.X, bb.Min.Y, bb.Max.Z)),
                        t.OfPoint(new XYZ(bb.Max.X, bb.Max.Y, bb.Min.Z)),
                        t.OfPoint(new XYZ(bb.Max.X, bb.Max.Y, bb.Max.Z))
                    };
                    min = new XYZ(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Min(p => p.Z));
                    max = new XYZ(corners.Max(p => p.X), corners.Max(p => p.Y), corners.Max(p => p.Z));
                    return true;
                }
            } catch {}
            return false;
        }

        private string GetElevInfo(Element e)
        {
            if (GetGlobalBoundingBox(e, out XYZ minPt, out XYZ maxPt))
            {
                double min = Math.Round(minPt.Z * 304.8, 0); // Convert to mm
                double max = Math.Round(maxPt.Z * 304.8, 0);
                return $"{min}-{max}mm";
            }
            return "N/A";
        }

        private string GetPhaseName(Element e)
        {
            try {
                var phaseId = e.get_Parameter(BuiltInParameter.PHASE_CREATED)?.AsElementId();
                if (phaseId != null && phaseId != ElementId.InvalidElementId) {
                    return e.Document.GetElement(phaseId)?.Name ?? "None";
                }
            } catch {}
            return "None";
        }

        private Solid GetUnionSolid(Element element, Options options)
        {
            var solids = new List<Solid>();
            GetSolidsRecursive(element.get_Geometry(options), solids);
            
            if (solids.Count == 0) return null;
            if (solids.Count == 1) return solids[0];

            Solid union = solids[0];
            for (int i = 1; i < solids.Count; i++) {
                try {
                    union = BooleanOperationsUtils.ExecuteBooleanOperation(union, solids[i], BooleanOperationsType.Union);
                } catch { }
            }
            return union;
        }

        private void GetSolidsRecursive(GeometryElement geom, List<Solid> solids)
        {
            if (geom == null) return;
            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid s && s.Volume > 0)
                    solids.Add(s);
                else if (obj is GeometryInstance inst)
                {
                    GetSolidsRecursive(inst.GetInstanceGeometry(), solids);
                    // Also check Symbol geometry just in case
                    GetSolidsRecursive(inst.GetSymbolGeometry(), solids);
                }
            }
        }
 
        public string GetName()
        {
            return "Join/Unjoin Handler";
        }
    }
}
