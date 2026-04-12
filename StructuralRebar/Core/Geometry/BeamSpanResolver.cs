using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Geometry
{
    /// <summary>
    /// Analyzes a selection of beams to group collinear/connected spans into continuous beam structures.
    /// Handles plan-oriented collinearity (same XY direction, sharing endpoints).
    /// </summary>
    public static class BeamSpanResolver
    {
        private static readonly double DIST_TOLERANCE = 50.0 / 304.8; // 50mm in feet
        private static readonly double ANGLE_TOLERANCE = 1.0; // Degrees

        /// <summary>
        /// Groups selected beams into multiple 'continuous beams'.
        /// Each list within the result is a set of beams forming one continuous line.
        /// </summary>
        public static List<List<FamilyInstance>> GroupSelectedBeams(List<FamilyInstance> selectedBeams)
        {
            var results = new List<List<FamilyInstance>>();
            var assigned = new HashSet<ElementId>();

            // Group by approximate direction first (must be parallel to be collinear)
            var groupsByDir = GroupByDirection(selectedBeams);

            foreach (var group in groupsByDir)
            {
                var remainingInGroup = new List<FamilyInstance>(group);

                while (remainingInGroup.Count > 0)
                {
                    var beam = remainingInGroup[0];
                    if (assigned.Contains(beam.Id))
                    {
                        remainingInGroup.RemoveAt(0);
                        continue;
                    }

                    // Build a continuous chain for this beam within this directional group
                    var chain = ResolveContinuousChain(beam, remainingInGroup);
                    
                    if (chain.Count > 0)
                    {
                        foreach (var b in chain) assigned.Add(b.Id);
                        results.Add(chain);
                        remainingInGroup.RemoveAll(b => assigned.Contains(b.Id));
                    }
                    else
                    {
                        remainingInGroup.RemoveAt(0);
                    }
                }
            }

            return results;
        }

        private static List<List<FamilyInstance>> GroupByDirection(List<FamilyInstance> beams)
        {
            var groups = new List<List<FamilyInstance>>();
            var handled = new HashSet<ElementId>();

            foreach (var beam in beams)
            {
                if (handled.Contains(beam.Id)) continue;

                XYZ dir1 = GetBeamDirection(beam);
                if (dir1 == null) continue;

                var group = new List<FamilyInstance> { beam };
                handled.Add(beam.Id);

                foreach (var other in beams)
                {
                    if (handled.Contains(other.Id)) continue;

                    XYZ dir2 = GetBeamDirection(other);
                    if (dir2 == null) continue;

                    // Parallel or antiparallel check
                    double dot = Math.Abs(dir1.DotProduct(dir2));
                    if (dot > Math.Cos(ANGLE_TOLERANCE * Math.PI / 180.0))
                    {
                        group.Add(other);
                        handled.Add(other.Id);
                    }
                }
                groups.Add(group);
            }

            return groups;
        }

        private static List<FamilyInstance> ResolveContinuousChain(FamilyInstance seed, List<FamilyInstance> filterPool)
        {
            var chain = new List<FamilyInstance> { seed };
            bool added;

            do
            {
                added = false;
                // Look for connectors at 'ends' of the current chain
                foreach (var candidate in filterPool)
                {
                    if (chain.Any(b => b.Id == candidate.Id)) continue;

                    if (AreBeamsConnected(chain.First(), candidate, true) || 
                        AreBeamsConnected(chain.Last(), candidate, false))
                    {
                        // Check if it should go at start or end
                        if (AreBeamsConnected(chain.First(), candidate, true))
                            chain.Insert(0, candidate);
                        else
                            chain.Add(candidate);

                        added = true;
                        break;
                    }
                }
            } while (added);

            // Re-sort chain from one end to another based on projected distance
            SortChain(chain);

            return chain;
        }

        private static bool AreBeamsConnected(FamilyInstance b1, FamilyInstance b2, bool atStart)
        {
            Curve c1 = (b1.Location as LocationCurve)?.Curve;
            Curve c2 = (b2.Location as LocationCurve)?.Curve;
            if (c1 == null || c2 == null) return false;

            XYZ[] pts1 = { c1.GetEndPoint(0), c1.GetEndPoint(1) };
            XYZ[] pts2 = { c2.GetEndPoint(0), c2.GetEndPoint(1) };

            foreach (var p1 in pts1)
            {
                foreach (var p2 in pts2)
                {
                    // For multi-span, we usually care about XY connection (same plan line)
                    // Even if levels vary slightly or slopes differ, if they share a plan node
                    double dx = p1.X - p2.X;
                    double dy = p1.Y - p2.Y;
                    double dist2D = Math.Sqrt(dx * dx + dy * dy);
                    
                    if (dist2D < DIST_TOLERANCE) return true;
                }
            }
            return false;
        }

        private static void SortChain(List<FamilyInstance> chain)
        {
            if (chain.Count <= 1) return;

            XYZ axis = GetBeamDirection(chain[0]);
            if (axis == null) return;

            // Simple sort by projection along the common axis
            chain.Sort((a, b) =>
            {
                XYZ pA = (a.Location as LocationCurve).Curve.GetEndPoint(0);
                XYZ pB = (b.Location as LocationCurve).Curve.GetEndPoint(0);
                double valA = pA.DotProduct(axis);
                double valB = pB.DotProduct(axis);
                return valA.CompareTo(valB);
            });
        }

        private static XYZ GetBeamDirection(FamilyInstance beam)
        {
            Curve c = (beam.Location as LocationCurve)?.Curve;
            if (c is Line line)
            {
                XYZ dir = line.Direction;
                return new XYZ(dir.X, dir.Y, 0).Normalize();
            }
            return null;
        }

        /// <summary>
        /// Info about a column/wall support detected along a beam's path.
        /// </summary>
        public struct SupportInfo
        {
            /// <summary>Distance from beam start point to the support centerline along beam axis.</summary>
            public double CenterOffset;
            /// <summary>Distance from beam start point to the near face of the support.</summary>
            public double NearFaceOffset;
            /// <summary>Distance from beam start point to the far face of the support.</summary>
            public double FarFaceOffset;
            /// <summary>Width of the support along the beam axis.</summary>
            public double SupportWidth;
            /// <summary>Element ID of the support.</summary>
            public ElementId ElementId;
            /// <summary>Whether this is an end support (at beam start or end) vs intermediate.</summary>
            public bool IsEndSupport;
            /// <summary>Support priority for hierarchy resolution (1=Col/Wall, 2=Beam).</summary>
            public int Priority;
            /// <summary>Depth of the support (used for beam-beam resolution).</summary>
            public double SupportDepth;
        }

        /// <summary>
        /// Finds all columns and walls that intersect a single beam's path.
        /// Returns supports sorted by distance from beam start, excluding the beam's own endpoints.
        /// </summary>
        public static List<SupportInfo> FindIntermediateSupports(
            Document doc, FamilyInstance beam, double beamWidth)
        {
            Curve beamCurve = (beam.Location as LocationCurve)?.Curve;
            if (beamCurve == null) return new List<SupportInfo>();

            XYZ start = beamCurve.GetEndPoint(0);
            XYZ end = beamCurve.GetEndPoint(1);

            BoundingBoxXYZ bbox = beam.get_BoundingBox(null);
            double? minZ = bbox != null ? bbox.Min.Z - 1.0 : (double?)null;
            double? maxZ = bbox != null ? bbox.Max.Z + 1.0 : (double?)null;

            return FindSupportsAlongLine(doc, start, end, beamWidth, new List<ElementId> { beam.Id }, minZ, maxZ);
        }

        /// <summary>
        /// Finds all columns and walls that intersect an arbitrary line path.
        /// Returns supports sorted by distance from lineStart.
        /// </summary>
        public static List<SupportInfo> FindSupportsAlongLine(
            Document doc, XYZ lineStart, XYZ lineEnd, double supportWidth, ICollection<ElementId> excludeIds = null, 
            double? minZ = null, double? maxZ = null)
        {
            XYZ lineDir = (lineEnd - lineStart).Normalize();
            double lineLength = lineStart.DistanceTo(lineEnd);
            double halfWidth = supportWidth / 2.0;

            // Perpendicular direction for lateral proximity check
            XYZ linePerp = new XYZ(-lineDir.Y, lineDir.X, 0);

            double proximityTol = halfWidth + 50.0 / 304.8; // line half-width + 50mm

            var supports = new List<SupportInfo>();

            // === Search columns ===
            var columns = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_StructuralColumns)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .ToList();

            foreach (var col in columns)
            {
                if (excludeIds != null && excludeIds.Contains(col.Id)) continue;
                BoundingBoxXYZ bbox = col.get_BoundingBox(null);
                if (bbox == null) continue;

                if (minZ.HasValue && bbox.Max.Z < minZ.Value) continue;
                if (maxZ.HasValue && bbox.Min.Z > maxZ.Value) continue;

                var result = ProjectSupportOntoBeam(bbox, lineStart, lineDir, linePerp, lineLength, proximityTol);
                if (result.HasValue)
                {
                    var info = result.Value;
                    info.ElementId = col.Id;
                    info.Priority = 1; // High priority (Column)
                    info.SupportDepth = bbox.Max.Z - bbox.Min.Z;
                    supports.Add(info);
                }
            }

            // === Search walls ===
            var walls = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Walls)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .ToList();

            foreach (var wall in walls)
            {
                if (excludeIds != null && excludeIds.Contains(wall.Id)) continue;
                BoundingBoxXYZ bbox = wall.get_BoundingBox(null);
                if (bbox == null) continue;

                if (minZ.HasValue && bbox.Max.Z < minZ.Value) continue;
                if (maxZ.HasValue && bbox.Min.Z > maxZ.Value) continue;

                var result = ProjectSupportOntoBeam(bbox, lineStart, lineDir, linePerp, lineLength, proximityTol);
                if (result.HasValue)
                {
                    var info = result.Value;
                    info.ElementId = wall.Id;
                    info.Priority = 1; // High priority (Wall)
                    info.SupportDepth = bbox.Max.Z - bbox.Min.Z;
                    supports.Add(info);
                }
            }

            // === Search Primary Beams (Structural Framing) ===
            var primaryBeams = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_StructuralFraming).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().ToList();

            foreach (var pb in primaryBeams)
            {
                if (excludeIds != null && excludeIds.Contains(pb.Id)) continue;
                BoundingBoxXYZ bbox = pb.get_BoundingBox(null);
                if (bbox == null) continue;

                if (minZ.HasValue && bbox.Max.Z < minZ.Value) continue;
                if (maxZ.HasValue && bbox.Min.Z > maxZ.Value) continue;

                var locationCurve = pb.Location as LocationCurve;
                if (locationCurve != null && locationCurve.Curve is Line pbLine)
                {
                    XYZ pbDir = pbLine.Direction.Normalize();
                    if (Math.Abs(pbDir.DotProduct(lineDir.Normalize())) > 0.5) continue;
                }

                double trueHostBotZ = minZ.HasValue ? minZ.Value + 2.0 : bbox.Min.Z;
                double truePbBotZ = bbox.Min.Z;
                double pbWidth = BeamGeometryModule.GetParamValue(pb, "Width", "b");
                if (pbWidth <= 0) pbWidth = Math.Abs((bbox.Max - bbox.Min).DotProduct(lineDir.Normalize()));

                // Hierarchy detection: Deeper or Wider
                bool isDeeper = truePbBotZ <= trueHostBotZ - (50.0 / 304.8);
                bool isWider = pbWidth >= supportWidth + (20.0 / 304.8);

                if (!isDeeper && !isWider) continue; 

                var result = ProjectSupportOntoBeam(bbox, lineStart, lineDir, linePerp, lineLength, proximityTol);
                if (result.HasValue)
                {
                    var info = result.Value;
                    info.ElementId = pb.Id;
                    info.Priority = 2; // Beam priority
                    info.SupportDepth = bbox.Max.Z - bbox.Min.Z;
                    supports.Add(info);
                }
            }

            // === Hierarchy Resolution ===
            // 1. Group by location
            // 2. Priority: Col/Wall > Deeper Beam > Wider Beam
            supports = supports.OrderBy(s => s.CenterOffset)
                               .GroupBy(s => Math.Round(s.CenterOffset, 2))
                               .Select(g => 
                               {
                                   // Within this physical point, find the best support
                                   return g.OrderBy(s => s.Priority) // 1 (Col) is better than 2 (Beam)
                                           .ThenByDescending(s => s.SupportDepth) // Deeper is better
                                           .ThenByDescending(s => s.SupportWidth) // Wider is better
                                           .First();
                               })
                               .ToList();

            // Mark end supports
            double endTol = 100.0 / 304.8;
            for (int i = 0; i < supports.Count; i++)
            {
                var s = supports[i];
                s.IsEndSupport = (s.CenterOffset < endTol) || (s.CenterOffset > lineLength - endTol);
                supports[i] = s;
            }

            return supports;

            return supports;
        }

        /// <summary>
        /// Projects a support's bounding box onto a beam axis. Returns SupportInfo if the
        /// support is close enough laterally and overlaps the beam's length range.
        /// </summary>
        private static SupportInfo? ProjectSupportOntoBeam(
            BoundingBoxXYZ bbox, XYZ beamStart, XYZ beamDir, XYZ beamPerp,
            double beamLength, double proximityTol)
        {
            // Get the 4 XY corners of the bounding box
            XYZ[] corners = {
                new XYZ(bbox.Min.X, bbox.Min.Y, 0),
                new XYZ(bbox.Max.X, bbox.Min.Y, 0),
                new XYZ(bbox.Max.X, bbox.Max.Y, 0),
                new XYZ(bbox.Min.X, bbox.Max.Y, 0)
            };

            XYZ beamStart2D = new XYZ(beamStart.X, beamStart.Y, 0);

            double minAlong = double.MaxValue;
            double maxAlong = double.MinValue;
            double minLateral = double.MaxValue;
            double maxLateral = double.MinValue;

            foreach (var corner in corners)
            {
                XYZ delta = corner - beamStart2D;
                double along = delta.DotProduct(beamDir);
                double lateral = delta.DotProduct(beamPerp);

                if (along < minAlong) minAlong = along;
                if (along > maxAlong) maxAlong = along;
                if (lateral < minLateral) minLateral = lateral;
                if (lateral > maxLateral) maxLateral = lateral;
            }

            // Check lateral proximity: the support must straddle the beam axis
            // i.e., the beam axis must pass through or near the support
            if (minLateral > proximityTol || maxLateral < -proximityTol) return null;

            // Check that the support overlaps the beam's length range
            // Allow some tolerance beyond beam ends for end supports
            double endTol = 500.0 / 304.8; // 500mm beyond beam ends
            if (maxAlong < -endTol || minAlong > beamLength + endTol) return null;

            double center = (minAlong + maxAlong) / 2.0;
            return new SupportInfo
            {
                CenterOffset = center,
                NearFaceOffset = minAlong,
                FarFaceOffset = maxAlong,
                SupportWidth = maxAlong - minAlong,
                IsEndSupport = false // Will be classified later
            };
        }
    }
}
