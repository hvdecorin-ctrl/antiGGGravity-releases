using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimUnits;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimCore;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimReferences;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Dimensions individual elements (walls, columns, foundations) relative to grids.
    /// Handles the 3 scenarios: grid inside, grid on edge, grid outside.
    /// Matches Python dim_along_axis exactly.
    /// </summary>
    public static class ElementDimensionEngine
    {
        /// <summary>
        /// Dimensions a single element along one axis.
        /// Returns the number of dimensions created.
        /// </summary>
        public static int DimAlongAxis(Document doc, View view, ElementInfo ei, string axis,
            List<GridInfo> gridsPerp, List<GridInfo> gridsPar,
            List<ElementInfo> allElems, List<(Dimension Dim, int Side)> dimsToAdjust,
            int? forcedSide, List<(double, double, double, double)> occupiedZones,
            HashSet<string> dimKeys, AutoDimSettings settings)
        {
            int created = 0;
            var (refLo, refHi, cLo, cHi) = GetFaces(ei, axis, view);
            if (refLo == null) return 0;

            bool isCenter = refHi == null;

            double perpLo = axis == "x" ? ei.MinY : ei.MinX;
            double perpHi = axis == "x" ? ei.MaxY : ei.MaxX;
            double anchorLo = perpLo;
            double anchorHi = perpHi;

            // For elongated elements perpendicular to this axis, use nearest parallel grid as anchor
            if (ei.Dominant != null)
            {
                bool isElongatedPerp = (axis == "x" && ei.Dominant == "y") || (axis == "y" && ei.Dominant == "x");
                if (isElongatedPerp)
                {
                    double centerCoord = axis == "x" ? ei.Cy : ei.Cx;
                    GridInfo bestParGrid = null;
                    double bestParDist = double.MaxValue;
                    foreach (var g in gridsPar)
                    {
                        double d = Math.Abs(g.CoordFt - centerCoord);
                        if (d < bestParDist) { bestParDist = d; bestParGrid = g; }
                    }
                    if (bestParGrid != null) { anchorLo = bestParGrid.CoordFt; anchorHi = bestParGrid.CoordFt; }
                }
            }

            int sideDefault = PickSide(ei, axis, gridsPar, forcedSide);
            double off1 = MmToFt(settings.Offset1Mm);
            double off2 = MmToFt(settings.Offset2Mm);

            bool isWall = ei.Category.Equals("Wall", StringComparison.OrdinalIgnoreCase);
            bool canFlip = !isWall;

            (double row1, double row2, int side) GetLinesForSide(int s)
            {
                double r1 = s < 0 ? anchorLo - off1 : anchorHi + off1;
                double r2 = s < 0 ? anchorLo - off2 : anchorHi + off2;
                double r1Adj = r1;
                if (occupiedZones != null)
                    r1Adj = AdjustPerpForCollisions(axis, cLo, cHi, r1, s, occupiedZones, settings);
                double shiftAbs = Math.Abs(r1Adj - r1);
                return (r1Adj, r2 + (r1Adj - r1), s);
            }

            double lineRow1, lineRow2;
            int side;
            if (canFlip)
            {
                var (r1a, r2a, sa) = GetLinesForSide(sideDefault);
                var (r1b, r2b, sb) = GetLinesForSide(-sideDefault);
                double shiftA = Math.Abs(r1a - (sideDefault < 0 ? anchorLo - off1 : anchorHi + off1));
                double shiftB = Math.Abs(r1b - (-sideDefault < 0 ? anchorLo - off1 : anchorHi + off1));
                if (shiftA <= shiftB) { lineRow1 = r1a; lineRow2 = r2a; side = sa; }
                else { lineRow1 = r1b; lineRow2 = r2b; side = sb; }
            }
            else
            {
                (lineRow1, lineRow2, side) = GetLinesForSide(sideDefault);
            }

            var (bestGrid, bestDistFt) = FindNearestGrid(ei, axis, gridsPerp);

            // ---- CENTER MODE (round elements) ----
            if (isCenter)
            {
                if (bestGrid == null || FtToMm(bestDistFt) > settings.MaxSnapDistMm) return 0;

                double gridCoord = bestGrid.CoordFt;
                var gridRef = GetGridRef(bestGrid.Element, view);
                if (gridRef == null) return 0;

                // Dedup
                if (dimKeys != null)
                {
                    double perpCoord = axis == "x" ? ei.Cy : ei.Cx;
                    var key = DedupKey(axis, bestGrid.Name, cLo, perpCoord, settings);
                    string keyStr = $"{key.axis}|{key.gridName}|{key.rounded}|{key.perpRounded}";
                    if (dimKeys.Contains(keyStr)) return 0;
                    dimKeys.Add(keyStr);
                }

                if (occupiedZones != null)
                    lineRow1 = AdjustPerpForCollisions(axis, cLo, gridCoord, lineRow1, side, occupiedZones, settings);

                var (p0, p1) = LinePoints(cLo, cLo, axis, lineRow1, gridCoord);
                var dim = MakeDim(doc, view, new[] { gridRef, refLo }, p0, p1);
                if (dim != null)
                {
                    dimsToAdjust.Add((dim, side));
                    created++;
                    if (occupiedZones != null)
                        RegisterZone(axis, Math.Min(cLo, gridCoord), Math.Max(cLo, gridCoord), lineRow1, occupiedZones);
                }
                return created;
            }

            // ---- RECTANGULAR MODE ----
            if (bestGrid == null || FtToMm(bestDistFt) > settings.MaxSnapDistMm)
            {
                var dimG = DimOverall(doc, view, refLo, refHi, cLo, cHi, axis, lineRow1);
                if (dimG != null) { dimsToAdjust.Add((dimG, side)); created++; }
                return created;
            }

            double gridCoordRect = bestGrid.CoordFt;
            var gridRefRect = GetGridRef(bestGrid.Element, view);
            if (gridRefRect == null)
            {
                var dimG = DimOverall(doc, view, refLo, refHi, cLo, cHi, axis, lineRow1);
                if (dimG != null) { dimsToAdjust.Add((dimG, side)); created++; }
                return created;
            }

            // Dedup
            if (dimKeys != null)
            {
                double perpCoord = axis == "x" ? ei.Cy : ei.Cx;
                var kLo = DedupKey(axis, bestGrid.Name, cLo, perpCoord, settings);
                var kHi = DedupKey(axis, bestGrid.Name, cHi, perpCoord, settings);
                string combined = $"{axis}|{bestGrid.Name}|{kLo.rounded}|{kHi.rounded}|{perpCoord:F0}";
                if (dimKeys.Contains(combined)) return 0;
                dimKeys.Add(combined);
            }

            double tolZero = MmToFt(settings.ZeroTolMm);
            double tolInter = MmToFt(settings.IntersectTolMm);
            bool intersects = (cLo - tolInter) < gridCoordRect && gridCoordRect < (cHi + tolInter);

            if (intersects)
            {
                double dLo = Math.Abs(cLo - gridCoordRect);
                double dHi = Math.Abs(cHi - gridCoordRect);
                bool isOnLo = dLo <= tolZero;
                bool isOnHi = dHi <= tolZero;

                if (isOnLo || isOnHi)
                {
                    // Grid on edge → single snap dimension
                    var refsSnap = new List<Reference> { gridRefRect };
                    if (!isOnLo) refsSnap.Add(refLo);
                    if (!isOnHi) refsSnap.Add(refHi);

                    if (occupiedZones != null)
                        lineRow1 = AdjustPerpForCollisions(axis, cLo, cHi, lineRow1, side, occupiedZones, settings);

                    var (p0, p1) = LinePoints(cLo, cHi, axis, lineRow1, gridCoordRect);
                    var dim1 = MakeDim(doc, view, refsSnap, p0, p1);
                    if (dim1 != null)
                    {
                        dimsToAdjust.Add((dim1, side));
                        created++;
                        if (occupiedZones != null)
                            RegisterZone(axis, cLo, cHi, lineRow1, occupiedZones);
                    }
                }
                else
                {
                    // Grid inside element → locator + overall
                    var nearestRef = dLo < dHi ? refLo : refHi;
                    double nearestC = dLo < dHi ? cLo : cHi;

                    if (occupiedZones != null)
                        lineRow1 = AdjustPerpForCollisions(axis, gridCoordRect, nearestC, lineRow1, side, occupiedZones, settings);

                    var (pL0, pL1) = LinePoints(gridCoordRect, nearestC, axis, lineRow1, gridCoordRect);
                    var dimLocate = MakeDim(doc, view, new[] { gridRefRect, nearestRef }, pL0, pL1);
                    if (dimLocate != null)
                    {
                        dimsToAdjust.Add((dimLocate, side));
                        created++;
                        if (occupiedZones != null)
                            RegisterZone(axis, Math.Min(gridCoordRect, nearestC), Math.Max(gridCoordRect, nearestC), lineRow1, occupiedZones);
                    }

                    // Full wall dimension
                    if (occupiedZones != null)
                        lineRow2 = AdjustPerpForCollisions(axis, cLo, cHi, lineRow2, side, occupiedZones, settings);

                    var dimOverall = DimOverall(doc, view, refLo, refHi, cLo, cHi, axis, lineRow2);
                    if (dimOverall != null)
                    {
                        dimsToAdjust.Add((dimOverall, side));
                        created++;
                        if (occupiedZones != null)
                            RegisterZone(axis, cLo, cHi, lineRow2, occupiedZones);
                    }
                }
            }
            else
            {
                // Grid outside element → chain G->E->E
                var refsChain = new List<Reference> { gridRefRect, refLo, refHi };
                double spanMin = Math.Min(Math.Min(cLo, cHi), gridCoordRect);
                double spanMax = Math.Max(Math.Max(cLo, cHi), gridCoordRect);

                if (occupiedZones != null)
                    lineRow1 = AdjustPerpForCollisions(axis, spanMin, spanMax, lineRow1, side, occupiedZones, settings);

                double safeLineRow1 = AvoidCollision(ei, lineRow1, spanMin, spanMax, axis, side, allElems ?? new List<ElementInfo>());

                var (p0, p1) = LinePoints(cLo, cHi, axis, safeLineRow1, gridCoordRect);
                var dimChain = MakeDim(doc, view, refsChain, p0, p1);
                if (dimChain != null)
                {
                    dimsToAdjust.Add((dimChain, side));
                    created++;
                    if (occupiedZones != null)
                        RegisterZone(axis, spanMin, spanMax, safeLineRow1, occupiedZones);
                }
            }

            return created;
        }

        /// <summary>
        /// Dimensions a cluster of elements as a single chain.
        /// Matches Python dim_cluster_along_axis.
        /// </summary>
        public static int DimClusterAlongAxis(Document doc, View view,
            List<ElementInfo> cluster, string axis,
            List<GridInfo> gridsPerp, List<GridInfo> gridsPar,
            List<(Dimension Dim, int Side)> dimsToAdjust,
            List<(double, double, double, double)> occupiedZones,
            HashSet<string> dimKeys, AutoDimSettings settings)
        {
            if (cluster.Count < 2) return 0;

            // Identify potential pile cap (largest element)
            double maxArea = 0; int maxIndex = -1; double totalArea = 0;
            for (int i = 0; i < cluster.Count; i++)
            {
                double area = cluster[i].WidthFt * cluster[i].DepthFt;
                totalArea += area;
                if (area > maxArea) { maxArea = area; maxIndex = i; }
            }

            int capIndex = -1;
            if (cluster.Count > 1)
            {
                double avgOtherArea = (totalArea - maxArea) / (cluster.Count - 1);
                if (avgOtherArea > 0 && maxArea >= 2.0 * avgOtherArea)
                {
                    var largeEi = cluster[maxIndex];
                    if (largeEi.Category.Equals("Foundation", StringComparison.OrdinalIgnoreCase)
                        || largeEi.Category.Equals("Column", StringComparison.OrdinalIgnoreCase))
                        capIndex = maxIndex;
                }
            }

            // Collect all references and coordinates
            var allRefs = new List<(Reference Ref, double Coord)>();
            for (int i = 0; i < cluster.Count; i++)
            {
                if (i == capIndex && cluster.Count > 1) continue;
                var (rLo, rHi, cLo2, cHi2) = GetFaces(cluster[i], axis, view);
                if (rLo == null) continue;

                if (rHi == null)
                    allRefs.Add((rLo, cLo2));
                else
                {
                    allRefs.Add((rLo, cLo2));
                    allRefs.Add((rHi, cHi2));
                }
            }

            // Include cap edges for foundations
            if (capIndex != -1)
            {
                var capEi = cluster[capIndex];
                if (capEi.Category.Equals("Foundation", StringComparison.OrdinalIgnoreCase))
                {
                    var (crLo, crHi, ccLo, ccHi) = GetFaces(capEi, axis, view);
                    if (crLo != null) allRefs.Add((crLo, ccLo));
                    if (crHi != null) allRefs.Add((crHi, ccHi));
                }
            }

            if (allRefs.Count < 1) return 0;

            // Sort and dedup
            allRefs.Sort((a, b) => a.Coord.CompareTo(b.Coord));
            double dedupTol = MmToFt(settings.DedupTolMm);
            var deduped = new List<(Reference Ref, double Coord)> { allRefs[0] };
            for (int i = 1; i < allRefs.Count; i++)
            {
                if (Math.Abs(allRefs[i].Coord - deduped[^1].Coord) > dedupTol)
                    deduped.Add(allRefs[i]);
            }
            if (deduped.Count < 1) return 0;

            // Find nearest grid
            GridInfo bestGrid = null;
            double? bestD = null;
            double clusterMin = deduped[0].Coord;
            double clusterMax = deduped[^1].Coord;
            double clusterCentroid = (clusterMin + clusterMax) / 2.0;

            foreach (var g in gridsPerp)
            {
                double d = Math.Abs(clusterCentroid - g.CoordFt);
                if (!bestD.HasValue || d < bestD.Value) { bestD = d; bestGrid = g; }
            }

            List<Reference> refsFinal;
            double? gridCoord = null;

            if (bestGrid == null || FtToMm(bestD.Value) > settings.MaxSnapDistMm)
            {
                if (deduped.Count < 2) return 0;
                refsFinal = deduped.Select(r => r.Ref).ToList();
            }
            else
            {
                gridCoord = bestGrid.CoordFt;
                var gridRef = GetGridRef(bestGrid.Element, view);

                // Check if grid splits any element interior
                bool splitsAny = false;
                double tolInter = MmToFt(settings.IntersectTolMm);
                foreach (var clEi in cluster)
                {
                    var (_, _, cl, ch) = GetFaces(clEi, axis, view);
                    if (cl != 0 && ch != 0 && (cl + tolInter) < gridCoord && gridCoord < (ch - tolInter))
                    {
                        splitsAny = true; break;
                    }
                }

                if (splitsAny)
                {
                    refsFinal = deduped.Select(r => r.Ref).ToList();
                }
                else
                {
                    var withGrid = new List<(Reference Ref, double Coord)>(deduped);
                    if (gridRef != null) withGrid.Add((gridRef, gridCoord.Value));
                    withGrid.Sort((a, b) => a.Coord.CompareTo(b.Coord));

                    var dedupedFinal = new List<(Reference Ref, double Coord)> { withGrid[0] };
                    for (int i = 1; i < withGrid.Count; i++)
                    {
                        if (Math.Abs(withGrid[i].Coord - dedupedFinal[^1].Coord) > dedupTol)
                            dedupedFinal.Add(withGrid[i]);
                    }
                    refsFinal = dedupedFinal.Select(r => r.Ref).ToList();
                }
            }

            if (refsFinal.Count < 2) return 0;

            // Dedup check
            if (dimKeys != null)
            {
                var coordsKey = string.Join(",", deduped.Select(r => Math.Round(FtToMm(r.Coord) / settings.DedupTolMm)));
                double perpCentroid = cluster.Average(e => axis == "x" ? e.Cy : e.Cx);
                long perpRounded = (long)Math.Round(FtToMm(perpCentroid) / settings.DedupTolMm);
                string gridName = bestGrid?.Name ?? "NoGrid";
                string clusterKey = $"CL|{axis}|{gridName}|{coordsKey}|{perpRounded}";
                if (dimKeys.Contains(clusterKey)) return 0;
                dimKeys.Add(clusterKey);
            }

            // Position dim line
            double pLo = axis == "x" ? cluster.Min(e => e.MinY) : cluster.Min(e => e.MinX);
            double pHi = axis == "x" ? cluster.Max(e => e.MaxY) : cluster.Max(e => e.MaxX);
            int sideDefault = PickSide(cluster[0], axis, gridsPar);
            double off = MmToFt(settings.Offset1Mm);

            bool isWallCluster = cluster.Any(e => e.Category.Equals("Wall", StringComparison.OrdinalIgnoreCase));
            bool canFlip = !isWallCluster;

            (double pos, double shift) TrySide(int s)
            {
                double pBase = s < 0 ? pLo - off : pHi + off;
                double pAdj = pBase;
                if (occupiedZones != null)
                    pAdj = AdjustPerpForCollisions(axis, deduped[0].Coord, deduped[^1].Coord, pBase, s, occupiedZones, settings);
                return (pAdj, Math.Abs(pAdj - pBase));
            }

            double linePos;
            int finalSide;
            if (canFlip)
            {
                var (pos1, shift1) = TrySide(sideDefault);
                var (pos2, shift2) = TrySide(-sideDefault);
                if (shift1 <= shift2) { linePos = pos1; finalSide = sideDefault; }
                else { linePos = pos2; finalSide = -sideDefault; }
            }
            else
            {
                (linePos, _) = TrySide(sideDefault);
                finalSide = sideDefault;
            }

            // Create dimension chain
            int created2 = 0;
            var (p0, p1) = LinePoints(deduped[0].Coord, deduped[^1].Coord, axis, linePos, gridCoord);
            var clusterDim = MakeDim(doc, view, refsFinal, p0, p1);
            if (clusterDim != null)
            {
                dimsToAdjust.Add((clusterDim, finalSide));
                created2++;
                if (occupiedZones != null)
                    RegisterZone(axis, clusterMin, clusterMax, linePos, occupiedZones);
            }

            // Locator dimension if grid was split
            if (bestGrid != null && gridCoord.HasValue && refsFinal.Count < (deduped.Count + 1))
            {
                Reference nearestEdgeRef = null;
                double nearestEdgeC = 0;
                double? bestEdgeD = null;
                foreach (var (r, c) in deduped)
                {
                    double ed = Math.Abs(c - gridCoord.Value);
                    if (!bestEdgeD.HasValue || ed < bestEdgeD.Value)
                    {
                        bestEdgeD = ed;
                        nearestEdgeRef = r;
                        nearestEdgeC = c;
                    }
                }

                if (nearestEdgeRef != null)
                {
                    double off2 = MmToFt(settings.Offset2Mm);
                    var gridRef = GetGridRef(bestGrid.Element, view);

                    (double locPos, double locShift) TryLocatorSide(int s)
                    {
                        double lBase = s < 0 ? pLo - off2 : pHi + off2;
                        double lAdj = lBase;
                        if (occupiedZones != null)
                            lAdj = AdjustPerpForCollisions(axis, gridCoord.Value, nearestEdgeC, lBase, s, occupiedZones, settings);
                        return (lAdj, Math.Abs(lAdj - lBase));
                    }

                    var (lp1, ls1) = TryLocatorSide(finalSide);
                    var (lp2, ls2) = TryLocatorSide(-finalSide);
                    double locatorLinePos = ls1 <= ls2 ? lp1 : lp2;

                    if (gridRef != null)
                    {
                        var (pL0, pL1) = LinePoints(gridCoord.Value, nearestEdgeC, axis, locatorLinePos, gridCoord);
                        var locatorDim = MakeDim(doc, view, new[] { gridRef, nearestEdgeRef }, pL0, pL1);
                        if (locatorDim != null)
                        {
                            dimsToAdjust.Add((locatorDim, finalSide));
                            created2++;
                            if (occupiedZones != null)
                                RegisterZone(axis, Math.Min(gridCoord.Value, nearestEdgeC),
                                    Math.Max(gridCoord.Value, nearestEdgeC), locatorLinePos, occupiedZones);
                        }
                    }
                }
            }

            return created2;
        }
    }
}
