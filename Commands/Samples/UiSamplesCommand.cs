using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.Samples;

namespace antiGGGravity.Commands.Samples
{
    [Transaction(TransactionMode.Manual)]
    public class UiSamplesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UiSamplesLauncher launcher = new UiSamplesLauncher();
                launcher.Show();
                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = "CRASH IN UI SAMPLES COMMAND:\n" + ex.ToString();
                return Result.Failed;
            }
        }
    }
}
