using System;
using antiGGGravity.Commands.Transfer.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class FamilyManagerCommand : BaseCommand
    {
        private static FamilyManagerWindow _window;

        protected override Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (_window != null && _window.IsLoaded)
            {
                _window.Focus();
                return Result.Succeeded;
            }

            // Setup External Event Handler for non-modal execution
            TransferRequestHandler handler = new TransferRequestHandler();
            ExternalEvent externalEvent = ExternalEvent.Create(handler);

            FamilyManagerRequestHandler fmHandler = new FamilyManagerRequestHandler();
            ExternalEvent fmExternalEvent = ExternalEvent.Create(fmHandler);

            ReadFamilyTypesHandler typesHandler = new ReadFamilyTypesHandler();
            ExternalEvent typesExEvent = ExternalEvent.Create(typesHandler);

            DuplicatorRequestHandler dupHandler = new DuplicatorRequestHandler();
            ExternalEvent dupExEvent = ExternalEvent.Create(dupHandler);

            _window = new FamilyManagerWindow(commandData.Application, handler, externalEvent, fmHandler, fmExternalEvent, typesHandler, typesExEvent, dupHandler, dupExEvent);
            _window.Show();

            return Result.Succeeded;
        }
    }
}
