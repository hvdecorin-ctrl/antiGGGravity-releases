using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Transfer.Core
{
    public class DocumentLoader
    {
        private UIApplication _uiApp;
        public Document SourceDocument { get; private set; }
        public Document TargetDocument { get; private set; }

        public DocumentLoader(UIApplication uiApp)
        {
            _uiApp = uiApp;
            TargetDocument = uiApp.ActiveUIDocument.Document;
        }

        public bool LoadSourceDocument(string filePath, out string errorMessage)
        {
            errorMessage = string.Empty;
            try
            {
                if (!File.Exists(filePath))
                {
                    errorMessage = "The selected file does not exist.";
                    return false;
                }

                if (filePath.Equals(TargetDocument.PathName, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Source and target documents cannot be the same.";
                    return false;
                }

                // Check if already open
                foreach (Document doc in _uiApp.Application.Documents)
                {
                    if (doc.PathName.Equals(filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        SourceDocument = doc;
                        return true;
                    }
                }

                // Open in background
                ModelPath modelPath = ModelPathUtils.ConvertUserVisiblePathToModelPath(filePath);
                OpenOptions openOptions = new OpenOptions { DetachFromCentralOption = DetachFromCentralOption.DoNotDetach };
                SourceDocument = _uiApp.Application.OpenDocumentFile(modelPath, openOptions);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        
        public void CloseSourceDocument()
        {
            if (SourceDocument != null && SourceDocument.IsLinked == false && SourceDocument.Title != TargetDocument.Title)
            {
                // SourceDocument.Close(false); 
                // Careful: Can't easily close if the user had it open beforehand, 
                // so we will skip closing for now unless we explicitly track if we opened it.
            }
        }
    }
}
