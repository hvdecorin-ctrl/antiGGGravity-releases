using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    public class QuickVgEventHandler : IExternalEventHandler
    {
        public List<CategoryVisibilityModel> CategoriesToApply { get; set; }
        public View TargetView { get; set; }

        public void Execute(UIApplication app)
        {
            try
            {
                if (TargetView == null || !TargetView.IsValidObject || CategoriesToApply == null) return;

                QuickVgLogic.ApplyVisibility(TargetView, CategoriesToApply);
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Quick VG Error", $"An error occurred during visibility update: {ex.Message}");
            }
        }

        public string GetName()
        {
            return "Quick VG Event Handler";
        }
    }
}
