using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Transfer.UI
{
    public class ReadFamilyTypesHandler : IExternalEventHandler
    {
        public FamilyManagerItem TargetFamily { get; set; }
        public event EventHandler<TypesReadEventArgs> TypesReadCompleted;

        public void Execute(UIApplication app)
        {
            if (TargetFamily == null || string.IsNullOrEmpty(TargetFamily.FilePath)) return;

            var extractedTypes = new List<FamilyManagerTypeItem>();
            string rfaPath = TargetFamily.FilePath;
            string txtPath = Path.ChangeExtension(rfaPath, ".txt");
            string familyName = TargetFamily.FamilyName;

            // Get loaded symbols for this family to check existence
            var loadedSymbols = new FilteredElementCollector(app.ActiveUIDocument.Document)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(s => s.FamilyName.Equals(familyName, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.Name)
                .ToList();

            try
            {
                var typeNamesList = new List<string>();

                // Priority 1: Fast Type Catalog Read (.txt)
                if (File.Exists(txtPath))
                {
                    var lines = File.ReadAllLines(txtPath);
                    if (lines.Length > 1)
                    {
                        foreach (var line in lines.Skip(1))
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var parts = line.Split(',');
                            if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                            {
                                string typeName = parts[0].Trim('"');
                                typeNamesList.Add(typeName);
                            }
                        }
                    }
                }
                
                // Priority 2: Safe Document Open (Requires Revit UI Thread)
                if (typeNamesList.Count == 0)
                {
                    OpenOptions openOptions = new OpenOptions();
                    openOptions.DetachFromCentralOption = DetachFromCentralOption.DetachAndPreserveWorksets;
                    
                    Document rfaDoc = null;
                    try
                    {
                        ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(rfaPath);
                        rfaDoc = app.Application.OpenDocumentFile(modelPath, openOptions);
                        if (rfaDoc.IsFamilyDocument)
                        {
                            var familyManager = rfaDoc.FamilyManager;
                            foreach (FamilyType ft in familyManager.Types)
                            {
                                typeNamesList.Add(ft.Name);
                            }
                        }
                    }
                    finally
                    {
                        if (rfaDoc != null)
                        {
                            rfaDoc.Close(false);
                        }
                    }
                }

                foreach (var name in typeNamesList)
                {
                    bool exists = loadedSymbols.Contains(name, StringComparer.OrdinalIgnoreCase);
                    extractedTypes.Add(new FamilyManagerTypeItem 
                    { 
                        TypeName = name, 
                        IsAlreadyInTarget = exists,
                        Status = exists ? "Loaded" : "Missing"
                    });
                }
            }
            catch (Exception)
            {
                // Types couldn't be extracted
            }

            TypesReadCompleted?.Invoke(this, new TypesReadEventArgs
            {
                Family = TargetFamily,
                ExtractedTypes = extractedTypes
            });
        }

        public string GetName() => "Read Family Types Handler";
    }

    public class TypesReadEventArgs : EventArgs
    {
        public FamilyManagerItem Family { get; set; }
        public List<FamilyManagerTypeItem> ExtractedTypes { get; set; }
    }
}
