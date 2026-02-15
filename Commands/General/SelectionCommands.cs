using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI.Selection;
using antiGGGravity.Views.General;

namespace antiGGGravity.Commands.General
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class PickElementsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            var categories = doc.Settings.Categories.Cast<Category>()
                .Where(c => (c.CategoryType == Autodesk.Revit.DB.CategoryType.Model || c.CategoryType == Autodesk.Revit.DB.CategoryType.Annotation))
                .ToList();

            PickElementsView win = new PickElementsView(categories);
            if (win.ShowDialog() != true) return Result.Cancelled;

            Category selectedCat = win.SelectedCategory.Category;

            try
            {
                var refs = uidoc.Selection.PickObjects(ObjectType.Element, new CategorySelectionFilter(selectedCat.Id), $"Pick {selectedCat.Name} elements");
                if (refs != null && refs.Any())
                {
                    uidoc.Selection.SetElementIds(refs.Select(r => r.ElementId).ToList());
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }

    public class CategorySelectionFilter : ISelectionFilter
    {
        private ElementId _catId;
        public CategorySelectionFilter(ElementId catId) { _catId = catId; }
        public bool AllowElement(Element elem) => elem.Category != null && elem.Category.Id == _catId;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
