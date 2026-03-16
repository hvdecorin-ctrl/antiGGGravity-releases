using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.Core.Creation;
using antiGGGravity.StructuralRebar.Core.Geometry;
using antiGGGravity.StructuralRebar.Core.Layout;
using antiGGGravity.StructuralRebar.DTO;
using System;
using System.Collections.Generic;
using System.Linq;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    public class PadShapeEngine : IRebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;

        public PadShapeEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
        }

        public bool Execute(Element host, RebarRequest request)
        {
            return ProcessPadShape(host, request);
        }

        private bool ProcessPadShape(Element foundation, RebarRequest request)
        {
            HostGeometry? hostOpt = PadShapeGeometryModule.Read(_doc, foundation);
            if (!hostOpt.HasValue) return false;
            HostGeometry host = hostOpt.Value;

            var definitions = new List<RebarDefinition>();

            foreach (var layer in request.Layers)
            {
                layer.BarDiameter_Backing = GetBarDiameter(layer.VerticalBarTypeName);
                if (layer.BarDiameter_Backing > 0)
                {
                    bool isTop = (layer.Side == RebarSide.Top);
                    var matDefs = PadShapeLayoutGenerator.CreateMat(host, layer, isTop);
                    if (matDefs != null) definitions.AddRange(matDefs);
                }
            }

            if (request.EnableSideRebar && !string.IsNullOrEmpty(request.SideRebarTypeName))
            {
                double sideDia = GetBarDiameter(request.SideRebarTypeName);
                if (sideDia > 0)
                {
                    double mainBarDia = request.Layers.Select(l => GetBarDiameter(l.VerticalBarTypeName)).DefaultIfEmpty(0).Max();

                    var sideDefs = PadShapeLayoutGenerator.CreateSideRebars(
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
