using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands.Transfer.Core;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Modules
{
    public class ViewCollectorModule
    {
        private readonly ViewAnalyzer _analyzer;

        public ViewCollectorModule(Document sourceDoc)
        {
            _analyzer = new ViewAnalyzer(sourceDoc);
        }

        public List<ViewTransferItem> GetTransferableViews()
        {
            var views = _analyzer.GetAllTransferableViewTypes();
            return views.Select(v => new ViewTransferItem
            {
                SourceViewId = v.Id,
                ViewName = v.Name,
                ViewType = v.ViewType,
                IsSelected = false
            }).ToList();
        }

        public List<SheetTransferItem> GetSheets()
        {
            var sheets = _analyzer.GetAllSheets();
            return sheets.Select(s => new SheetTransferItem
            {
                SourceSheetId = s.Id,
                SheetNumber = s.SheetNumber,
                SheetName = s.Name,
                IsSelected = false
            }).ToList();
        }
    }
}
