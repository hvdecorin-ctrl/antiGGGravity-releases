using Autodesk.Revit.DB;

namespace antiGGGravity.StructuralRebar.DTO
{
    /// <summary>
    /// Immutable snapshot of host element geometry.
    /// Created once per element by a GeometryModule, reused by all layout generators.
    /// All dimensions in FEET (Revit internal units).
    /// Slant-safe: uses Local Coordinate System (LCS) instead of world axes.
    /// </summary>
    public readonly struct HostGeometry
    {
        // === LOCAL COORDINATE SYSTEM ===
        /// <summary>Length direction — follows element slope. DO NOT flatten Z.</summary>
        public readonly XYZ LAxis;
        /// <summary>Width direction — perpendicular to L, stays horizontal.</summary>
        public readonly XYZ WAxis;
        /// <summary>Height direction — L × W cross product (local "up").</summary>
        public readonly XYZ HAxis;

        // === SLOPE ===
        /// <summary>Angle from horizontal in radians.</summary>
        public readonly double SlopeAngle;
        /// <summary>True if SlopeAngle > 0.01 rad (~0.6°).</summary>
        public bool IsSlanted => SlopeAngle > 0.01;

        // === POSITIONS (world coordinates) ===
        /// <summary>Center of element.</summary>
        public readonly XYZ Origin;
        /// <summary>Start along L-axis.</summary>
        public readonly XYZ StartPoint;
        /// <summary>End along L-axis.</summary>
        public readonly XYZ EndPoint;

        // === SOLID Z BOUNDS (world Z, from actual solid vertices — reliable for offset beams) ===
        /// <summary>Bottom Z of the actual solid geometry.</summary>
        public readonly double SolidZMin;
        /// <summary>Top Z of the actual solid geometry.</summary>
        public readonly double SolidZMax;

        // === DIMENSIONS (scalar, feet) ===
        /// <summary>True length along slope (not horizontal projection).</summary>
        public readonly double Length;
        public readonly double Width;
        public readonly double Height;

        // === COVER DISTANCES (feet) ===
        public readonly double CoverTop;
        public readonly double CoverBottom;
        public readonly double CoverExterior;
        public readonly double CoverInterior;
        public readonly double CoverOther;

        // === WALL-SPECIFIC ===
        /// <summary>Wall orientation normal (exterior face direction). Zero for non-walls.</summary>
        public readonly XYZ Normal;
        /// <summary>Wall thickness (= Width for walls). Zero for non-walls.</summary>
        public readonly double Thickness;

        // === METADATA ===
        /// <summary>Which extraction method produced the primary dimensions.</summary>
        public readonly GeometrySource Source;

        public HostGeometry(
            XYZ lAxis, XYZ wAxis, XYZ hAxis, double slopeAngle,
            XYZ origin, XYZ startPoint, XYZ endPoint,
            double length, double width, double height,
            double coverTop, double coverBottom,
            double coverExterior, double coverInterior, double coverOther,
            XYZ normal, double thickness,
            GeometrySource source,
            double solidZMin = 0, double solidZMax = 0)
        {
            LAxis = lAxis;
            WAxis = wAxis;
            HAxis = hAxis;
            SlopeAngle = slopeAngle;
            Origin = origin;
            StartPoint = startPoint;
            EndPoint = endPoint;
            SolidZMin = solidZMin;
            SolidZMax = solidZMax;
            Length = length;
            Width = width;
            Height = height;
            CoverTop = coverTop;
            CoverBottom = coverBottom;
            CoverExterior = coverExterior;
            CoverInterior = coverInterior;
            CoverOther = coverOther;
            Normal = normal ?? XYZ.Zero;
            Thickness = thickness;
            Source = source;
        }
    }

    /// <summary>
    /// Indicates which extraction method produced the geometry dimensions.
    /// Priority: SolidFaces > TypeParameters > LocationCurve > BoundingBox.
    /// </summary>
    public enum GeometrySource
    {
        SolidFaces,
        TypeParameters,
        LocationCurve,
        BoundingBox,
        Transform
    }
}
