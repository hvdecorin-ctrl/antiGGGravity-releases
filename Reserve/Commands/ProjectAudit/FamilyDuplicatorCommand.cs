using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.ProjectAudit;

namespace antiGGGravity.Commands.ProjectAudit
{
    [Transaction(TransactionMode.Manual)]
    public class FamilyDuplicatorCommand : IExternalCommand
    {
        private static FamilyDuplicatorView _view;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Modeless implementation: bring to front if already open
            if (_view != null && _view.IsVisible)
            {
                _view.Focus();
                return Result.Succeeded;
            }

            var handler = new FamilyDuplicationHandler();
            var dupEvent = ExternalEvent.Create(handler);
            
            // Create events for the FamilyLoading window
            var loadHandler = new LoadFamilyTypesHandler();
            var loadEvent = ExternalEvent.Create(loadHandler);

            var symbolsHandler = new GetSymbolsHandler();
            var symbolsEvent = ExternalEvent.Create(symbolsHandler);
            
            _view = new FamilyDuplicatorView(doc, dupEvent, handler, loadEvent, loadHandler, symbolsEvent, symbolsHandler);
            
            // Set Revit as the owner of the window to fix keyboard focus issues (so users can type into DataGrid)
            var wrapper = new System.Windows.Interop.WindowInteropHelper(_view);
            wrapper.Owner = commandData.Application.MainWindowHandle;

            _view.Show();

            return Result.Succeeded;
        }
    }
}
