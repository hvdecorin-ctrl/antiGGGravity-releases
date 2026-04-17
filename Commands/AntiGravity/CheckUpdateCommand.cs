using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Threading.Tasks;

using antiGGGravity.Commands;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.AntiGravity
{
    /// <summary>
    /// Revit command that checks GitHub for a newer version of antiGGGravity.
    /// If an update is available, prompts the user to download it.
    /// The update is staged and applied on the next Revit restart.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CheckUpdateCommand : BaseCommand
    {
        // Update checking must always be free
        protected override bool RequiresLicense => false;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Check if there's already a staged update waiting
            var stagedVersion = AutoUpdater.GetStagedVersion();
            if (stagedVersion != null)
            {
                TaskDialog.Show(
                    antiGGGravity.Resources.Branding.COMPANY_NAME,
                    $"✅ Update v{stagedVersion} is already downloaded.\n\n" +
                    $"Restart Revit to apply the update.");
                return Result.Succeeded;
            }

            // Show "checking..." feedback
            var checkingDialog = new TaskDialog(antiGGGravity.Resources.Branding.COMPANY_NAME)
            {
                MainIcon = TaskDialogIcon.TaskDialogIconNone,
                MainInstruction = "Checking for updates...",
                MainContent = $"Current version: v{antiGGGravity.Resources.Branding.VERSION}"
            };

            // Run the async check synchronously (Revit commands must be synchronous)
            UpdateInfo info;
            try
            {
                info = Task.Run(() => AutoUpdater.CheckForUpdateAsync()).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                    $"❌ Failed to check for updates.\n\n{ex.Message}");
                return Result.Failed;
            }

            if (info == null || info.HasError)
            {
                TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                    $"❌ {info?.ErrorMessage ?? "Unknown error checking for updates."}");
                return Result.Failed;
            }

            // No update available
            if (!info.IsUpdateAvailable)
            {
                TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                    $"✅ You are running the latest version.\n\n" +
                    $"Current version: v{info.CurrentVersion}\n" +
                    $"Latest release:  v{info.LatestVersion}");
                return Result.Succeeded;
            }

            // Update available — ask user to download
            var updateDialog = new TaskDialog(antiGGGravity.Resources.Branding.COMPANY_NAME)
            {
                MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                MainInstruction = $"Update Available: v{info.LatestVersion}",
                MainContent =
                    $"Current version: v{info.CurrentVersion}\n" +
                    $"New version: v{info.LatestVersion}" +
                    (info.DownloadSizeMB > 0 ? $" ({info.DownloadSizeMB:F1} MB)" : "") +
                    (!string.IsNullOrEmpty(info.ReleaseNotes)
                        ? $"\n\n📋 Release Notes:\n{TruncateNotes(info.ReleaseNotes, 500)}"
                        : ""),
                CommonButtons = TaskDialogCommonButtons.Close
            };
            updateDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                "Download Update",
                "Download and stage the update. Restart Revit to apply.");

            var dialogResult = updateDialog.Show();

            if (dialogResult != TaskDialogResult.CommandLink1)
                return Result.Succeeded;

            // Download the update
            if (string.IsNullOrEmpty(info.DownloadUrl))
            {
                TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                    "❌ No downloadable zip file found in this release.\n\n" +
                    "Please ensure the GitHub Release has a .zip asset attached.");
                return Result.Failed;
            }

            // Detect current Revit version year
            string revitVersion;
            try
            {
                revitVersion = commandData.Application.Application.VersionNumber;
            }
            catch
            {
                revitVersion = "2026"; // Fallback
            }

            // Download synchronously with a simple progress dialog
            DownloadResult downloadResult;
            try
            {
                downloadResult = Task.Run(() => AutoUpdater.DownloadUpdateAsync(
                    info.DownloadUrl, info.LatestVersion, revitVersion))
                    .GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                    $"❌ Download failed.\n\n{ex.Message}");
                return Result.Failed;
            }

            if (!downloadResult.Success)
            {
                TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                    $"❌ Download failed.\n\n{downloadResult.ErrorMessage}");
                return Result.Failed;
            }

            // Success!
            TaskDialog.Show(antiGGGravity.Resources.Branding.COMPANY_NAME,
                $"✅ Update v{info.LatestVersion} downloaded successfully!\n\n" +
                $"🔄 Restart Revit to apply the update.");

            return Result.Succeeded;
        }

        /// <summary>
        /// Truncates release notes to a maximum length for dialog display.
        /// </summary>
        private string TruncateNotes(string notes, int maxLength)
        {
            if (string.IsNullOrEmpty(notes)) return "";
            // Clean up markdown formatting for display in TaskDialog
            notes = notes.Replace("##", "").Replace("**", "").Replace("- ", "• ");
            return notes.Length <= maxLength ? notes : notes.Substring(0, maxLength) + "...";
        }
    }
}
