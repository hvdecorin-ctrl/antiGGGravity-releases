using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Overrides;

namespace antiGGGravity.Commands.Overrides
{
    [Transaction(TransactionMode.Manual)]
    public class ColorSplashCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return Run(commandData.Application);
        }

        public Result Run(UIApplication app)
        {
            try
            {
                ColorSplasherView view = new ColorSplasherView(app);
                view.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Color Splasher Error", $"Failed to open Color Splasher.\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
