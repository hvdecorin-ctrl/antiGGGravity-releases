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
        /// Gets the base elevation (bottom Z) of a column.
        /// </summary>
        private static double GetBaseElevation(FamilyInstance column)
        {
            BoundingBoxXYZ bbox = column.get_BoundingBox(null);
            return bbox?.Min.Z ?? 0;
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
    }
}
