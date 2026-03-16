using System;
using antiGGGravity.Commands.Transfer.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ViewTransferCommand : IExternalCommand
    {
        private static ViewTransferWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_window != null && _window.IsLoaded)
            {
                _window.Focus();
                return Result.Succeeded;
            }

            try
            {
                // Setup External Event Handler for non-modal execution
                TransferRequestHandler handler = new TransferRequestHandler();
                ExternalEvent externalEvent = ExternalEvent.Create(handler);

                _window = new ViewTransferWindow(commandData.Application, handler, externalEvent);
                _window.Show();

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
