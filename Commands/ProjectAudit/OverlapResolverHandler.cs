using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.Commands.ProjectAudit
{
    public class OverlapResolverHandler : IExternalEventHandler
    {
        // --- Public properties set by the UI before Raise() ---
        public ApplicationScope Scope { get; set; } = ApplicationScope.ActiveView;
        public bool IncludeTextNotes { get; set; } = true;
        public bool IncludeTags { get; set; } = true;
        public double PaddingMm { get; set; } = 2.0;
        public Action<string> StatusCallback { get; set; }
        public Action OperationCompleted { get; set; }

        // Internal data class to hold annotation info
        private class AnnotationInfo
        {
            public ElementId Id { get; set; }
            public BoundingBoxXYZ Box { get; set; }
            public bool IsTag { get; set; }
            public bool WasUnpinned { get; set; }
            public double Height { get; set; } // For identical box nudging
        }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                if (Scope == ApplicationScope.EntireProject)
                {
                    // Process every view that can contain annotations
                    var views = new FilteredElementCollector(doc)
                        .OfClass(typeof(View))
                        .Cast<View>()
                        .Where(v => !v.IsTemplate && v.CanBePrinted && !(v is View3D))
                        .ToList();

                    int totalResolved = 0;
                    int viewsProcessed = 0;

                    using (Transaction t = new Transaction(doc, "Resolve Overlaps (Project)"))
                    {
                        t.Start();

                        foreach (var view in views)
                        {
                            int resolved = ProcessView(doc, view);
                            if (resolved > 0)
                            {
                                totalResolved += resolved;
                                viewsProcessed++;
                            }
                        }

                        t.Commit();
                    }

                    StatusCallback?.Invoke($"Resolved {totalResolved} overlaps across {viewsProcessed} views.");
                }
                else
                {
                    // Active View or Selection
                    View activeView = doc.ActiveView;

                    if (activeView == null || activeView.IsTemplate || activeView is View3D)
                    {
                        StatusCallback?.Invoke("Cannot process 3D views. Please switch to a 2D view.");
                        return;
                    }

                    List<AnnotationInfo> annotations;

                    if (Scope == ApplicationScope.Selection)
                    {
                        var selectedIds = uidoc.Selection.GetElementIds();
                        if (selectedIds.Count == 0)
                        {
                            StatusCallback?.Invoke("No elements selected.");
                            return;
                        }
                        annotations = CollectFromIds(doc, activeView, selectedIds);
                    }
                    else
                    {
                        annotations = CollectFromView(doc, activeView);
                    }

                    if (annotations.Count < 2)
                    {
                        StatusCallback?.Invoke("Need at least 2 annotations to check for overlaps.");
                        return;
                    }

                    using (Transaction t = new Transaction(doc, "Resolve Overlaps"))
                    {
                        t.Start();
                        int resolved = ResolveOverlaps(doc, activeView, annotations);
                        t.Commit();

                        StatusCallback?.Invoke($"Resolved {resolved} overlaps among {annotations.Count} annotations.");
                    }
                }

                OperationCompleted?.Invoke();
            }
            catch (Exception ex)
            {
                StatusCallback?.Invoke($"Error: {ex.Message}");
            }
        }

        public string GetName() => "Overlap Resolver Handler";

        #region Collection

        private int ProcessView(Document doc, View view)
        {
            var annotations = CollectFromView(doc, view);
            if (annotations.Count < 2) return 0;
            return ResolveOverlaps(doc, view, annotations);
        }

        private List<AnnotationInfo> CollectFromView(Document doc, View view)
        {
            var result = new List<AnnotationInfo>();

            if (IncludeTextNotes)
            {
                try
                {
                    var notes = new FilteredElementCollector(doc, view.Id)
                        .OfClass(typeof(TextNote))
                        .WhereElementIsNotElementType()
                        .Cast<TextNote>()
                        .ToList();

                    foreach (var note in notes)
                    {
                        var box = note.get_BoundingBox(view);
                        if (box != null)
                        {
                            result.Add(new AnnotationInfo
                            {
                                Id = note.Id,
                                Box = box,
                                IsTag = false,
                                Height = box.Max.Y - box.Min.Y
                            });
                        }
                    }
                }
                catch { }
            }

            if (IncludeTags)
            {
                try
                {
                    var tags = new FilteredElementCollector(doc, view.Id)
                        .OfClass(typeof(IndependentTag))
                        .WhereElementIsNotElementType()
                        .Cast<IndependentTag>()
                        .ToList();

                    foreach (var tag in tags)
                    {
                        var box = GetAnnotationBounds(tag, view);
                        if (box != null)
                        {
                            result.Add(new AnnotationInfo
                            {
                                Id = tag.Id,
                                Box = box,
                                IsTag = true,
                                Height = box.Max.Y - box.Min.Y
                            });
                        }
                    }
                }
                catch { }
            }

            return result;
        }

        private BoundingBoxXYZ GetAnnotationBounds(Element elem, View view)
        {
            if (elem is IndependentTag tag && tag.HasLeader)
            {
                // To get ONLY the tag head bounding box (ignoring the leader),
                // we temporarily disable the leader. This requires a transaction or subtransaction.
                Document doc = elem.Document;
                BoundingBoxXYZ box = null;
                
                using (SubTransaction st = new SubTransaction(doc))
                {
                    st.Start();
                    tag.HasLeader = false;
                    doc.Regenerate();
                    box = tag.get_BoundingBox(view);
                    st.RollBack(); // Reset it back immediately but we have the box
                }
                return box;
            }
            
            return elem.get_BoundingBox(view);
        }

        private List<AnnotationInfo> CollectFromIds(Document doc, View view, ICollection<ElementId> ids)
        {
            var result = new List<AnnotationInfo>();

            foreach (var id in ids)
            {
                Element elem = doc.GetElement(id);
                if (elem == null) continue;

                bool isTag = false;
                bool include = false;

                if (IncludeTextNotes && elem is TextNote)
                {
                    include = true;
                    isTag = false;
                }
                else if (IncludeTags && elem is IndependentTag)
                {
                    include = true;
                    isTag = true;
                }

                if (!include) continue;

                var box = GetAnnotationBounds(elem, view);
                if (box != null)
                {
                    result.Add(new AnnotationInfo
                    {
                        Id = id,
                        Box = box,
                        IsTag = isTag,
                        Height = box.Max.Y - box.Min.Y
                    });
                }
            }

            return result;
        }

        #endregion

        #region Overlap Resolution

        private int ResolveOverlaps(Document doc, View view, List<AnnotationInfo> annotations)
        {
            // CRITICAL FIX: Padding must account for view scale. 2.0 mm on paper is much larger in model space.
            double scale = (view.ViewType == ViewType.DrawingSheet) ? 1.0 : (double)view.Scale;
            double paddingFeet = (PaddingMm / 304.8) * scale;
            
            int totalResolved = 0;
            int maxPasses = 5; // Increased passes for cascading overlaps

            for (int pass = 0; pass < maxPasses; pass++)
            {
                int resolvedThisPass = 0;

                // Sort by position: top-to-bottom, left-to-right
                annotations.Sort((a, b) =>
                {
                    int cmp = b.Box.Max.Y.CompareTo(a.Box.Max.Y); // top first
                    if (cmp != 0) return cmp;
                    return a.Box.Min.X.CompareTo(b.Box.Min.X); // left first
                });

                for (int i = 0; i < annotations.Count; i++)
                {
                    for (int j = i + 1; j < annotations.Count; j++)
                    {
                        var a = annotations[i];
                        var b = annotations[j];

                        if (!AreOverlapping(a.Box, b.Box, paddingFeet))
                            continue;

                        // Determine which element to move:
                        // Move the one with the smaller bounding box area
                        double areaA = GetArea(a.Box);
                        double areaB = GetArea(b.Box);

                        AnnotationInfo toMove = areaA <= areaB ? a : b;
                        AnnotationInfo stationary = areaA <= areaB ? b : a;

                        XYZ nudge = CalculateNudgeVector(stationary.Box, toMove.Box, paddingFeet);

                        // If they are exactly on top of each other, nudge by height + padding
                        if (nudge.GetLength() < 1e-6)
                        {
                            nudge = new XYZ(0, -(toMove.Height + paddingFeet), 0);
                        }

                        // Unpin if necessary
                        Element elem = doc.GetElement(toMove.Id);
                        if (elem != null && elem.Pinned)
                        {
                            elem.Pinned = false;
                            toMove.WasUnpinned = true;
                        }

                        // Move the element
                        bool moved = MoveAnnotation(doc, toMove, nudge);

                        if (moved)
                        {
                            resolvedThisPass++;
                            
                            // Important: Regenerate to update geometry state
                            doc.Regenerate();

                            // Refresh bounding box after move
                            Element movedElem = doc.GetElement(toMove.Id);
                            if (movedElem != null)
                            {
                                var newBox = GetAnnotationBounds(movedElem, view);
                                if (newBox != null)
                                    toMove.Box = newBox;
                            }
                        }
                    }
                }

                totalResolved += resolvedThisPass;
                if (resolvedThisPass == 0)
                    break; // No more overlaps to resolve
            }

            return totalResolved;
        }

        private bool AreOverlapping(BoundingBoxXYZ a, BoundingBoxXYZ b, double padding)
        {
            // AABB intersection check in XY plane with padding
            return (a.Min.X - padding < b.Max.X + padding) &&
                   (a.Max.X + padding > b.Min.X - padding) &&
                   (a.Min.Y - padding < b.Max.Y + padding) &&
                   (a.Max.Y + padding > b.Min.Y - padding);
        }

        private XYZ CalculateNudgeVector(BoundingBoxXYZ stationary, BoundingBoxXYZ toMove, double padding)
        {
            // Calculate the minimum translation vector (MTV) to separate the two boxes
            // We find the axis with the smallest overlap and push along that axis

            double overlapX_right = (stationary.Max.X + padding) - (toMove.Min.X - padding);
            double overlapX_left = (toMove.Max.X + padding) - (stationary.Min.X - padding);
            double overlapY_up = (stationary.Max.Y + padding) - (toMove.Min.Y - padding);
            double overlapY_down = (toMove.Max.Y + padding) - (stationary.Min.Y - padding);

            // Find minimum overlap direction
            double minOverlap = double.MaxValue;
            XYZ nudge = XYZ.Zero;

            // Push toMove to the right
            if (overlapX_right > 0 && overlapX_right < minOverlap)
            {
                minOverlap = overlapX_right;
                nudge = new XYZ(overlapX_right, 0, 0);
            }

            // Push toMove to the left
            if (overlapX_left > 0 && overlapX_left < minOverlap)
            {
                minOverlap = overlapX_left;
                nudge = new XYZ(-overlapX_left, 0, 0);
            }

            // Push toMove up
            if (overlapY_up > 0 && overlapY_up < minOverlap)
            {
                minOverlap = overlapY_up;
                nudge = new XYZ(0, overlapY_up, 0);
            }

            // Push toMove down
            if (overlapY_down > 0 && overlapY_down < minOverlap)
            {
                minOverlap = overlapY_down;
                nudge = new XYZ(0, -overlapY_down, 0);
            }

            return nudge;
        }

        private double GetArea(BoundingBoxXYZ box)
        {
            return (box.Max.X - box.Min.X) * (box.Max.Y - box.Min.Y);
        }

        private bool MoveAnnotation(Document doc, AnnotationInfo info, XYZ nudge)
        {
            try
            {
                Element elem = doc.GetElement(info.Id);
                if (elem == null) return false;

                if (info.IsTag && elem is IndependentTag tag)
                {
                    // For tags, move via TagHeadPosition
                    XYZ currentPos = tag.TagHeadPosition;
                    tag.TagHeadPosition = currentPos + nudge;
                    return true;
                }
                else
                {
                    // For text notes and other annotations, use MoveElement
                    ElementTransformUtils.MoveElement(doc, info.Id, nudge);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
