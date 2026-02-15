using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using antiGGGravity.Commands;

namespace antiGGGravity.Commands.AntiGravity
{
    /// <summary>
    /// Template for a licensed command. Validates license before executing.
    /// Copy this as a starting point for your licensed features.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LicensedCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // ========================================
            // YOUR LICENSED FUNCTIONALITY GOES HERE
            // ========================================
            
            // Example: Show success message
            TaskDialog.Show("Licensed Feature", 
                "Success! Your licensed feature is now executing.");
            
            // Add your actual functionality here...
            
            return Result.Succeeded;
        }
    }
}
