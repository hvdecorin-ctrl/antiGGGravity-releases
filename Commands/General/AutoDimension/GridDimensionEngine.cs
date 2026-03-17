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
    /// Creates dimension chains between grids.
    /// Matches the Python make_grid_chain / _grid_chain_exists / _get_bubble_baseline logic.
    /// </summary>
    public static class GridDimensionEngine
    {
        private static IList<Dimension> _cachedDimsOnView;
        private static ElementId _cachedViewId;

        private static IList<Dimension> GetCachedDims(Document doc, View view)
        {
            if (_cachedDimsOnView == null || _cachedViewId != view.Id)
            {
                try
                {
                    _cachedDimsOnView = new FilteredElementCollector(doc, view.Id)
                        .OfClass(typeof(Dimension))
                        .Cast<Dimension>()
                        .ToList();
                    _cachedViewId = view.Id;
                }
                catch
                {
                    _cachedDimsOnView = new List<Dimension>();
                }
            }
            return _cachedDimsOnView;
        }

        /// <summary>
        /// Must be called before a new Auto Dims session to clear stale cache.
        /// </summary>
        public static void ClearCache()
        {
            _cachedDimsOnView = null;
            _cachedViewId = null;
        }

        /// <summary>
        /// Creates a dimension chain between sorted grids along measureAxis.
        /// Returns the number of dimensions created (0 or 1).
        /// </summary>
        public static int MakeGridChain(Document doc, View view,
            List<GridInfo> gridsSorted, string measureAxis, double offsetMm, AutoDimSettings settings)
        {
            if (gridsSorted.Count < 2) return 0;
            if (GridChainExists(doc, view, gridsSorted)) return 0;

            var refs = new List<Reference>();
            foreach (var g in gridsSorted)
            {
                var r = GetGridRef(g.Element, view);
                if (r != null) refs.Add(r);
            }
            if (refs.Count < 2) return 0;

            var (bubbleCoord, bubbleSide) = GetBubbleBaseline(gridsSorted, measureAxis);
            double? existingOffset = FindExistingGridDimOffset(doc, view, gridsSorted, measureAxis, bubbleSide);

            double off = MmToFt(offsetMm);
            double perp;
            if (bubbleSide > 0)
            {
                double bBase = existingOffset.HasValue ? Math.Max(bubbleCoord, existingOffset.Value) : bubbleCoord;
                perp = bBase + off;
            }
            else
            {
                double bBase = existingOffset.HasValue ? Math.Min(bubbleCoord, existingOffset.Value) : bubbleCoord;
                perp = bBase - off;
            }

            XYZ p0, p1;
            if (measureAxis == "x")
            {
                p0 = new XYZ(gridsSorted[0].CoordFt, perp, 0);
                p1 = new XYZ(gridsSorted[gridsSorted.Count - 1].CoordFt, perp, 0);
            }
            else
            {
                p0 = new XYZ(perp, gridsSorted[0].CoordFt, 0);
                p1 = new XYZ(perp, gridsSorted[gridsSorted.Count - 1].CoordFt, 0);
            }

            var ra = new ReferenceArray();
            foreach (var r in refs) ra.Append(r);
            try
            {
                var dim = doc.Create.NewDimension(view, Line.CreateBound(p0, p1), ra);
                return dim != null ? 1 : 0;
            }
            catch { return 0; }
        }

        private static bool GridChainExists(Document doc, View view, List<GridInfo> gridsSorted)
        {
            var gridIds = new HashSet<long>(gridsSorted.Select(g => GetIdValue(g.Element)));
            if (gridIds.Count < 2) return false;

            var dims = GetCachedDims(doc, view);
            foreach (var dim in dims)
            {
                try
                {
                    var refs = dim.References;
                    if (refs == null || refs.Size < 2) continue;

                    var dimRefIds = new HashSet<long>();
                    foreach (Reference r in refs)
                        dimRefIds.Add(r.ElementId.GetIdValue());

                    if (gridIds.IsSubsetOf(dimRefIds)) return true;
                }
                catch { continue; }
            }
            return false;
        }

        private static (double coord, int side) GetBubbleBaseline(List<GridInfo> gridsSorted, string measureAxis)
        {
            var bubbleCoords = new List<double>();
            var nonBubbleCoords = new List<double>();

            foreach (var g in gridsSorted)
            {
                var bp = g.BubbleEnd == "p0" ? g.P0 : g.P1;
                var nbp = g.BubbleEnd == "p0" ? g.P1 : g.P0;

                if (measureAxis == "x")
                {
                    bubbleCoords.Add(bp.Y);
                    nonBubbleCoords.Add(nbp.Y);
                }
                else
                {
                    bubbleCoords.Add(bp.X);
                    nonBubbleCoords.Add(nbp.X);
                }
            }

            double avgBubble = bubbleCoords.Average();
            double avgNonBubble = nonBubbleCoords.Average();

            if (avgBubble > avgNonBubble)
                return (bubbleCoords.Max(), -1);
            else
                return (bubbleCoords.Min(), +1);
        }

        private static double? FindExistingGridDimOffset(Document doc, View view,
            List<GridInfo> gridsSorted, string measureAxis, int side)
        {
            var gridIds = new HashSet<long>(gridsSorted.Select(g => GetIdValue(g.Element)));
            var dims = GetCachedDims(doc, view);
            double? bestPerp = null;

            foreach (var dim in dims)
            {
                try
                {
                    var refs = dim.References;
                    if (refs == null || refs.Size < 2) continue;

                    int matchCount = 0;
                    foreach (Reference r in refs)
                        if (gridIds.Contains(r.ElementId.GetIdValue())) matchCount++;
                    if (matchCount < 2) continue;

                    if (dim.Curve is Line line)
                    {
                        if (measureAxis == "x")
                        {
                            double perp = side > 0
                                ? Math.Max(line.GetEndPoint(0).Y, line.GetEndPoint(1).Y)
                                : Math.Min(line.GetEndPoint(0).Y, line.GetEndPoint(1).Y);
                            if (!bestPerp.HasValue || (side > 0 ? perp > bestPerp : perp < bestPerp))
                                bestPerp = perp;
                        }
                        else
                        {
                            double perp = side > 0
                                ? Math.Max(line.GetEndPoint(0).X, line.GetEndPoint(1).X)
                                : Math.Min(line.GetEndPoint(0).X, line.GetEndPoint(1).X);
                            if (!bestPerp.HasValue || (side > 0 ? perp > bestPerp : perp < bestPerp))
                                bestPerp = perp;
                        }
                    }
                }
                catch { continue; }
            }
            return bestPerp;
        }
    }
}
