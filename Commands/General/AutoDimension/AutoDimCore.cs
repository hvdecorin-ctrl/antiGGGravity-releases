using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimUnits;

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
                    double outwardGap = MmToFt(6.0 * scale);

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

                        var tp = seg.TextPosition;
                        if (tp == null) continue;

                        // Move outward with stagger: 1x, 2x, 3x gap for consecutive small segs
                        double gapMultiplier = 1.0 + smallCount;
                        seg.TextPosition = new XYZ(
                            tp.X + outward.X * outwardGap * gapMultiplier,
                            tp.Y + outward.Y * outwardGap * gapMultiplier,
                            tp.Z);

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

                var tp = dim.TextPosition;
                if (tp == null) return;

                double offsetAlong = MmToFt(9.0 * scale);
                dim.TextPosition = new XYZ(
                    tp.X + direction.X * offsetAlong,
                    tp.Y + direction.Y * offsetAlong,
                    tp.Z);

                try { dim.HasLeader = false; } catch { }
            }
            catch { }
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
