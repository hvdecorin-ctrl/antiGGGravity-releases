using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Calculators;
using antiGGGravity.StructuralRebar.Core.Creation;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Core.Layout;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    public class FootingEngine : IRebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;

        public FootingEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
        }

        public bool Execute(Element host, RebarRequest request)
        {
            if (request.HostType == ElementHostType.StripFooting)
                return ProcessStripFooting(host, request);
            else if (request.HostType == ElementHostType.FootingPad)
                return ProcessFootingPad(host, request);
            return false;
        }

        private bool ProcessStripFooting(Element foundation, RebarRequest request)
        {
            HostGeometry? hostOpt = StripFootingGeometryModule.Read(_doc, foundation);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            var definitions = new List<RebarDefinition>();

            // 1. Stirrups (Transverse)
            double transDia = 0;
            if (!string.IsNullOrEmpty(request.TransverseBarTypeName))
            {
                transDia = GetBarDiameter(request.TransverseBarTypeName);
                var stirrupDef = StripFootingLayoutGenerator.CreateStirrup(
                    host, request.TransverseBarTypeName, transDia,
                    request.TransverseSpacing, request.TransverseStartOffset,
                    request.TransverseHookStartName, request.TransverseHookEndName);

                if (stirrupDef != null) definitions.Add(stirrupDef);
            }

            // 2. Longitudinal Layers
            // Use cover for offsets by default to keep rebar within the foundation
            double startOff = host.CoverOther;
            double endOff = host.CoverOther;

            foreach (var layer in request.Layers)
            {
                layer.BarDiameter_Backing = GetBarDiameter(layer.VerticalBarTypeName);
                if (layer.BarDiameter_Backing > 0)
                {
                    double barLen = host.Length - 2 * host.CoverExterior;
                    
                    var longDef = StripFootingLayoutGenerator.CreateLongitudinalLayer(
                        host, layer, transDia, startOff, endOff);

                    if (longDef != null)
                    {
                        if (request.EnableLapSplice && barLen > LapSpliceCalculator.MaxStockLengthFt)
                        {
                            var segments = LapSpliceCalculator.SplitBarForLap(
                                barLen, longDef.BarDiameter, request.DesignCode, 0, LapSpliceCalculator.GetCrankRun(longDef.BarDiameter));

                            if (segments.Count <= 1)
                            {
                                definitions.Add(longDef);
                            }
                            else
                            {
                                var origLine = longDef.Curves[0] as Line;
                                XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                                XYZ barStart = origLine.GetEndPoint(0);

                                for (int si = 0; si < segments.Count; si++)
                                {
                                    var seg = segments[si];
                                    XYZ segStart = barStart + barDir * seg.Start;
                                    XYZ segEnd = barStart + barDir * seg.End;

                                    var curves = new List<Curve>();
                                    if (si > 0)
                                    {
                                        double crankOff = LapSpliceCalculator.GetCrankOffset(longDef.BarDiameter);
                                        double crankRun = LapSpliceCalculator.GetCrankRun(longDef.BarDiameter);
                                        double lapLen = LapSpliceCalculator.CalculateTensionLapLength(longDef.BarDiameter, request.DesignCode);
                                        double straightLap = lapLen + crankRun;

                                        XYZ crankDir = (layer.Side == RebarSide.Top) ? -host.HAxis : host.HAxis;

                                        XYZ ptA = segStart + crankDir * crankOff;
                                        XYZ ptB = ptA + barDir * straightLap;
                                        XYZ ptC = segStart + barDir * (straightLap + crankRun);

                                        curves.Add(Line.CreateBound(ptA, ptB));
                                        curves.Add(Line.CreateBound(ptB, ptC));
                                        curves.Add(Line.CreateBound(ptC, segEnd));
                                    }
                                    else
                                    {
                                        curves.Add(Line.CreateBound(segStart, segEnd));
                                    }

                                    definitions.Add(new RebarDefinition
                                    {
                                        Curves = curves,
                                        Style = longDef.Style,
                                        BarTypeName = longDef.BarTypeName,
                                        BarDiameter = longDef.BarDiameter,
                                        FixedCount = longDef.FixedCount,
                                        DistributionWidth = longDef.DistributionWidth,
                                        ArrayDirection = longDef.ArrayDirection,
                                        Normal = longDef.Normal,
                                        HookStartName = (si == 0) ? longDef.HookStartName : null,
                                        HookEndName = (si == segments.Count - 1) ? longDef.HookEndName : null,
                                        HookStartOrientation = longDef.HookStartOrientation,
                                        HookEndOrientation = longDef.HookEndOrientation,
                                        OverrideHookLength = longDef.OverrideHookLength,
                                        HookLengthOverride = longDef.HookLengthOverride,
                                        ShapeNameHint = longDef.ShapeNameHint,
                                        Comment = longDef.Comment,
                                        Label = $"Footing Long. {layer.Side} (lapped)"
                                    });
                                }
                            }
                        }
                        else
                        {
                            definitions.Add(longDef);
                        }
                    }
                }
            }

            // 3. Side Bars
            if (request.EnableSideRebar && request.SideRebarRows > 0 && !string.IsNullOrEmpty(request.SideRebarTypeName))
            {
                double sideDia = GetBarDiameter(request.SideRebarTypeName);
                var sideDefs = StripFootingLayoutGenerator.CreateSideRebars(
                    host, request.SideRebarTypeName, sideDia, request.SideRebarRows, transDia);
                
                double sideBarLen = host.Length - 2 * host.CoverExterior;
                if (request.EnableLapSplice && sideBarLen > LapSpliceCalculator.MaxStockLengthFt)
                {
                    foreach (var sDef in sideDefs)
                    {
                        var segments = LapSpliceCalculator.SplitBarForLap(
                            sideBarLen, sDef.BarDiameter, request.DesignCode, 0, 0);

                        if (segments.Count <= 1)
                        {
                            definitions.Add(sDef);
                        }
                        else
                        {
                            var origLine = sDef.Curves[0] as Line;
                            XYZ barDir = (origLine.GetEndPoint(1) - origLine.GetEndPoint(0)).Normalize();
                            XYZ barStart = origLine.GetEndPoint(0);

                            for (int si = 0; si < segments.Count; si++)
                            {
                                var seg = segments[si];
                                XYZ segStart = barStart + barDir * seg.Start;
                                XYZ segEnd = barStart + barDir * seg.End;

                                var curves = new List<Curve> { Line.CreateBound(segStart, segEnd) };

                                definitions.Add(new RebarDefinition
                                {
                                    Curves = curves,
                                    Style = sDef.Style,
                                    BarTypeName = sDef.BarTypeName,
                                    BarDiameter = sDef.BarDiameter,
                                    FixedCount = sDef.FixedCount,
                                    DistributionWidth = sDef.DistributionWidth,
                                    ArrayDirection = sDef.ArrayDirection,
                                    Normal = sDef.Normal,
                                    ShapeNameHint = sDef.ShapeNameHint,
                                    Comment = sDef.Comment,
                                    Label = "Side Rebar (lapped)"
                                });
                            }
                        }
                    }
                }
                else
                {
                    definitions.AddRange(sideDefs);
                }
            }

            var ids = _creationService.PlaceRebar(foundation, definitions);
            return ids.Count > 0;
        }

        private bool ProcessFootingPad(Element foundation, RebarRequest request)
        {
            HostGeometry? hostOpt = FootingPadGeometryModule.Read(_doc, foundation);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            var definitions = new List<RebarDefinition>();

            foreach (var layer in request.Layers)
            {
                layer.BarDiameter_Backing = GetBarDiameter(layer.VerticalBarTypeName);
                if (layer.BarDiameter_Backing > 0)
                {
                    bool isTop = (layer.Side == RebarSide.Top);
                    var matDefs = FootingPadLayoutGenerator.CreateMat(host, layer, isTop);
                    if (matDefs != null) definitions.AddRange(matDefs);
                }
            }

            if (request.EnableSideRebar && !string.IsNullOrEmpty(request.SideRebarTypeName))
            {
                double sideDia = GetBarDiameter(request.SideRebarTypeName);
                if (sideDia > 0)
                {
                    double mainBarDia = request.Layers.Select(l => GetBarDiameter(l.VerticalBarTypeName)).DefaultIfEmpty(0).Max();

                    var sideDefs = FootingPadLayoutGenerator.CreateSideRebars(
                        host,
                        request.SideRebarTypeName,
                        sideDia,
                        request.SideRebarSpacing,
                        request.EnableSideRebarOverrideLeg,
                        request.SideRebarLegLength,
                        mainBarDia);

                    if (sideDefs != null) definitions.AddRange(sideDefs);
                }
            }

            var ids = _creationService.PlaceRebar(foundation, definitions);
            return ids.Count > 0;
        }

        private double GetBarDiameter(string barTypeName)
        {
            if (string.IsNullOrEmpty(barTypeName)) return 0;

            var barType = new FilteredElementCollector(_doc)
                .OfClass(typeof(Autodesk.Revit.DB.Structure.RebarBarType))
                .Cast<Autodesk.Revit.DB.Structure.RebarBarType>()
                .FirstOrDefault(t => string.Equals(t.Name, barTypeName, StringComparison.OrdinalIgnoreCase));

            return barType?.BarModelDiameter ?? 0;
        }
    }
}
