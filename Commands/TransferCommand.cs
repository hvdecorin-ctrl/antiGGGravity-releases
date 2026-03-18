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

                FamilyManagerRequestHandler fmHandler = new FamilyManagerRequestHandler();
                ExternalEvent fmExternalEvent = ExternalEvent.Create(fmHandler);

                ReadFamilyTypesHandler typesHandler = new ReadFamilyTypesHandler();
                ExternalEvent typesExEvent = ExternalEvent.Create(typesHandler);

                _window = new ViewTransferWindow(commandData.Application, handler, externalEvent, fmHandler, fmExternalEvent, typesHandler, typesExEvent);
                _window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog td = new TaskDialog("Application Error");
                td.MainInstruction = "Failed to load View Transfer Tool";
                td.MainContent = ex.Message;
                td.ExpandedContent = ex.ToString();
                td.Show();
                
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
