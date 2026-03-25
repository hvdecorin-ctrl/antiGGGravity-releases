using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace antiGGGravity.Commands.ProjectAudit
{
    [Transaction(TransactionMode.Manual)]
    public class OpenProjectFolderCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            try
            {
                string path = doc.PathName;

                // For workshared projects (not in cloud), prioritize the Central Model path
                if (doc.IsWorkshared && !doc.IsModelInCloud)
                {
                    ModelPath centralPath = doc.GetWorksharingCentralModelPath();
                    if (centralPath != null)
                    {
                        string userPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(centralPath);
                        // Only switch to central path if it actually looks like a local/network file path
                        if (!string.IsNullOrEmpty(userPath) && (userPath.Contains("\\") || userPath.Contains("/")))
                        {
                            path = userPath;
                        }
                    }
                }

                if (string.IsNullOrEmpty(path))
                {
                    TaskDialog.Show("Project Folder", "Document has not been saved yet.");
                    return Result.Failed;
                }

                // If path is a Cloud URN or similar, Directory.Exists will fail.
                // We'll try to find the directory, or fall back to doc.PathName if central failed.
                string directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    // Use "/select" to open the folder AND highlight the file
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                    return Result.Succeeded;
                }
                
                // Fallback for Cloud Models or disconnected centrals: try the local path
                if (path != doc.PathName && !string.IsNullOrEmpty(doc.PathName))
                {
                    string localDir = Path.GetDirectoryName(doc.PathName);
                    if (!string.IsNullOrEmpty(localDir) && Directory.Exists(localDir))
                    {
                        Process.Start("explorer.exe", $"/select,\"{doc.PathName}\"");
                        return Result.Succeeded;
                    }
                }

                TaskDialog.Show("Project Folder", "Could not find a valid directory on disk for this model.\n\nPath: " + path);
                return Result.Failed;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    public abstract class ViewportTitleBase : IExternalCommand
    {
        protected static readonly Regex DecimalPattern = new Regex(@"^\d+\.\d+[a-zA-Z]?$", RegexOptions.Compiled);
        protected static readonly Dictionary<ViewType, string> ViewTypeMap = new Dictionary<ViewType, string>
        {
            { ViewType.Section, "SECTION" },
            { ViewType.Detail, "DETAIL" },
            { ViewType.DraftingView, "DETAIL" }
        };

        public abstract Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements);

        protected void ProcessViewport(Document doc, Viewport viewport, ref int renamedCount)
        {
            View view = doc.GetElement(viewport.ViewId) as View;
            if (view == null || !ViewTypeMap.ContainsKey(view.ViewType)) return;

            Parameter param = view.LookupParameter("Title on Sheet");
            if (param == null || param.IsReadOnly) return;

            string currentTitle = param.HasValue ? param.AsString() : "";
            string desiredTitle = ViewTypeMap[view.ViewType];

            if (currentTitle == desiredTitle) return;

            bool isShort = currentTitle.Length < 4;
            bool matchesPattern = DecimalPattern.IsMatch(currentTitle);

            if (isShort || matchesPattern)
            {
                param.Set(desiredTitle);
                renamedCount++;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class TitleOnSheetsCommand : ViewportTitleBase
    {
        public override Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            try
            {
                int renamedCount = 0;
                var viewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>();

                using (Transaction t = new Transaction(doc, "Set Viewport Titles (Project Wide)"))
                {
                    t.Start();
                    foreach (Viewport vp in viewports)
                    {
                        ProcessViewport(doc, vp, ref renamedCount);
                    }
                    t.Commit();
                }
                TaskDialog.Show("Titles Updated", $"Successfully updated {renamedCount} viewport titles.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class WipeEmptyTagsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;

            try
            {
                var tags = new FilteredElementCollector(doc)
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .Where(tag => string.IsNullOrWhiteSpace(tag.TagText))
                    .Select(tag => tag.Id)
                    .ToList();

                if (!tags.Any())
                {
                    TaskDialog.Show("Wipe Empty Tags", "No empty tags found.");
                    return Result.Succeeded;
                }

                using (Transaction t = new Transaction(doc, "Wipe Empty Tags"))
                {
                    t.Start();
                    doc.Delete(tags);
                    t.Commit();
                }

                TaskDialog.Show("Wipe Empty Tags", $"Removed {tags.Count} empty tags.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }

}
