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
            
            _view = new FamilyDuplicatorView(doc, dupEvent, handler);
            _view.Show();

            return Result.Succeeded;
        }
    }
}
