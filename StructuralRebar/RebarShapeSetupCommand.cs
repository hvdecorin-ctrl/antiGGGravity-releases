using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using antiGGGravity.Commands;
using antiGGGravity.StructuralRebar.Core.Creation;

namespace antiGGGravity.StructuralRebar
{
    /// <summary>
    /// Loads all pre-defined Rebar Shape families from the RebarShapes folder
    /// into the current project. Detects standard mismatches and reports fallback aliases.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class RebarShapeSetupCommand : BaseCommand
    {
        // All canonical shape names our Rebar Suite tools may request
        private static readonly string[] RequiredShapes = new[]
        {
            "Shape 00", "Shape 0x0",
            "Shape 90x0", "Shape 0x90",
            "Shape 135x0", "Shape 0x135",
            "Shape 180x0", "Shape 0x180",
            "Shape 90x90", "Shape 135x135", "Shape 180x180",
            "Shape L", "Shape LL",
            "Shape HT", "Shape CT", "Shape SP",
            "Shape 00_Crk", "Shape 0x0_Crk",
            "Shape 90x0_Crk", "Shape 90x90_Crk",
            "Shape U_135 Hook", "Shape U_180 Hook",
        };

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            // ── Detect Reinforcement Settings hook inclusion flag ─────────────────
            bool hooksInShapeDefinition = false;
            try
            {
                var reinfSettings = ReinforcementSettings.GetReinforcementSettings(doc);
                hooksInShapeDefinition = reinfSettings.RebarShapeDefinesHooks;
            }
            catch { }

            // ── Find RebarShapes folder ──────────────────────────────────────────
            string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string baseFolder = Path.Combine(dllDir, "RebarShapes");

            if (!Directory.Exists(baseFolder))
            {
                TaskDialog.Show("Setup Shapes", $"⚠ RebarShapes folder not found.\nExpected: {baseFolder}");
                return Result.Failed;
            }

            // Version-specific subfolder (e.g. R26, R25), fallback to base
            string revitVersion = commandData.Application.Application.VersionNumber;
            string versionKey = "R" + revitVersion.Substring(revitVersion.Length - 2);
            string versionFolder = Path.Combine(baseFolder, versionKey);
            string shapesFolder = Directory.Exists(versionFolder) ? versionFolder : baseFolder;

            string[] rfaFiles = Directory.GetFiles(shapesFolder, "*.rfa");
            if (rfaFiles.Length == 0)
            {
                TaskDialog.Show("Setup Shapes", $"⚠ No .rfa files found in:\n{shapesFolder}");
                return Result.Failed;
            }

            // ── Warn if hooks setting is ON (shapes may fail to load) ────────────
            if (hooksInShapeDefinition)
            {
                var warn = new TaskDialog("Setup Shapes — Warning")
                {
                    MainInstruction = "Reinforcement Settings mismatch detected",
                    MainContent =
                        "Your project has \"Include hooks in Rebar Shape definition\" turned ON.\n\n" +
                        "The provided shape families were built with this setting OFF. " +
                        "Shapes like CT, SP, L, LL may fail to load.\n\n" +
                        "To fix: Go to  Structure → Reinforcement Settings → General  and turn this OFF.\n" +
                        "⚠ This can only be changed before any rebar is placed in the project.\n\n" +
                        "Do you want to continue loading anyway?",
                    CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No
                };
                if (warn.Show() == TaskDialogResult.No)
                    return Result.Cancelled;
            }

            // ── Get existing shapes for skip-check & alias reporting ─────────────
            var existingShapes = new FilteredElementCollector(doc)
                .OfClass(typeof(RebarShape))
                .Cast<RebarShape>()
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int loaded = 0;
            int skipped = 0;
            var failed = new List<string>();
            var errors = new List<string>();

            // ── Load families ────────────────────────────────────────────────────
            using (Transaction t = new Transaction(doc, "Setup Rebar Shapes"))
            {
                t.Start();

                foreach (string rfaPath in rfaFiles.OrderBy(f => f))
                {
                    string familyName = Path.GetFileNameWithoutExtension(rfaPath);

                    if (existingShapes.Contains(familyName))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        bool success = doc.LoadFamily(rfaPath, out Family _);
                        if (success) { loaded++; existingShapes.Add(familyName); }
                        else { skipped++; }
                    }
                    catch (Exception ex)
                    {
                        failed.Add(familyName);
                        errors.Add($"  • {familyName}: {ex.Message}");
                    }
                }

                t.Commit();
            }

            // ── Build alias fallback report for failed shapes ─────────────────────
            // Re-query project shapes after loading
            var shapeService = new StandardShapeService(doc);
            var aliasReport = new List<string>();

            foreach (string canonical in RequiredShapes)
            {
                // Skip shapes that loaded fine
                if (existingShapes.Contains(canonical)) continue;

                string foundAlias = shapeService.FindAliasInProject(canonical);
                if (foundAlias != null)
                    aliasReport.Add($"  ✔ {canonical}  →  using \"{foundAlias}\" (found in project)");
                else
                    aliasReport.Add($"  ✗ {canonical}  →  no equivalent found — some rebar may not be created");
            }

            // ── Final report ──────────────────────────────────────────────────────
            string hookStatus = hooksInShapeDefinition
                ? "⚠ Hooks in Shape Definition: ON (shapes may be incompatible)"
                : "✔ Hooks in Shape Definition: OFF (compatible)";

            string report =
                $"Setup Rebar Shapes — {versionKey}\n" +
                $"{'─',50}\n" +
                $"Loaded:         {loaded}\n" +
                $"Already exist:  {skipped}\n" +
                $"Failed:         {failed.Count}\n" +
                $"{'─',50}\n" +
                hookStatus;

            if (errors.Count > 0)
            {
                report += "\n\nFailed to load:\n" + string.Join("\n", errors);
            }

            if (aliasReport.Count > 0)
            {
                report += "\n\nFallback matching (US / AS / BS aliases):";
                report += "\n" + string.Join("\n", aliasReport);
            }

            if (failed.Count == 0 && aliasReport.All(r => r.StartsWith("  ✔")))
            {
                report = $"✅ All rebar shapes are ready for {versionKey}!\n\n" + report;
            }
            else if (aliasReport.Any(r => r.StartsWith("  ✗")))
            {
                report = $"⚠ Some required shapes are missing. Check details below.\n\n" + report;
            }

            TaskDialog.Show("Setup Shapes", report);
            return Result.Succeeded;
        }
    }
}
