using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimUnits;
using static antiGGGravity.Commands.General.AutoDimension.AutoDimCore;

namespace antiGGGravity.Commands.General.AutoDimension
{
    /// <summary>
    /// Main workflow orchestrator for the Auto Dimension tool.
    /// Matches the Python main() function step-by-step.
    /// </summary>
    public static class AutoDimOrchestrator
    {
        public static void Run(UIDocument uidoc, AutoDimSettings settings)
        {
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            // Validate view type
            if (view is not ViewPlan)
            {
                TaskDialog.Show("Auto Dims", "Please open a plan view.");
                return;
            }

            // Apply view scale
            try
            {
                settings.ApplyViewScale(view.Scale);
            }
            catch { }

            // Prompt user to pick elements
            var filter = new AutoDimSelectionFilter(settings);
            IList<Element> selectedElements;
            try
            {
                var selRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    filter,
                    "Box-select the area. Only chosen categories will be highlighted.");
                selectedElements = selRefs.Select(r => doc.GetElement(r.ElementId)).ToList();
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return; }
            catch { return; }

            if (selectedElements.Count == 0)
            {
                TaskDialog.Show("Auto Dims", "Nothing selected.");
                return;
            }

            // Collect data
            var allGrids = AutoDimData.CollectGrids(selectedElements, view);
            var allElems = AutoDimData.CollectElements(selectedElements, doc, view, settings);

            var hGrids = allGrids
                .Where(g => g.Orientation == "horizontal")
                .OrderBy(g => g.CoordFt)
                .ToList();
            var vGrids = allGrids
                .Where(g => g.Orientation == "vertical")
                .OrderBy(g => g.CoordFt)
                .ToList();

            if (allElems.Count == 0 && !settings.DimGrids) return;
            if (allGrids.Count == 0) return;

            // Clear grid dim cache
            GridDimensionEngine.ClearCache();

            var tg = new TransactionGroup(doc, "Auto Dims");
            tg.Start();
            int total = 0;
            var failureHandler = new DimFailureSwallower();

            try
            {
                // === Transaction 1: Grid chains ===
                var t1 = new Transaction(doc, "Chains");
                var opts1 = t1.GetFailureHandlingOptions();
                opts1.SetFailuresPreprocessor(failureHandler);
                t1.SetFailureHandlingOptions(opts1);
                t1.Start();

                int nChains = 0;
                if (settings.DimGrids && vGrids.Count >= 2)
                {
                    // Overall (extreme grids)
                    nChains += GridDimensionEngine.MakeGridChain(doc, view,
                        new List<GridInfo> { vGrids[0], vGrids[vGrids.Count - 1] }, "x",
                        settings.OffsetChain1Mm, settings);
                    // Pairwise chain
                    if (vGrids.Count > 2)
                        nChains += GridDimensionEngine.MakeGridChain(doc, view,
                            vGrids, "x",
                            settings.OffsetChain1Mm + settings.OffsetChainGapMm, settings);
                }

                if (settings.DimGrids && hGrids.Count >= 2)
                {
                    nChains += GridDimensionEngine.MakeGridChain(doc, view,
                        new List<GridInfo> { hGrids[0], hGrids[hGrids.Count - 1] }, "y",
                        settings.OffsetChain1Mm, settings);
                    if (hGrids.Count > 2)
                        nChains += GridDimensionEngine.MakeGridChain(doc, view,
                            hGrids, "y",
                            settings.OffsetChain1Mm + settings.OffsetChainGapMm, settings);
                }
                t1.Commit();
                total += nChains;

                // === Transaction 2: Element dimensions ===
                var t2 = new Transaction(doc, "Snaps+Overalls");
                var opts2 = t2.GetFailureHandlingOptions();
                opts2.SetFailuresPreprocessor(failureHandler);
                t2.SetFailureHandlingOptions(opts2);
                t2.Start();

                var occupiedZonesX = new List<(double, double, double, double)>();
                var occupiedZonesY = new List<(double, double, double, double)>();
                var dimKeys = new HashSet<string>();
                var dimsToAdjust = new List<(Dimension Dim, int Side)>();
                int nX = 0, nY = 0;

                var clusters = AutoDimData.ClusterElements(allElems, settings);

                foreach (var cluster in clusters)
                {
                    if (cluster.Count == 1)
                    {
                        // Single element
                        var ei = cluster[0];
                        int sideX = PickSide(ei, "x", hGrids);
                        int sideY = PickSide(ei, "y", vGrids);
                        string dom = ei.Dominant;

                        try
                        {
                            if (dom != "x")
                            {
                                int addedX = ElementDimensionEngine.DimAlongAxis(doc, view, ei, "x",
                                    vGrids, hGrids, allElems, dimsToAdjust, sideX,
                                    occupiedZonesX, dimKeys, settings);
                                if (addedX > 0)
                                {
                                    nX += addedX;
                                    RegisterCrossAxis(new List<ElementInfo> { ei }, "x", occupiedZonesY);
                                }
                            }
                        }
                        catch { }

                        try
                        {
                            if (dom != "y")
                            {
                                int addedY = ElementDimensionEngine.DimAlongAxis(doc, view, ei, "y",
                                    hGrids, vGrids, allElems, dimsToAdjust, sideY,
                                    occupiedZonesY, dimKeys, settings);
                                if (addedY > 0)
                                {
                                    nY += addedY;
                                    RegisterCrossAxis(new List<ElementInfo> { ei }, "y", occupiedZonesX);
                                }
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        // Multi-element cluster
                        try
                        {
                            int addedX = ElementDimensionEngine.DimClusterAlongAxis(doc, view,
                                cluster, "x", vGrids, hGrids, dimsToAdjust,
                                occupiedZonesX, dimKeys, settings);
                            if (addedX > 0)
                            {
                                nX += addedX;
                                RegisterCrossAxis(cluster, "x", occupiedZonesY);
                            }
                        }
                        catch { }

                        try
                        {
                            int addedY = ElementDimensionEngine.DimClusterAlongAxis(doc, view,
                                cluster, "y", hGrids, vGrids, dimsToAdjust,
                                occupiedZonesY, dimKeys, settings);
                            if (addedY > 0)
                            {
                                nY += addedY;
                                RegisterCrossAxis(cluster, "y", occupiedZonesX);
                            }
                        }
                        catch { }
                    }
                }

                // Regenerate before adjusting text positions
                doc.Regenerate();

                foreach (var (d, s) in dimsToAdjust)
                    DisplaceSmallTexts(d, view, s);

                t2.Commit();
                total += nX + nY;

                if (t2.GetStatus() == TransactionStatus.Started)
                    t2.Commit();

                if (tg.GetStatus() == TransactionStatus.Started)
                    tg.Assimilate();

                int totalCreated = nChains + nX + nY;
                TaskDialog.Show("Auto Dims", $"Done! Created {totalCreated} dimensions.");
            }
            catch (Exception ex)
            {
                try { tg.RollBack(); } catch { }
                TaskDialog.Show("Auto Dims", $"Error: {ex.Message}");
            }
        }
    }
}
