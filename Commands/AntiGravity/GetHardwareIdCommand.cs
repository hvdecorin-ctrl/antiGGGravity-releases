using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Windows;

using antiGGGravity.Commands;
using antiGGGravity.Utilities;

namespace antiGGGravity.Commands.AntiGravity
{
    /// <summary>
    /// Revit external command that displays and copies the user's hardware ID.
    /// This ID is needed for license registration.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GetHardwareIdCommand : BaseCommand
    {
        protected override bool RequiresLicense => false;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var hwId = HardwareIdGenerator.GetHardwareId();
            
            // Copy to clipboard
            Clipboard.SetText(hwId);
            
            // Show dialog with the hardware ID
            var dialog = new TaskDialog("Hardware ID")
            {
                MainInstruction = "Your Hardware ID",
                MainContent = hwId + "\n\n✓ Copied to clipboard!\n\nSend this ID to the administrator to activate your license.",
                CommonButtons = TaskDialogCommonButtons.Ok,
                MainIcon = TaskDialogIcon.TaskDialogIconInformation
            };
            
            dialog.Show();
            
            return Result.Succeeded;
        }
    }
}
