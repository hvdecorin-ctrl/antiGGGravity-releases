using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.VisibilityGraphic;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    [Transaction(TransactionMode.Manual)]
    public class QuickFilterCommand : BaseCommand
    {

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Run(commandData.Application);
        }

        public Result Run(UIApplication app)
        {
            try
            {
                QuickFilterView view = new QuickFilterView(app);
                view.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Quick Filter Error", $"Failed to open Quick Filter.\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
