using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Creation;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Core.Layout;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Utilities;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    public class BoredPileEngine : IRebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;

        public BoredPileEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
        }

        public bool Execute(Element host, RebarRequest request)
        {
            if (!(host is FamilyInstance foundation)) return false;
            
            HostGeometry? bpHostOpt = BoredPileGeometryModule.Read(_doc, foundation);
            if (!bpHostOpt.HasValue) return false;
            HostGeometry bpHost = bpHostOpt.Value;

            double? overrideZEnd = null;
            if (request.PileMainExtensionMode == "Auto")
            {
                var hostAbove = FindHostAbove(foundation, bpHost.Origin, bpHost.SolidZMax);
                if (hostAbove != null)
                {
                    double zMax = 0;
                    double cTop = GeometryUtils.GetCoverDistance(_doc, hostAbove, BuiltInParameter.CLEAR_COVER_TOP);
                    
                    var bb = hostAbove.get_BoundingBox(null);
                    if (bb != null)
                    {
                        zMax = bb.Max.Z;
                        overrideZEnd = zMax - cTop - UnitConversion.MmToFeet(100);
                    }
                }
            }
            else if (request.PileMainExtensionMode == "Manual" && request.PileMainExtensionVal > 0)
            {
                overrideZEnd = bpHost.SolidZMax + request.PileMainExtensionVal;
            }

            var definitions = BoredPileLayoutGenerator.Generate(bpHost, request, overrideZEnd);
            var ids = _creationService.PlaceRebar(foundation, definitions);
            bool success = ids.Count > 0;

            // Transverse Reinforcement for Bored Piles
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                double hostRadius = 0;
                if (bpHost.BoundaryCurves.Count > 0 && bpHost.BoundaryCurves.Any(c => c is Arc))
                {
                    var arcs = bpHost.BoundaryCurves.OfType<Arc>().ToList();
                    hostRadius = arcs.Max(a => a.Radius);
                }
                else
                {
                    hostRadius = Math.Min(bpHost.Width, bpHost.Length) / 2.0;
                }

                if (hostRadius > 0)
                {
                    double rebarRadius = hostRadius - bpHost.CoverExterior;
                    if (rebarRadius <= 0) rebarRadius = hostRadius * 0.8;
                    XYZ center = bpHost.Origin;
                    
                    double zStart = bpHost.SolidZMin + bpHost.CoverBottom + UnitConversion.MmToFeet(50);
                    double zEnd = bpHost.SolidZMax - bpHost.CoverTop - UnitConversion.MmToFeet(50);

                    bool hasHostAboveForTrans = FindHostAbove(foundation, bpHost.Origin, bpHost.SolidZMax) != null;
                    if (hasHostAboveForTrans && request.PileTransverseExtensionVal > 0)
                    {
                        zEnd = bpHost.SolidZMax + request.PileTransverseExtensionVal;
                    }
                    else 
                    {
                        zEnd = bpHost.SolidZMax - bpHost.CoverTop - UnitConversion.MmToFeet(50);
                    }

                    double dist = zEnd - zStart;

                    RebarBarType tieBarType = new FilteredElementCollector(_doc)
                        .OfClass(typeof(RebarBarType))
                        .Cast<RebarBarType>()
                        .FirstOrDefault(t => t.Name.Equals(request.TransverseBarTypeName, StringComparison.OrdinalIgnoreCase));

                    RebarShape tieShape = new FilteredElementCollector(_doc)
                        .OfClass(typeof(RebarShape))
                        .Cast<RebarShape>()
                        .FirstOrDefault(s => s.Name == (request.EnableSpiral ? "SP" : "CT") || s.Name == (request.EnableSpiral ? "Shape SP" : "Shape CT"));

                    if (tieBarType != null && dist > 0)
                    {
                        try 
                        {
                            if (request.EnableSpiral)
                            {
                                var tie = CircularRebarService.CreateSpiralFromRing(_doc, foundation, center, rebarRadius, zStart, zEnd, tieBarType, request.TransverseSpacing, tieShape);
                                if (tie != null) success = true;
                            }
                            else
                            {
                                var tie = CircularRebarService.CreateCircularTie(_doc, foundation, center, rebarRadius, zStart, tieBarType, tieShape);
                                if (tie != null)
                                {
                                    var accessor = tie.GetShapeDrivenAccessor();
                                    accessor.SetLayoutAsMaximumSpacing(request.TransverseSpacing, dist, true, true, true);
                                    success = true;
                                }
                            }
                        } catch { }
                    }
                }
            }

            return success;
        }

        private Element FindHostAbove(Element element, XYZ origin, double minZ)
        {
            double searchRadius = UnitConversion.MmToFeet(100);
            XYZ searchMin = new XYZ(origin.X - searchRadius, origin.Y - searchRadius, minZ + 0.01);
            XYZ searchMax = new XYZ(origin.X + searchRadius, origin.Y + searchRadius, minZ + UnitConversion.MmToFeet(2000));
            
            Outline searchOutline = new Outline(searchMin, searchMax);
            
            var collector = new FilteredElementCollector(_doc);
            var categories = new List<BuiltInCategory> 
            { 
                BuiltInCategory.OST_StructuralFoundation, 
                BuiltInCategory.OST_Floors 
            };
            
            var aboveElements = collector
                .WherePasses(new ElementMulticategoryFilter(categories))
                .WherePasses(new BoundingBoxIntersectsFilter(searchOutline))
                .WhereElementIsNotElementType()
                .ToList();
                
            Element bestHost = null;
            double lowestZ = double.MaxValue;
            
            foreach (var e in aboveElements)
            {
                if (e.Id == element.Id) continue;
                
                var bb = e.get_BoundingBox(null);
                if (bb != null && bb.Min.Z >= minZ - 0.5)
                {
                    if (bb.Min.Z < lowestZ)
                    {
                        lowestZ = bb.Min.Z;
                        bestHost = e;
                    }
                }
            }
            return bestHost;
        }
    }
}
