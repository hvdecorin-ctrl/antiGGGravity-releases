using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Discovers stacked structural elements across multiple levels.
    /// Used for multi-level column and wall reinforcement.
    /// </summary>
    public static class MultiLevelResolver
    {
        /// <summary>
        /// Tolerance for matching XY positions (50mm in feet).
        /// </summary>
        private static readonly double XY_TOLERANCE = 50.0 / 304.8;

        /// <summary>
        /// Finds all structural columns stacked at the same XY position,
        /// sorted by base elevation (bottom to top).
        /// </summary>
        /// <param name="doc">Revit document.</param>
        /// <param name="column">Any column in the stack.</param>
        /// <returns>Ordered list of columns from lowest to highest level.</returns>
        public static List<FamilyInstance> FindColumnStack(Document doc, FamilyInstance column)
        {
            // Get the XY center of the input column
            XYZ refCenter = GetColumnXYCenter(column);
            if (refCenter == null) return new List<FamilyInstance> { column };

            // Collect all structural columns in the document
            var allColumns = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            // Filter to columns at the same XY position
            var stack = new List<FamilyInstance>();
            foreach (var candidate in allColumns)
            {
                XYZ candidateCenter = GetColumnXYCenter(candidate);
                if (candidateCenter == null) continue;

                double dx = Math.Abs(refCenter.X - candidateCenter.X);
                double dy = Math.Abs(refCenter.Y - candidateCenter.Y);

                if (dx < XY_TOLERANCE && dy < XY_TOLERANCE)
                {
                    stack.Add(candidate);
                }
            }

            // Sort by base elevation (lowest first)
            stack.Sort((a, b) =>
            {
                double zA = GetBaseElevation(a);
                double zB = GetBaseElevation(b);
                return zA.CompareTo(zB);
            });

            // Fallback: if nothing found, return just the input column
            if (stack.Count == 0)
                stack.Add(column);

            return stack;
        }

        /// <summary>
        /// Groups a list of selected columns into separate stacks based on
        /// their XY center position. Each group contains columns
        /// stacked vertically at the same plan position, sorted bottom→top.
        /// Only includes columns that were actually selected.
        /// </summary>
        public static List<List<FamilyInstance>> GroupIntoColumnStacks(Document doc, List<FamilyInstance> selectedColumns)
        {
            var stacks = new List<List<FamilyInstance>>();
            var assigned = new HashSet<ElementId>();

            foreach (var col in selectedColumns)
            {
                if (assigned.Contains(col.Id)) continue;

                // Find the full stack for this column (may include unselected columns)
                var fullStack = FindColumnStack(doc, col);

                // Only keep columns that were selected by the user
                var selectedIds = new HashSet<ElementId>(selectedColumns.Select(c => c.Id));
                var filteredStack = fullStack.Where(c => selectedIds.Contains(c.Id)).ToList();

                if (filteredStack.Count == 0) continue;

                foreach (var c in filteredStack)
                    assigned.Add(c.Id);

                stacks.Add(filteredStack);
            }

            return stacks;
        }

        /// <summary>
        /// Finds all walls stacked vertically above/below the given wall,
        /// sorted by base elevation (bottom to top).
        /// Matches walls that are collinear in plan (same axis, close perpendicular
        /// distance) AND whose projections along the wall axis overlap.
        /// This correctly handles walls of different lengths or slight horizontal offsets.
        /// </summary>
        public static List<Wall> FindWallStack(Document doc, Wall wall)
        {
            var refLine = GetWallXYLine(wall);
            if (refLine == null) return new List<Wall> { wall };

            // Collect all walls in the document
            var allWalls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            // Filter to walls that are collinear and overlap in plan projection
            var stack = new List<Wall>();
            foreach (var candidate in allWalls)
            {
                var candidateLine = GetWallXYLine(candidate);
                if (candidateLine == null) continue;

                if (AreWallsCollinearAndOverlapping(refLine.Value, candidateLine.Value))
                {
                    stack.Add(candidate);
                }
            }

            // Sort by base elevation (lowest first)
            stack.Sort((a, b) =>
            {
                double zA = GetBaseElevation(a);
                double zB = GetBaseElevation(b);
                return zA.CompareTo(zB);
            });

            if (stack.Count == 0)
                stack.Add(wall);

            return stack;
        }

        /// <summary>
        /// Groups a list of selected walls into separate stacks based on
        /// their location-curve XY midpoints. Each group contains walls
        /// stacked vertically at the same plan position, sorted bottom→top.
        /// </summary>
        public static List<List<Wall>> GroupIntoWallStacks(Document doc, List<Wall> selectedWalls)
        {
            var stacks = new List<List<Wall>>();
            var assigned = new HashSet<ElementId>();

            foreach (var wall in selectedWalls)
            {
                if (assigned.Contains(wall.Id)) continue;

                // Find the full stack for this wall (may include unselected walls at other levels)
                var fullStack = FindWallStack(doc, wall);

                // Only keep walls that were selected by the user
                var selectedIds = new HashSet<ElementId>(selectedWalls.Select(w => w.Id));
                var filteredStack = fullStack.Where(w => selectedIds.Contains(w.Id)).ToList();

                if (filteredStack.Count == 0) continue;

                foreach (var w in filteredStack)
                    assigned.Add(w.Id);

                stacks.Add(filteredStack);
            }

            return stacks;
        }

        /// <summary>
        /// Gets the XY center of a column (Z flattened to 0).
        /// Uses the Transform origin for accuracy.
        /// </summary>
        private static XYZ GetColumnXYCenter(FamilyInstance column)
        {
            try
            {
                Transform trans = column.GetTransform();
                XYZ origin = trans.Origin;
                return new XYZ(origin.X, origin.Y, 0);
            }
            catch
            {
                // Fallback to bounding box center
                BoundingBoxXYZ bbox = column.get_BoundingBox(null);
                if (bbox == null) return null;
                double cx = (bbox.Min.X + bbox.Max.X) / 2.0;
                double cy = (bbox.Min.Y + bbox.Max.Y) / 2.0;
                return new XYZ(cx, cy, 0);
            }
        }

        /// <summary>
        /// Gets the XY endpoints of a wall's location curve (Z flattened to 0).
        /// Returns null if the wall has no line-based location curve.
        /// </summary>
        private static (XYZ P0, XYZ P1)? GetWallXYLine(Wall wall)
        {
            LocationCurve loc = wall.Location as LocationCurve;
            if (loc == null || !(loc.Curve is Line line)) return null;
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);
            return (new XYZ(p0.X, p0.Y, 0), new XYZ(p1.X, p1.Y, 0));
        }

        /// <summary>
        /// Checks whether two wall XY lines are collinear (same infinite axis)
        /// AND their projections along that axis overlap.
        /// This correctly handles walls of different lengths that share some extent.
        /// </summary>
        private static bool AreWallsCollinearAndOverlapping(
            (XYZ P0, XYZ P1) lineA, (XYZ P0, XYZ P1) lineB)
        {
            XYZ dirA = (lineA.P1 - lineA.P0);
            double lenA = dirA.GetLength();
            if (lenA < 1e-9) return false;
            XYZ unitA = dirA.Normalize();

            // Check perpendicular distance of lineB's midpoint to the infinite line of A
            XYZ midB = new XYZ(
                (lineB.P0.X + lineB.P1.X) / 2.0,
                (lineB.P0.Y + lineB.P1.Y) / 2.0, 0);
            XYZ vecToMidB = midB - lineA.P0;
            double perpDist = Math.Abs(vecToMidB.X * (-unitA.Y) + vecToMidB.Y * unitA.X);

            if (perpDist > XY_TOLERANCE) return false;

            // Check that the wall directions are parallel (dot product ~1 or ~-1)
            XYZ dirB = (lineB.P1 - lineB.P0);
            double lenB = dirB.GetLength();
            if (lenB < 1e-9) return false;
            XYZ unitB = dirB.Normalize();
            double dot = Math.Abs(unitA.X * unitB.X + unitA.Y * unitB.Y);
            if (dot < 0.95) return false; // Not parallel enough

            // Project both lines onto the axis direction of line A
            double a0 = 0;
            double a1 = lenA;
            double b0 = (lineB.P0 - lineA.P0).DotProduct(unitA);
            double b1 = (lineB.P1 - lineA.P0).DotProduct(unitA);
            if (b0 > b1) { double tmp = b0; b0 = b1; b1 = tmp; }

            // Check for overlap: intervals [a0, a1] and [b0, b1] overlap if min of maxes > max of mins
            double overlapStart = Math.Max(a0, b0);
            double overlapEnd = Math.Min(a1, b1);
            double overlap = overlapEnd - overlapStart;

            // Require at least XY_TOLERANCE of overlap
            return overlap > XY_TOLERANCE;
        }

        /// <summary>
        /// Gets the XY midpoint of a wall's location curve (Z flattened to 0).
        /// </summary>
        private static XYZ GetWallXYMidpoint(Wall wall)
        {
            var line = GetWallXYLine(wall);
            if (line == null) return null;
            return new XYZ(
                (line.Value.P0.X + line.Value.P1.X) / 2.0,
                (line.Value.P0.Y + line.Value.P1.Y) / 2.0, 0);
        }

        private static double GetBaseElevation(Element element)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            return bbox?.Min.Z ?? 0;
        }

        /// <summary>
        /// Gets the XY bounding box of an element (Z flattened).
        /// </summary>
        private static BoundingBoxXYZ GetElementXYBoundingBox(Element element)
        {
            BoundingBoxXYZ bbox = element.get_BoundingBox(null);
            if (bbox == null) return null;
            return new BoundingBoxXYZ
            {
                Min = new XYZ(bbox.Min.X, bbox.Min.Y, 0),
                Max = new XYZ(bbox.Max.X, bbox.Max.Y, 0)
            };
        }

        /// <summary>
        /// Checks if two bounding boxes overlap in the XY plane, with a given tolerance.
        /// </summary>
        private static bool DoBoundingBoxesOverlapXY(BoundingBoxXYZ b1, BoundingBoxXYZ b2, double tolerance)
        {
            bool overlapX = b1.Max.X + tolerance > b2.Min.X && b1.Min.X - tolerance < b2.Max.X;
            bool overlapY = b1.Max.Y + tolerance > b2.Min.Y && b1.Min.Y - tolerance < b2.Max.Y;
            return overlapX && overlapY;
        }

        /// <summary>
        /// Gets summary info for each column in a stack (for UI display).
        /// Returns list of (LevelName, Width, Depth, Height) tuples.
        /// </summary>
        public static List<(string LevelName, double Width, double Depth, double Height)> GetStackInfo(
            Document doc, List<FamilyInstance> stack)
        {
            var info = new List<(string LevelName, double Width, double Depth, double Height)>();

            foreach (var column in stack)
            {
                // Level name
                string levelName = "Unknown";
                Parameter baseLevelParam = column.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                if (baseLevelParam != null && baseLevelParam.HasValue)
                {
                    ElementId levelId = baseLevelParam.AsElementId();
                    Level level = doc.GetElement(levelId) as Level;
                    if (level != null) levelName = level.Name;
                }

                // Dimensions
                double width = GetDimensionParam(column, "Width", "b");
                double depth = GetDimensionParam(column, "Depth", "h");

                BoundingBoxXYZ bbox = column.get_BoundingBox(null);
                double height = bbox != null ? (bbox.Max.Z - bbox.Min.Z) : 0;

                info.Add((levelName, width, depth, height));
            }

            return info;
        }

        private static double GetDimensionParam(FamilyInstance fi, params string[] names)
        {
            foreach (var name in names)
            {
                Parameter p = fi.LookupParameter(name) ?? fi.Symbol?.LookupParameter(name);
                if (p != null && p.HasValue) return p.AsDouble();
            }
            // Fallback to bounding box
            BoundingBoxXYZ bbox = fi.get_BoundingBox(null);
            if (bbox != null) return bbox.Max.X - bbox.Min.X;
            return 0;
        }

        /// <summary>
        /// Gets summary info for each wall in a stack (for UI display).
        /// Returns list of (LevelName, Length, Thickness, Height) tuples.
        /// </summary>
        public static List<(string LevelName, double Length, double Thickness, double Height)> GetWallStackInfo(
            Document doc, List<Wall> stack)
        {
            var info = new List<(string LevelName, double Length, double Thickness, double Height)>();

            foreach (var wall in stack)
            {
                // Level name
                string levelName = "Unknown";
                Parameter baseLevelParam = wall.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                if (baseLevelParam != null && baseLevelParam.HasValue)
                {
                    ElementId levelId = baseLevelParam.AsElementId();
                    Level level = doc.GetElement(levelId) as Level;
                    if (level != null) levelName = level.Name;
                }

                // Dimensions
                double length = 0;
                Parameter lengthParam = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);
                if (lengthParam != null && lengthParam.HasValue) length = lengthParam.AsDouble();

                double thickness = wall.WallType.Width;

                // Height
                double height = 0;
                Parameter heightParam = wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM);
                if (heightParam != null && heightParam.HasValue) 
                {
                    height = heightParam.AsDouble();
                }
                else
                {
                    BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                    if (bbox != null) height = bbox.Max.Z - bbox.Min.Z;
                }

                info.Add((levelName, length, thickness, height));
            }

            return info;
        }
    }
}
