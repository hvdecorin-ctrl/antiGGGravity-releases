using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
                        // --- JOIN OPERATION WITH AUTOMATIC SWITCHING ---
                        foreach (var left in leftElements)
                        {
                            var leftStructuralMaterial = GetStructuralMaterial(left);
                            bool leftIsConcrete = IsConcrete(leftStructuralMaterial);
                            bool leftIsSteel = IsSteel(leftStructuralMaterial);

                            // 1. Correct Existing Joins (Switch Order)
                            try 
                            {
                                var joinedIds = JoinGeometryUtils.GetJoinedElements(doc, left);
                                foreach (var joinedId in joinedIds)
                                {
                                    Element joinedRight = doc.GetElement(joinedId);
                                    if (rightCats.Contains(joinedRight.Category.Id))
                                    {
                                        // Priority Check: Should 'left' cut 'joinedRight'?
                                        if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, left, joinedRight))
                                        {
                                            JoinGeometryUtils.SwitchJoinOrder(doc, left, joinedRight);
                                            processedCount++;
                                            log.AppendLine($"[SWITCHED] {left.Category.Name} [{left.Id}] / {joinedRight.Category.Name} [{joinedRight.Id}] - Priority Enforced.");
                                        }
                                    }
                                }
                            }
                            catch { }

                            // 2. Perform New Joins (Intersection Filter)
                            var intersectFilter = new ElementIntersectsElementFilter(left);
                            var potentialIntersects = new FilteredElementCollector(doc, rightElements.Select(e => e.Id).ToList())
                                .WherePasses(intersectFilter)
                                .ToElements();

                            foreach (var right in potentialIntersects)
                            {
                                if (left.Id == right.Id) continue;

                                try
                                {
                                    // Material Exclusion: Skip Steel-Concrete joints
                                    var rightStructuralMaterial = GetStructuralMaterial(right);
                                    if ((leftIsSteel && IsConcrete(rightStructuralMaterial)) || 
                                        (leftIsConcrete && IsSteel(rightStructuralMaterial)))
                                    {
                                        continue; 
                                    }

                                    bool alreadyJoined = JoinGeometryUtils.AreElementsJoined(doc, left, right);
                                    
                                    if (!alreadyJoined)
                                    {
                                        JoinGeometryUtils.JoinGeometry(doc, left, right);
                                        processedCount++;
                                        
                                        // Ensure Left (Priority) cuts Right
                                        if (!JoinGeometryUtils.IsCuttingElementInJoin(doc, left, right))
                                        {
                                            JoinGeometryUtils.SwitchJoinOrder(doc, left, right);
                                        }
                                        
                                        log.AppendLine($"[JOINED] {left.Category.Name} [{left.Id}] to {right.Category.Name} [{right.Id}]");
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

        private StructuralMaterialType GetStructuralMaterial(Element element)
        {
            // For FamilyInstances (Framing/Columns), check the property directly
            if (element is FamilyInstance fi)
            {
                return fi.StructuralMaterialType;
            }

            // For Walls/Floors/Foundations, check BuiltInParameter or default to Concrete
            Parameter p = element.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_TYPE);
            if (p != null && p.AsInteger() != 0) return (StructuralMaterialType)p.AsInteger();

            if (element is Wall || element is Floor || element.Category.Id == new ElementId(BuiltInCategory.OST_StructuralFoundation))
            {
                return StructuralMaterialType.Concrete;
            }

            return StructuralMaterialType.Other;
        }

        private bool DoesPhysicallyOverlap(Element e1, Element e2)
        {
            try
            {
                // Double check using the most restrictive filter
                return new FilteredElementCollector(e1.Document, new List<ElementId> { e2.Id })
                    .WherePasses(new ElementIntersectsElementFilter(e1))
                    .Any();
            }
            catch { return false; }
        }

        private bool IsSteel(StructuralMaterialType mat) => mat == StructuralMaterialType.Steel;
        private bool IsConcrete(StructuralMaterialType mat) => mat == StructuralMaterialType.Concrete || mat == StructuralMaterialType.PrecastConcrete;

        public string GetName() => "Join Advance Handler";
    }
}
