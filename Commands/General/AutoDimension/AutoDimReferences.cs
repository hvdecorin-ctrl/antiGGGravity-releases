using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimUnits;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Extracts geometry references (face refs, center refs, grid refs)
    /// for use in dimension creation. Matches the Python reference logic.
    /// </summary>
    public static class AutoDimReferences
    {
        /// <summary>
        /// Extracts all planar-face references from an element's geometry,
        /// sorted into X-normal and Y-normal lists. Populates FacesX, FacesY,
        /// and optionally CenterRefX/CenterRefY on the ElementInfo.
        /// </summary>
        public static void ExtractAllFaces(ElementInfo ei, View view)
        {
            var elem = ei.Element;
            ei.FacesX = new List<(Reference, double)>();
            ei.FacesY = new List<(Reference, double)>();

            var opt = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                View = view,
            };
            var geo = elem.get_Geometry(opt);
            if (geo == null) return;

            bool isFamily = elem is FamilyInstance;

            foreach (var item in geo)
            {
                try
                {
                    if (item is GeometryInstance gi && isFamily)
                    {
                        var xform = gi.Transform;
                        var symGeo = gi.GetSymbolGeometry();
                        if (symGeo == null) continue;

                        foreach (var symItem in symGeo)
                        {
                            if (symItem is not Solid solid || solid.Faces.Size == 0) continue;
                            foreach (Face face in solid.Faces)
                            {
                                if (face is not PlanarFace pf) continue;
                                var symRef = pf.Reference;
                                if (symRef == null) continue;

                                var wn = xform.OfVector(pf.FaceNormal);
                                var wo = xform.OfPoint(pf.Origin);
                                var instRef = SymbolToInstanceRef(symRef, (FamilyInstance)elem, view.Document);
                                if (instRef == null) continue;

                                if (Math.Abs(wn.X) > 0.9)
                                    ei.FacesX.Add((instRef, wo.X));
                                else if (Math.Abs(wn.Y) > 0.9)
                                    ei.FacesY.Add((instRef, wo.Y));
                            }
                        }
                    }
                    else if (item is Solid s && s.Faces.Size > 0)
                    {
                        foreach (Face face in s.Faces)
                        {
                            if (face is not PlanarFace pf) continue;
                            var r = pf.Reference;
                            if (r == null) continue;
                            var n = pf.FaceNormal;
                            if (Math.Abs(n.X) > 0.9)
                                ei.FacesX.Add((r, pf.Origin.X));
                            else if (Math.Abs(n.Y) > 0.9)
                                ei.FacesY.Add((r, pf.Origin.Y));
                        }
                    }
                }
                catch { continue; }
            }

            ei.FacesX.Sort((a, b) => a.Item2.CompareTo(b.Item2));
            ei.FacesY.Sort((a, b) => a.Item2.CompareTo(b.Item2));

            // For columns and foundations, ALWAYS get center references
            if (elem is FamilyInstance fi)
            {
                bool forceCenter = ei.Category == "Column" || ei.Category == "Foundation";
                if (forceCenter || ei.FacesX.Count < 2)
                {
                    var cref = GetCenterRef(fi, "x");
                    if (cref != null) ei.CenterRefX = cref;
                }
                if (forceCenter || ei.FacesY.Count < 2)
                {
                    var cref = GetCenterRef(fi, "y");
                    if (cref != null) ei.CenterRefY = cref;
                }
            }
        }

        /// <summary>
        /// Gets face references of an element.
        /// Returns (refLo, refHi, coordLo, coordHi).
        /// When refHi is null → center-point dimensioning mode.
        /// </summary>
        public static (Reference refLo, Reference refHi, double coordLo, double coordHi)
            GetFaces(ElementInfo ei, string axis, View view)
        {
            if (ei.FacesX == null) ExtractAllFaces(ei, view);

            // For columns and foundations, always prefer center dimensioning
            if (ei.Category == "Column" || ei.Category == "Foundation")
            {
                var centerRef = axis == "x" ? ei.CenterRefX : ei.CenterRefY;
                if (centerRef != null)
                {
                    double coord = axis == "x" ? ei.Cx : ei.Cy;
                    return (centerRef, null, coord, coord);
                }
            }

            var faces = axis == "x" ? ei.FacesX : ei.FacesY;
            if (faces.Count >= 2)
                return (faces[0].Ref, faces[^1].Ref, faces[0].Coord, faces[^1].Coord);

            // Fallback: center reference for any remaining elements
            {
                var centerRef = axis == "x" ? ei.CenterRefX : ei.CenterRefY;
                if (centerRef != null)
                {
                    double coord = axis == "x" ? ei.Cx : ei.Cy;
                    return (centerRef, null, coord, coord);
                }
            }

            return (null, null, 0, 0);
        }

        /// <summary>
        /// Gets center reference plane for round elements.
        /// </summary>
        private static Reference GetCenterRef(FamilyInstance fi, string axis)
        {
            var typesToTry = axis == "x"
                ? new[] { FamilyInstanceReferenceType.CenterLeftRight, FamilyInstanceReferenceType.CenterFrontBack }
                : new[] { FamilyInstanceReferenceType.CenterFrontBack, FamilyInstanceReferenceType.CenterLeftRight };

            foreach (var refType in typesToTry)
            {
                try
                {
                    var refs = fi.GetReferences(refType);
                    if (refs != null && refs.Count > 0) return refs[0];
                }
                catch { continue; }
            }
            return null;
        }

        /// <summary>
        /// Converts a reference from GetSymbolGeometry() to an instance reference.
        /// </summary>
        private static Reference SymbolToInstanceRef(Reference symRef, FamilyInstance instance, Document doc)
        {
            try
            {
                string stable = symRef.ConvertToStableRepresentation(doc);
                int colonIdx = stable.IndexOf(':');
                string newStable = GetIdValue(instance).ToString() + stable.Substring(colonIdx);
                return Reference.ParseFromStableRepresentation(doc, newStable);
            }
            catch { return null; }
        }

        /// <summary>
        /// Gets a Reference from a grid's geometry (for dimensioning).
        /// </summary>
        public static Reference GetGridRef(Grid grid, View view)
        {
            try
            {
                var opt = new Options
                {
                    ComputeReferences = true,
                    IncludeNonVisibleObjects = true,
                    View = view,
                };
                var geo = grid.get_Geometry(opt);
                if (geo != null)
                {
                    foreach (var item in geo)
                    {
                        if (item is Line line && line.Reference != null)
                            return line.Reference;
                    }
                }
                var crv = grid.Curve;
                if (crv?.Reference != null) return crv.Reference;
            }
            catch { }
            return null;
        }
    }
}
