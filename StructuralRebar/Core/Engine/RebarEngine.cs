using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
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
    /// <summary>
    /// Main orchestrator for rebar generation.
    /// Delegates specific element types to their respective engines.
    /// </summary>
    public class RebarEngine
    {
        private readonly Document _doc;
        private readonly RebarCreationService _creationService;

        public RebarEngine(Document doc)
        {
            _doc = doc;
            _creationService = new RebarCreationService(doc);
        }

        public (int Processed, int Total) GenerateBeamRebar(
            List<FamilyInstance> beams, RebarRequest request)
        {
            return new BeamEngine(_doc).GenerateBeamRebar(beams, request);
        }

        public (int Processed, int Total) GenerateContinuousBeamRebar(
            List<FamilyInstance> spans, RebarRequest request)
        {
            return new BeamEngine(_doc).GenerateContinuousBeamRebar(spans, request);
        }

        public (int Processed, int Total) GenerateWallRebar(List<Wall> walls, RebarRequest request)
        {
            return new WallEngine(_doc).GenerateWallRebar(walls, request);
        }

        public (int Processed, int Total) GenerateWallStackRebar(List<Wall> stack, RebarRequest request)
        {
            return new WallEngine(_doc).GenerateWallStackRebar(stack, request);
        }

        public (int Processed, int Total) GenerateColumnRebar(
            List<FamilyInstance> columns, RebarRequest request)
        {
            return new ColumnEngine(_doc).GenerateColumnRebar(columns, request);
        }

        public (int Processed, int Total) GenerateColumnStackRebar(
            List<FamilyInstance> stack, RebarRequest request)
        {
            return new ColumnEngine(_doc).GenerateColumnStackRebar(stack, request);
        }

        public (int Processed, int Total) GenerateStripFootingRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Strip Footing Rebar");
        }

        public (int Processed, int Total) GenerateFootingPadRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Footing Pad Rebar");
        }

        public (int Processed, int Total) GeneratePadShapeRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Pad Shape Rebar");
        }

        public (int Processed, int Total) GenerateBoredPileRebar(
            List<Element> foundations, RebarRequest request)
        {
            return GenerateRebarInternal(foundations, request, "Generate Bored Pile Rebar");
        }

        public (int Processed, int Total) GenerateWallCornerRebar(List<Wall> walls, RebarRequest request)
        {
            return new WallEngine(_doc).GenerateWallCornerRebar(walls, request);
        }

        private (int Processed, int Total) GenerateRebarInternal<T>(
            List<T> elements, RebarRequest request, string transactionName) where T : Element
        {
            int processed = 0;

            using (Transaction t = new Transaction(_doc, transactionName))
            {
                t.Start();

                foreach (var element in elements)
                {
                    try
                    {
                        if (request.RemoveExisting)
                            _creationService.DeleteExistingRebar(element);

                        bool success = false;
                        if (element is FamilyInstance fi && request.HostType == ElementHostType.Beam)
                            success = new BeamEngine(_doc).Execute(fi, request);
                        else if (element is Wall wall && request.HostType == ElementHostType.Wall)
                            success = new WallEngine(_doc).Execute(wall, request);
                        else if (element is FamilyInstance col && request.HostType == ElementHostType.Column)
                            success = new ColumnEngine(_doc).Execute(col, request);
                        else if (element.Category?.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.StripFooting)
                            success = new FootingEngine(_doc).Execute(element, request);
                        else if (element.Category?.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.FootingPad)
                            success = new FootingEngine(_doc).Execute(element, request);
                        else if (element.Category?.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.PadShape)
                            success = new PadShapeEngine(_doc).Execute(element, request);
                        else if (element.Category?.Id.GetIdValue() == (long)BuiltInCategory.OST_StructuralFoundation && request.HostType == ElementHostType.BoredPile)
                            success = new BoredPileEngine(_doc).Execute(element, request);

                        if (success) processed++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"RebarEngine: {element.Id} failed: {ex.Message}");
                    }
                }

                t.Commit();
            }

            return (processed, elements.Count);
        }
    }
}
