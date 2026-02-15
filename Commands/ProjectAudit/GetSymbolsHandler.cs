using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.ProjectAudit
{
    /// <summary>
    /// External event handler that reads all family symbols (types) from an .rfa file
    /// via a dry transaction (rolled back), matching Python GetSymbolsHandler behavior.
    /// </summary>
    public class GetSymbolsHandler : IExternalEventHandler
    {
        public string FamilyPath { get; set; }
        public List<string> AllSymbols { get; private set; } = new();
        public HashSet<string> LoadedSymbols { get; private set; } = new();
        public string Error { get; private set; }
        public bool IsComplete { get; private set; }

        /// <summary>
        /// Callback invoked on completion: (allSymbols, loadedSymbols, error)
        /// </summary>
        public Action<List<string>, HashSet<string>, string> Callback { get; set; }

        public void SetData(string familyPath, Action<List<string>, HashSet<string>, string> callback)
        {
            FamilyPath = familyPath;
            Callback = callback;
            AllSymbols = new List<string>();
            LoadedSymbols = new HashSet<string>();
            Error = null;
            IsComplete = false;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument.Document;
                if (string.IsNullOrEmpty(FamilyPath)) return;

                string familyName = System.IO.Path.GetFileNameWithoutExtension(FamilyPath);

                // Check if family is already loaded and get loaded symbols
                var collector = new FilteredElementCollector(doc).OfClass(typeof(Family));
                Family existingFamily = collector
                    .Cast<Family>()
                    .FirstOrDefault(f => f.Name == familyName);

                if (existingFamily != null)
                {
                    foreach (var symbolId in existingFamily.GetFamilySymbolIds())
                    {
                        var symbol = doc.GetElement(symbolId) as FamilySymbol;
                        if (symbol != null)
                            LoadedSymbols.Add(symbol.Name);
                    }
                }

                // Get all symbols via dry transaction (rolled back)
                var symbolSet = new HashSet<string>();
                using (var t = new Transaction(doc, "Get symbols"))
                {
                    t.Start();
                    try
                    {
                        Family loadedFam = null;
                        var loadOptions = new FamilyLoadOptions();
                        doc.LoadFamily(FamilyPath, loadOptions, out loadedFam);

                        if (loadedFam != null)
                        {
                            foreach (var symbolId in loadedFam.GetFamilySymbolIds())
                            {
                                var symbol = doc.GetElement(symbolId) as FamilySymbol;
                                if (symbol != null)
                                    symbolSet.Add(symbol.Name);
                            }
                        }
                    }
                    finally
                    {
                        // Always rollback — this is a dry transaction
                        t.RollBack();
                    }
                }

                AllSymbols = symbolSet.OrderBy(s => s).ToList();
                IsComplete = true;

                Callback?.Invoke(AllSymbols, LoadedSymbols, null);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                IsComplete = true;
                Callback?.Invoke(new List<string>(), new HashSet<string>(), ex.Message);
            }
        }

        public string GetName() => "Get Family Symbols";
    }
}
