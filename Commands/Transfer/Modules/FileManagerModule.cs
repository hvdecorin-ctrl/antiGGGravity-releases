using System;
using antiGGGravity.Commands.Transfer.Core;
using Autodesk.Revit.UI;

namespace antiGGGravity.Commands.Transfer.Modules
{
    public class FileManagerModule
    {
        private readonly UIApplication _uiApp;
        public DocumentLoader DocLoader { get; private set; }

        public FileManagerModule(UIApplication uiApp)
        {
            _uiApp = uiApp;
            DocLoader = new DocumentLoader(uiApp);
        }

        public bool SelectAndLoadSourceFile(string filePath, out string errorMessage)
        {
            return DocLoader.LoadSourceDocument(filePath, out errorMessage);
        }

        public void Cleanup()
        {
            DocLoader.CloseSourceDocument();
        }
    }
}
