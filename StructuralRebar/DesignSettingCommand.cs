using System;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.UI;

namespace antiGGGravity.StructuralRebar
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DesignSettingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Instantiate and show the Design Code Reference Window
                var refWindow = new DesignCodeReferenceWindow();
                
                // Set Revit as the owner window using Win32 API interop
                var revitWindow = new System.Windows.Interop.WindowInteropHelper(refWindow)
                {
                    Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle
                };

                refWindow.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = "The Design Setting window failed to open:\n" + ex.Message;
                return Result.Failed;
            }
        }
    }
}
