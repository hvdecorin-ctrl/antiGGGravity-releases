using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Views.ProjectAudit;

namespace antiGGGravity.Commands.ProjectAudit
{
    [Transaction(TransactionMode.Manual)]
    public class ProjectTextStyleCommand : IExternalCommand
    {
        private static ProjectTextStyleView _view;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_view != null && _view.IsVisible)
                {
                    _view.Focus();
                    return Result.Succeeded;
                }

                var handler = new ProjectTextStyleHandler();
                var alignEvent = ExternalEvent.Create(handler);

                _view = new ProjectTextStyleView(alignEvent, handler, commandData.Application.ActiveUIDocument.Document);
                
                // Set initial owner to Revit
                try
                {
                    var process = System.Diagnostics.Process.GetCurrentProcess();
                    var wrapper = new System.Windows.Interop.WindowInteropHelper(_view);
                    wrapper.Owner = process.MainWindowHandle;
                }
                catch { }

                _view.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
