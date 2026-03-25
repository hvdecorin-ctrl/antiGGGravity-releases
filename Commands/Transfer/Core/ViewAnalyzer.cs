using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Core
{
    public class ViewAnalyzer
    {
        private readonly Document _sourceDoc;

        public ViewAnalyzer(Document sourceDoc)
        {
            _sourceDoc = sourceDoc;
        }

        public List<View> GetAllTransferableViewTypes()
        {
            return new FilteredElementCollector(_sourceDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => IsTransferableView(v))
                .OrderBy(v => v.ViewType.ToString())
                .ThenBy(v => v.Name)
                .ToList();
        }

        public List<ViewSheet> GetAllSheets()
        {
            return new FilteredElementCollector(_sourceDoc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
                .ToList();
        }

        public List<View> GetAllViewTemplates()
        {
            return new FilteredElementCollector(_sourceDoc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();
        }

        private bool IsTransferableView(View view)
        {
            if (view.IsTemplate) return false;

            // Common transferable views
            switch (view.ViewType)
            {
                case ViewType.DraftingView:
                case ViewType.Section:
                case ViewType.Elevation:
                case ViewType.Detail:
                case ViewType.Legend:
                case ViewType.FloorPlan:
                case ViewType.CeilingPlan:
                case ViewType.EngineeringPlan:
                    return true;
                default:
                    return false;
            }
        }
    }
}
