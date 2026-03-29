using System;
using System.Collections.Generic;
using antiGGGravity.Commands.Transfer.Core;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Transfer.UI
{
    public class FamilyManagerRequestHandler : IExternalEventHandler
    {
        public List<FamilyManagerItem> FamiliesToProcess { get; set; } = new List<FamilyManagerItem>();
        public event EventHandler<FamilyManagerProcessResultEventArgs> ProcessCompleted;

        public void Execute(UIApplication app)
        {
            if (FamiliesToProcess == null || FamiliesToProcess.Count == 0) return;

            int loaded = 0;
            int updated = 0;
            List<string> errors = new List<string>();

            try
            {
                var engine = new FamilyManagerEngine(app.Application);
                var doc = app.ActiveUIDocument.Document;
                
                using (Transaction t = new Transaction(doc, "Batch Load & Update Families"))
                {
                    t.Start();
                    engine.ProcessFamilies(doc, FamiliesToProcess, out loaded, out updated, out errors);
                    t.Commit();
                }
            }
            catch (Exception ex)
            {
                errors.Add($"FATAL: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                ProcessCompleted?.Invoke(this, new FamilyManagerProcessResultEventArgs
                {
                    LoadedCount = loaded,
                    UpdatedCount = updated,
                    Errors = errors
                });
            }
        }

        public string GetName() => "Family Manager Processor";
    }

    public class FamilyManagerProcessResultEventArgs : EventArgs
    {
        public int LoadedCount { get; set; }
        public int UpdatedCount { get; set; }
        public List<string> Errors { get; set; }
    }
}
