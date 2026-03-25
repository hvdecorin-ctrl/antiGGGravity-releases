using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.ProjectAudit;

namespace antiGGGravity.Commands.ProjectAudit
{
    [Transaction(TransactionMode.Manual)]
    public class FamilyLoadingCommand : IExternalCommand
    {
        private static FamilyLoadingView _view;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Modeless implementation: bring to front if already open
                if (_view != null && _view.IsVisible)
                {
                    _view.Focus();
                    return Result.Succeeded;
                }

                // Create event handlers
                var loadHandler = new LoadFamilyTypesHandler();
                var loadEvent = ExternalEvent.Create(loadHandler);

                var symbolsHandler = new GetSymbolsHandler();
                var symbolsEvent = ExternalEvent.Create(symbolsHandler);

                // Create and show modeless form
                _view = new FamilyLoadingView(loadEvent, loadHandler, symbolsEvent, symbolsHandler);

                // Populate folder dropdown with saved paths
                _view.PopulateFolderDropdown();

                _view.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }
    }
}
