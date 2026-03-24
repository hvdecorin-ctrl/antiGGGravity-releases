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
        // This command must always be free so users can retrieve their HWID / activate
        protected override bool RequiresLicense => false;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Open the new WPF Activation Window
            var activationWindow = new antiGGGravity.Views.LicenseActivationWindow();
            
            // Set Revit as the parent window using WindowInteropHelper
            var uiApp = commandData.Application;
            var revitWindowHandle = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            
            var helper = new System.Windows.Interop.WindowInteropHelper(activationWindow);
            helper.Owner = revitWindowHandle;

            activationWindow.ShowDialog();
            
            return Result.Succeeded;
        }
    }
}
