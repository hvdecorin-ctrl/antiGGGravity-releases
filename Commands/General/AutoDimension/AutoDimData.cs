using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimUnits;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Info about a grid line visible in the current view.
    /// </summary>
    public class GridInfo
    {
        public Grid Element { get; set; }
        public string Name { get; set; }
        public string Orientation { get; set; } // "horizontal" or "vertical"
        public double CoordFt { get; set; }
        public XYZ P0 { get; set; }
        public XYZ P1 { get; set; }
        public string BubbleEnd { get; set; } // "p0" or "p1"
    }

    /// <summary>
    /// Info about a dimensionable element (wall, column, foundation).
    /// </summary>
    public class ElementInfo
    {
        public Element Element { get; set; }
        public string Category { get; set; } // "Wall", "Column", "Foundation"
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
        public double Cx { get; set; }
        public double Cy { get; set; }
        public double WidthFt { get; set; }
        public double DepthFt { get; set; }
        /// <summary>Dominant axis: "x", "y", or null (roughly square)</summary>
        public string Dominant { get; set; }

        // Face references populated by AutoDimReferences
        public List<(Reference Ref, double Coord)> FacesX { get; set; }
        public List<(Reference Ref, double Coord)> FacesY { get; set; }
        public Reference CenterRefX { get; set; }
        public Reference CenterRefY { get; set; }
    }

    /// <summary>
    /// Collects grids and elements from the user selection.
    /// Matches the Python collect_grids_from_selection / collect_elements_from_selection logic.
    /// </summary>
    public static class AutoDimData
    {
        /// <summary>
        /// Collects grids from selected elements. Determines bubble side.
        /// </summary>
        public static List<GridInfo> CollectGrids(IEnumerable<Element> selected, View view)
        {
            var grids = new List<GridInfo>();
            foreach (var elem in selected)
            {
                if (elem is not Grid g) continue;
                try
                {
                    var crv = g.Curve;
                    if (crv is not Line line) continue;

                    var d = line.Direction.Normalize();
                    var p0 = line.GetEndPoint(0);
                    var p1 = line.GetEndPoint(1);

                    string orientation;
                    double coord;
                    if (Math.Abs(d.Y) < 0.1)
                    {
                        orientation = "horizontal";
                        coord = (p0.Y + p1.Y) / 2.0;
                    }
                    else if (Math.Abs(d.X) < 0.1)
                    {
                        orientation = "vertical";
                        coord = (p0.X + p1.X) / 2.0;
                    }
                    else continue;

                    string bubbleEnd = GetBubbleEnd(g, view);

                    grids.Add(new GridInfo
                    {
                        Element = g,
                        Name = g.Name,
                        Orientation = orientation,
                        CoordFt = coord,
                        P0 = p0,
                        P1 = p1,
                        BubbleEnd = bubbleEnd,
                    });
                }
                catch { continue; }
            }
            return grids;
        }

        private static string GetBubbleEnd(Grid grid, View view)
        {
            try
            {
                bool b0 = grid.IsBubbleVisibleInView(DatumEnds.End0, view);
                bool b1 = grid.IsBubbleVisibleInView(DatumEnds.End1, view);
                if (b1 && !b0) return "p1";
                return "p0";
            }
            catch { return "p0"; }
        }

        /// <summary>
        /// Collects walls and columns from selected elements, unpacking Groups and Subcomponents.
        /// </summary>
        public static List<ElementInfo> CollectElements(IEnumerable<Element> selected, Document doc, View view, AutoDimSettings settings)
        {
            var elements = new List<ElementInfo>();
            var seenIds = new HashSet<long>();
            var wallCatId = new ElementId(BuiltInCategory.OST_Walls);
            var strColCatId = new ElementId(BuiltInCategory.OST_StructuralColumns);
            var colCatId = new ElementId(BuiltInCategory.OST_Columns);
            var strFdnCatId = new ElementId(BuiltInCategory.OST_StructuralFoundation);
            var strFrmCatId = new ElementId(BuiltInCategory.OST_StructuralFraming);

            var unpacked = new List<Element>();
            foreach (var e in selected)
            {
                if (e is Grid) continue;
                UnpackElement(e, unpacked, seenIds, doc);
            }

            foreach (var e in unpacked)
            {
                try
                {
                    var catId = e.Category?.Id;
                    if (catId == null) continue;

                    if (catId == wallCatId && settings.DimWalls)
                    {
                        var info = MakeBBox(e, "Wall", view);
                        if (info != null) elements.Add(info);
                    }
                    else if ((catId == strColCatId || catId == colCatId) && settings.DimColumns)
                    {
                        var info = MakeBBox(e, "Column", view);
                        if (info != null) elements.Add(info);
                    }
                    else if (catId == strFdnCatId && settings.DimFoundations)
                    {
                        var info = MakeBBox(e, "Foundation", view);
                        if (info != null) elements.Add(info);
                    }
                    else if (catId == strFrmCatId && settings.DimColumns)
                    {
                        var info = MakeBBox(e, "Column", view);
                        if (info != null) elements.Add(info);
                    }
                }
                catch { continue; }
            }
            return elements;
        }

        private static void UnpackElement(Element elem, List<Element> outList, HashSet<long> seenIds, Document doc)
        {
            if (elem == null) return;
            long eid = GetIdValue(elem);
            if (seenIds.Contains(eid)) return;

            // Unpack Model Groups
            if (elem is Group group)
            {
                seenIds.Add(eid);
                foreach (ElementId mId in group.GetMemberIds())
                {
                    var mElem = doc.GetElement(mId);
                    UnpackElement(mElem, outList, seenIds, doc);
                }
                return;
            }

            // Unpack Nested Families (Subcomponents)
            if (elem is FamilyInstance fi)
            {
                var subIds = fi.GetSubComponentIds();
                if (subIds != null && subIds.Count > 0)
                {
                    foreach (ElementId sId in subIds)
                    {
                        var sElem = doc.GetElement(sId);
                        UnpackElement(sElem, outList, seenIds, doc);
                    }
                }
            }

            outList.Add(elem);
            seenIds.Add(eid);
        }

        private static ElementInfo MakeBBox(Element elem, string cat, View view)
        {
            try
            {
                var bb = elem.get_BoundingBox(view) ?? elem.get_BoundingBox(null);
                if (bb == null) return null;

                double w = Math.Abs(bb.Max.X - bb.Min.X);
                double d = Math.Abs(bb.Max.Y - bb.Min.Y);
                if (FtToMm(w) < 50 || FtToMm(d) < 50) return null;

                double aspect = d > 0.001 ? w / d : 999;
                string dominant = null;
                if (aspect > 3.0) dominant = "x";
                else if (aspect < 1.0 / 3.0) dominant = "y";

                return new ElementInfo
                {
                    Element = elem,
                    Category = cat,
                    MinX = bb.Min.X, MaxX = bb.Max.X,
                    MinY = bb.Min.Y, MaxY = bb.Max.Y,
                    Cx = (bb.Min.X + bb.Max.X) / 2.0,
                    Cy = (bb.Min.Y + bb.Max.Y) / 2.0,
                    WidthFt = w, DepthFt = d,
                    Dominant = dominant,
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// Groups nearby elements into spatial clusters using union-find.
        /// Elements whose bounding boxes are within ClusterGapMm are merged.
        /// </summary>
        public static List<List<ElementInfo>> ClusterElements(List<ElementInfo> elements, AutoDimSettings settings)
        {
            int n = elements.Count;
            if (n == 0) return new List<List<ElementInfo>>();

            double threshold = MmToFt(settings.ClusterGapMm);
            int[] parent = Enumerable.Range(0, n).ToArray();

            int Find(int x)
            {
                while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
                return x;
            }
            void Union(int a, int b)
            {
                int ra = Find(a), rb = Find(b);
                if (ra != rb) parent[rb] = ra;
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    var ei = elements[i]; var ej = elements[j];
                    double dx = Math.Max(0, Math.Max(ei.MinX, ej.MinX) - Math.Min(ei.MaxX, ej.MaxX));
                    double dy = Math.Max(0, Math.Max(ei.MinY, ej.MinY) - Math.Min(ei.MaxY, ej.MaxY));
                    if (dx < threshold && dy < threshold && ei.Category == ej.Category) Union(i, j);
                }
            }

            var groups = new Dictionary<int, List<ElementInfo>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(i);
                if (!groups.ContainsKey(root)) groups[root] = new List<ElementInfo>();
                groups[root].Add(elements[i]);
            }
            return groups.Values.ToList();
        }
    }
}
