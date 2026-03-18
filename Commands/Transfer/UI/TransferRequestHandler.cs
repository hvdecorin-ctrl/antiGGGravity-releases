using System;
using System.Collections.Generic;
using antiGGGravity.Commands.Transfer.DTO;
using antiGGGravity.Commands.Transfer.Modules;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Transfer.UI
{
    public class TransferRequestHandler : IExternalEventHandler
    {
        public Document SourceDoc { get; set; }
        public TransferOptions Options { get; set; }
        public List<ViewTransferItem> SelectedViews { get; set; }
        public List<SheetTransferItem> SelectedSheets { get; set; }
        public List<FamilyTransferItem> SelectedFamilies { get; set; }
        public List<SystemFamilyTypeItem> SelectedSystemTypes { get; set; }
        public event EventHandler TransferCompleted;

        public void Execute(UIApplication app)
        {
            if (SourceDoc == null || Options == null) return;
            Document targetDoc = app.ActiveUIDocument.Document;

            try
            {
                TransferEngineWrapper engine = new TransferEngineWrapper(SourceDoc, targetDoc, Options);
                engine.ExecuteTransfer(SelectedViews, SelectedSheets, SelectedFamilies, SelectedSystemTypes);
                
                TransferCompleted?.Invoke(this, EventArgs.Empty);
                TaskDialog.Show("Transfer Complete", "Successfully transferred selected items.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Transfer Error", $"An error occurred during transfer:\n{ex.Message}");
            }
        }

        public string GetName()
        {
            return "View Transfer Engine Event Handler";
        }
    }
}
