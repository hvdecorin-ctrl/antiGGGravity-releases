using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.General;

namespace antiGGGravity.Commands.General
{
    [Transaction(TransactionMode.Manual)]
    public class PrintCommand : BaseCommand
    {
        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            PrintView window = new PrintView(doc);
            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
