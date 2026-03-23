using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimUnits;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Shared dimension creation and collision-avoidance primitives.
    /// Matches the Python make_dim, _displace_small_texts, collision prevention, etc.
    /// </summary>
    public static class AutoDimCore
    {
        // =====================================================================
        // DIMENSION CREATION
        // =====================================================================

        /// <summary>Creates a dimension from a list of references and a line.</summary>
        public static Dimension MakeDim(Document doc, View view, IList<Reference> refs, XYZ p0, XYZ p1)
        {
            if (refs.Count < 2) return null;
            var ra = new ReferenceArray();
            foreach (var r in refs) ra.Append(r);
            try
            {
                var ln = Line.CreateBound(p0, p1);
                return doc.Create.NewDimension(view, ln, ra);
            }
            catch { return null; }
        }

        /// <summary>Shifts dimension text outward when the segment is too small to read.</summary>
        public static void DisplaceSmallTexts(Dimension dim, View view, int side)
        {
            int scale;
            try { scale = view.Scale; } catch { scale = 100; }

            double textWidthMm = 6.0 * scale;

            XYZ direction;
            try
            {
                var crv = dim.Curve;
                if (crv is not Line line) return;
                direction = line.Direction.Normalize();
            }
            catch { return; }

            // Compute outward direction based on dim orientation and side
            // For horizontal dim (direction ≈ X): outward is side * Y
            // For vertical dim (direction ≈ Y): outward is side * X
            XYZ outward;
            if (Math.Abs(direction.X) > Math.Abs(direction.Y))
                outward = new XYZ(0, side, 0);  // Horizontal dim → push along Y
            else
                outward = new XYZ(side, 0, 0);  // Vertical dim → push along X

            bool anyDisplaced = false;

            // Handle multiple segments (chains) — move outward with staggered gap
            try
            {
                var segs = dim.Segments;
                if (segs != null && segs.Size > 0)
                {
                    double outwardGap = MmToFt(5.0 * scale); // Increased to 5mm as requested

                    int smallCount = 0;
                    for (int i = 0; i < segs.Size; i++)
                    {
                        var seg = segs.get_Item(i);
                        var val = seg.Value;
                        if (val == null) continue;

                        double valMm = FtToMm(val.Value);
                        if (valMm >= textWidthMm)
                        {
                            smallCount = 0;
                            continue;
                        }

                        if (!seg.IsTextPositionAdjustable()) continue;

                        // Midpoint Baseline: Reset text to the midpoint of its segment
                        // for perfectly consistent outcomes across all segments.
                        XYZ segMid = seg.Origin;
                        if (segMid == null) continue;

                        double gapMultiplier = 1.0 + smallCount;
                        seg.TextPosition = new XYZ(
                            segMid.X + outward.X * outwardGap * gapMultiplier,
                            segMid.Y + outward.Y * outwardGap * gapMultiplier,
                            segMid.Z);

                        anyDisplaced = true;
                        smallCount++;
                    }

                    // Suppress leaders for all displaced multi-segment dims
                    if (anyDisplaced)
                    {
                        try { dim.HasLeader = false; } catch { }
                    }
                    return;
                }
            }
            catch { }

            // Single-segment dimension — keep along-axis tail, no leader
            try
            {
                var val = dim.Value;
                if (val == null) return;
                if (FtToMm(val.Value) >= textWidthMm) return;
                if (!dim.IsTextPositionAdjustable()) return;

                // Midpoint baseline for single dimension
                XYZ dimMid = dim.Origin;
                if (dimMid == null) return;

                double offsetAlong = MmToFt(5.0 * scale); // Decreased to 5mm as requested
                dim.TextPosition = new XYZ(
                    dimMid.X + direction.X * offsetAlong,
                    dimMid.Y + direction.Y * offsetAlong,
                    dimMid.Z);

                try { dim.HasLeader = false; } catch { }
            }
            catch { }
        }

        /// <summary>Audits dimensions for overlaps and fixes them by shifting the entire dimension line.</summary>
        public static void AuditAndFixDimensions(Document doc, View view, IList<Dimension> dims)
        {
            int scale;
            try { scale = view.Scale; } catch { scale = 100; }

            // Margins - use very tight tolerances to move only what is absolutely clashing
            double marginFt = MmToFt(0.5 * scale); // 0.5mm physical margin
            double shiftStepFt = MmToFt(2.5 * scale); // 2.5mm physical shift step

            // Collect host elements
            var hostZones = new List<(XYZ min, XYZ max)>();
            var collector = new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType()
                .WherePasses(new LogicalOrFilter(new List<ElementFilter>
                {
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns),
                    new ElementCategoryFilter(BuiltInCategory.OST_StructuralFoundation),
                    new ElementCategoryFilter(BuiltInCategory.OST_Walls)
                }));
            
            foreach (var e in collector)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(view);
                if (bb != null)
                {
                    hostZones.Add((new XYZ(bb.Min.X, bb.Min.Y, 0), new XYZ(bb.Max.X, bb.Max.Y, 0)));
                }
            }

            // Collect Dim Info
            var dimInfos = new List<DimAuditInfo>();
            foreach (var dim in dims)
            {
                if (dim.Curve is not Line line) continue;
                XYZ dir = line.Direction.Normalize();
                bool isHorizontal = Math.Abs(dir.X) > Math.Abs(dir.Y);
                XYZ outward = isHorizontal ? new XYZ(0, 1, 0) : new XYZ(1, 0, 0);

                var texts = new List<(XYZ min, XYZ max)>();
                
                if (dim.Segments != null && dim.Segments.Size > 0)
                {
                    for (int i = 0; i < dim.Segments.Size; i++)
                    {
                        var seg = dim.Segments.get_Item(i);
                        if (seg.Value == null || !seg.IsTextPositionAdjustable()) continue;

                        double valFt = seg.Value.Value;
                        string textStr = Math.Round(FtToMm(valFt)).ToString();
                        double widthDist = MmToFt((textStr.Length * 1.5 + 2.0) * scale) / 2.0;
                        double heightDist = MmToFt(3.0 * scale) / 2.0;
                        
                        XYZ pos = seg.TextPosition;
                        texts.Add((new XYZ(pos.X - widthDist, pos.Y - heightDist, 0), 
                                   new XYZ(pos.X + widthDist, pos.Y + heightDist, 0)));
                    }
                }
                else
                {
                    if (dim.Value != null && dim.IsTextPositionAdjustable())
                    {
                        double valFt = dim.Value.Value;
                        string textStr = Math.Round(FtToMm(valFt)).ToString();
                        double widthDist = MmToFt((textStr.Length * 1.5 + 2.0) * scale) / 2.0;
                        double heightDist = MmToFt(3.0 * scale) / 2.0;

                        XYZ pos = dim.TextPosition;
                        texts.Add((new XYZ(pos.X - widthDist, pos.Y - heightDist, 0), 
                                   new XYZ(pos.X + widthDist, pos.Y + heightDist, 0)));
                    }
                }
                
                if (texts.Count > 0)
                {
                    XYZ lineStart, lineEnd;
                    if (line.IsBound)
                    {
                        lineStart = line.GetEndPoint(0);
                        lineEnd = line.GetEndPoint(1);
                    }
                    else
                    {
                        // Unbound curve – use the dimension's bounding box instead
                        BoundingBoxXYZ dbb = dim.get_BoundingBox(view);
                        if (dbb != null)
                        {
                            lineStart = dbb.Min;
                            lineEnd = dbb.Max;
                        }
                        else
                        {
                            continue;
                        }
                    }
                    dimInfos.Add(new DimAuditInfo { Dim = dim, IsHorizontal = isHorizontal, Outward = outward, Texts = texts, LineStart = lineStart, LineEnd = lineEnd });
                }
            }

            bool Overlap(XYZ min1, XYZ max1, XYZ min2, XYZ max2, double margin)
            {
                return (max1.X + margin) >= (min2.X - margin) && (max2.X + margin) >= (min1.X - margin) &&
                       (max1.Y + margin) >= (min2.Y - margin) && (max2.Y + margin) >= (min1.Y - margin);
            }

            bool HasClash(DimAuditInfo info, XYZ shift)
            {
                var movedTexts = info.Texts.Select(t => (min: t.min + shift, max: t.max + shift)).ToList();
                var movedLineMin = new XYZ(Math.Min(info.LineStart.X, info.LineEnd.X) + shift.X, Math.Min(info.LineStart.Y, info.LineEnd.Y) + shift.Y, 0);
                var movedLineMax = new XYZ(Math.Max(info.LineStart.X, info.LineEnd.X) + shift.X, Math.Max(info.LineStart.Y, info.LineEnd.Y) + shift.Y, 0);

                // Check vs Hosts
                foreach (var text in movedTexts)
                {
                    foreach (var host in hostZones)
                    {
                        if (Overlap(text.min, text.max, host.min, host.max, marginFt)) return true;
                    }
                }

                // Check vs other dims
                foreach (var other in dimInfos)
                {
                    if (other == info) continue;

                    // other texts
                    foreach (var myText in movedTexts)
                    {
                        foreach (var otherText in other.Texts)
                        {
                            if (Overlap(myText.min, myText.max, otherText.min, otherText.max, marginFt)) return true;
                        }
                        
                        // my text vs other line
                        var otherLineMin = new XYZ(Math.Min(other.LineStart.X, other.LineEnd.X), Math.Min(other.LineStart.Y, other.LineEnd.Y), 0);
                        var otherLineMax = new XYZ(Math.Max(other.LineStart.X, other.LineEnd.X), Math.Max(other.LineStart.Y, other.LineEnd.Y), 0);
                        if (Overlap(myText.min, myText.max, otherLineMin, otherLineMax, marginFt)) return true;
                    }

                    // other texts vs my line
                    foreach (var otherText in other.Texts)
                    {
                        if (Overlap(otherText.min, otherText.max, movedLineMin, movedLineMax, marginFt)) return true;
                    }
                }
                
                return false;
            }

            foreach (var info in dimInfos)
            {
                if (HasClash(info, XYZ.Zero))
                {
                    // Find a shift to resolve
                    XYZ bestShift = XYZ.Zero;
                    bool found = false;

                    // Try expanding steps 1 to 20
                    for (int step = 1; step <= 20; step++)
                    {
                        XYZ shiftPlus = info.Outward * step * shiftStepFt;
                        if (!HasClash(info, shiftPlus))
                        {
                            bestShift = shiftPlus;
                            found = true;
                            break;
                        }
                        
                        XYZ shiftMinus = -info.Outward * step * shiftStepFt;
                        if (!HasClash(info, shiftMinus))
                        {
                            bestShift = shiftMinus;
                            found = true;
                            break;
                        }
                    }

                    if (found)
                    {
                        try
                        {
                            ElementTransformUtils.MoveElement(doc, info.Dim.Id, bestShift);
                            // Update info
                            info.LineStart += bestShift;
                            info.LineEnd += bestShift;
                            info.Texts = info.Texts.Select(t => (min: t.min + bestShift, max: t.max + bestShift)).ToList();
                        }
                        catch { }
                    }
                }
            }
        }

        class DimAuditInfo
        {
            public Dimension Dim { get; set; }
            public bool IsHorizontal { get; set; }
            public XYZ Outward { get; set; }
            public List<(XYZ min, XYZ max)> Texts { get; set; }
            public XYZ LineStart { get; set; }
            public XYZ LineEnd { get; set; }
        }


        // =====================================================================
        // LINE-POINT CONSTRUCTION
        // =====================================================================

        /// <summary>
        /// Creates dimension line endpoints. axis="x" → horizontal line; "y" → vertical.
        /// </summary>
        public static (XYZ p0, XYZ p1) LinePoints(double coordLo, double coordHi, string axis,
            double perpPos, double? gridCoord = null)
        {
            double lo = Math.Min(coordLo, coordHi);
            double hi = Math.Max(coordLo, coordHi);
            if (gridCoord.HasValue)
            {
                lo = Math.Min(lo, gridCoord.Value);
                hi = Math.Max(hi, gridCoord.Value);
            }
            if (axis == "x")
                return (new XYZ(lo, perpPos, 0), new XYZ(hi, perpPos, 0));
            else
                return (new XYZ(perpPos, lo, 0), new XYZ(perpPos, hi, 0));
        }

        /// <summary>Creates a simple overall dimension between two face refs.</summary>
        public static Dimension DimOverall(Document doc, View view,
            Reference refLo, Reference refHi, double cLo, double cHi,
            string axis, double perpPos)
        {
            var (p0, p1) = LinePoints(cLo, cHi, axis, perpPos);
            return MakeDim(doc, view, new[] { refLo, refHi }, p0, p1);
        }

        // =====================================================================
        // SIDE PICKING
        // =====================================================================

        /// <summary>
        /// Chooses which side to place the dimension line.
        /// Rule: X down/Y left (side=-1) or X up/Y right (side=+1).
        /// Updated: Favors the side AWAY from the nearest parallel grid.
        /// </summary>
        public static int PickSide(ElementInfo ei, string axis, List<GridInfo> gridsParallel, int? forcedSide = null)
        {
            if (forcedSide.HasValue) return forcedSide.Value;
            if (gridsParallel == null || gridsParallel.Count == 0)
                return -1;

            double elemCenter = axis == "x" ? ei.Cy : ei.Cx;
            GridInfo bestGrid = null;
            double bestSign = 0;
            double bestD = double.MaxValue;

            foreach (var g in gridsParallel)
            {
                double d = g.CoordFt - elemCenter;
                double absD = Math.Abs(d);
                if (absD < bestD) { bestD = absD; bestGrid = g; bestSign = d; }
            }
            // If grid is below center (bestSign < 0), pick side=1 (up) 
            // If grid is above center (bestSign > 0), pick side=-1 (down) 
            return bestGrid == null ? -1 : (bestSign < 0 ? 1 : -1);
        }

        /// <summary>Finds the nearest perpendicular grid to the element center.</summary>
        public static (GridInfo grid, double distance) FindNearestGrid(ElementInfo ei, string axis, List<GridInfo> grids)
        {
            double center = axis == "x" ? ei.Cx : ei.Cy;
            GridInfo best = null;
            double bestD = double.MaxValue;
            foreach (var g in grids)
            {
                double d = Math.Abs(center - g.CoordFt);
                if (d < bestD) { bestD = d; best = g; }
            }
            return (best, bestD);
        }

        // =====================================================================
        // COLLISION AVOIDANCE (element-level)
        // =====================================================================

        /// <summary>Pushes perpPos away from other elements that overlap the dim line span.</summary>
        public static double AvoidCollision(ElementInfo ei, double perpPos, double coordLo, double coordHi,
            string axis, int side, List<ElementInfo> allElems)
        {
            double margin = MmToFt(300); // Increased margin
            long myId = GetIdValue(ei.Element);
            double lo = Math.Min(coordLo, coordHi);
            double hi = Math.Max(coordLo, coordHi);

            foreach (var other in allElems)
            {
                if (GetIdValue(other.Element) == myId) continue;
                if (axis == "x")
                {
                    if (other.MaxX < lo || other.MinX > hi) continue;
                    if (other.MinY - margin < perpPos && perpPos < other.MaxY + margin)
                        perpPos = side < 0
                            ? Math.Min(perpPos, other.MinY - margin)
                            : Math.Max(perpPos, other.MaxY + margin);
                }
                else
                {
                    if (other.MaxY < lo || other.MinY > hi) continue;
                    if (other.MinX - margin < perpPos && perpPos < other.MaxX + margin)
                        perpPos = side < 0
                            ? Math.Min(perpPos, other.MinX - margin)
                            : Math.Max(perpPos, other.MaxX + margin);
                }
            }
            return perpPos;
        }

        // =====================================================================
        // DIMENSION-LINE COLLISION (zone-based)
        // =====================================================================

        public static (double minX, double minY, double maxX, double maxY) MakeZone(
            string axis, double coordLo, double coordHi, double perpPos, double? heightFt = null)
        {
            double h = heightFt ?? MmToFt(100); // Increased height for text safety
            double lo = Math.Min(coordLo, coordHi);
            double hi = Math.Max(coordLo, coordHi);
            double textMargin = MmToFt(150); // Increased margin for staggered text
            lo -= textMargin;
            hi += textMargin;

            return axis == "x"
                ? (lo, perpPos - h, hi, perpPos + h)
                : (perpPos - h, lo, perpPos + h, hi);
        }

        public static bool ZoneOverlaps((double, double, double, double) zone,
            List<(double, double, double, double)> occupied)
        {
            foreach (var oz in occupied)
            {
                if (zone.Item3 <= oz.Item1 || oz.Item3 <= zone.Item1) continue;
                if (zone.Item4 <= oz.Item2 || oz.Item4 <= zone.Item2) continue;
                return true;
            }
            return false;
        }

        /// <summary>Shifts perpPos until the dim zone no longer overlaps occupied zones.</summary>
        public static double AdjustPerpForCollisions(string axis, double coordLo, double coordHi,
            double perpPos, int side, List<(double, double, double, double)> occupied, AutoDimSettings settings)
        {
            double lo = Math.Min(coordLo, coordHi);
            double hi = Math.Max(coordLo, coordHi);
            double shift = MmToFt(settings.CollisionShiftMm);

            for (int attempt = 0; attempt < settings.CollisionMaxPasses; attempt++)
            {
                var zone = MakeZone(axis, lo, hi, perpPos);
                if (!ZoneOverlaps(zone, occupied)) break;
                perpPos += shift * side;
            }
            return perpPos;
        }

        public static void RegisterZone(string axis, double coordLo, double coordHi,
            double perpPos, List<(double, double, double, double)> occupied)
        {
            double lo = Math.Min(coordLo, coordHi);
            double hi = Math.Max(coordLo, coordHi);
            occupied.Add(MakeZone(axis, lo, hi, perpPos));
        }

        /// <summary>Marks a corner as busy when a dimension is placed on the other axis.</summary>
        public static void RegisterCrossAxis(List<ElementInfo> cluster, string srcAxis,
            List<(double, double, double, double)> targetOccupied)
        {
            double loX = cluster.Min(e => e.MinX);
            double hiX = cluster.Max(e => e.MaxX);
            double loY = cluster.Min(e => e.MinY);
            double hiY = cluster.Max(e => e.MaxY);
            double buffer = MmToFt(300);

            if (srcAxis == "x")
                targetOccupied.Add((loX - buffer, loY, hiX + buffer, hiY));
            else
                targetOccupied.Add((loX, loY - buffer, hiX, hiY + buffer));
        }

        // =====================================================================
        // DEDUP
        // =====================================================================

        public static (string axis, string gridName, long rounded, long perpRounded) DedupKey(
            string axis, string gridName, double coordFt, double perpFt, AutoDimSettings settings)
        {
            long rounded = (long)Math.Round(FtToMm(coordFt) / settings.DedupTolMm);
            long perpRounded = (long)Math.Round(FtToMm(perpFt) / settings.DedupTolMm);
            return (axis, gridName, rounded, perpRounded);
        }
    }
}
