using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.ProjectAudit
{
    /// <summary>
    /// External event handler that loads selected family types (symbols)
    /// via doc.LoadFamilySymbol(). Matches Python LoadFamilyTypesHandler.
    /// </summary>
    public class LoadFamilyTypesHandler : IExternalEventHandler
    {
        public string FamilyPath { get; set; }
        public List<string> TypeNames { get; set; } = new();

        /// <summary>
        /// Callback invoked on completion: (success, error)
        /// </summary>
        public Action<bool, string> Callback { get; set; }

        public void SetData(string familyPath, List<string> typeNames, Action<bool, string> callback)
        {
            FamilyPath = familyPath;
            TypeNames = typeNames;
            Callback = callback;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;

                if (string.IsNullOrEmpty(FamilyPath) || TypeNames == null || TypeNames.Count == 0)
                    return;

                using (var t = new Transaction(doc, "Load Family Types"))
                {
                    t.Start();
                    var loadOptions = new FamilyLoadOptions();
                    foreach (string typeName in TypeNames)
                    {
                        doc.LoadFamilySymbol(FamilyPath, typeName, loadOptions, out FamilySymbol _);
                    }
                    t.Commit();
                }

                Callback?.Invoke(true, null);
            }
            catch (Exception ex)
            {
                Callback?.Invoke(false, ex.Message);
            }
        }

        public string GetName() => "Load Family Types";
    }
}
