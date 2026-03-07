using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Commands.General.AutoDimension;
using antiGGGravity.Views.General;

namespace antiGGGravity.Commands.General
{
    [Transaction(TransactionMode.Manual)]
    public class AutoDimensionCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Show settings dialog
            var dlg = new AutoDimensionView();
            if (dlg.ShowDialog() != true)
                return Result.Cancelled;

            // Run with user settings
            AutoDimOrchestrator.Run(uidoc, dlg.Settings);

            return Result.Succeeded;
        }
    }
}
