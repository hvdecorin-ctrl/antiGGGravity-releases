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
            return Run(commandData.Application);
        }

        public Result Run(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            var categories = doc.Settings.Categories.Cast<Category>()
                .Where(c => (c.CategoryType == Autodesk.Revit.DB.CategoryType.Model || c.CategoryType == Autodesk.Revit.DB.CategoryType.Annotation))
                .ToList();

            PickElementsView win = new PickElementsView(categories);
            if (win.ShowDialog() != true) return Result.Cancelled;

            try
            {
                ISelectionFilter filter = null;
                string prompt = "Pick elements";

                switch (win.Mode)
                {
                    case FilterMode.SingleCategory:
                        if (win.SelectedCategory != null)
                        {
                            filter = new CategorySelectionFilter(win.SelectedCategory.Category.Id);
                            prompt = $"Pick {win.SelectedCategory.Name} elements";
                        }
                        break;
                    case FilterMode.SpecificCategory:
                        // Find the category ID from the BuiltInCategory
                        Category cat = Category.GetCategory(doc, win.QuickCategory);
                        if (cat != null)
                        {
                            filter = new CategorySelectionFilter(cat.Id);
                            prompt = $"Pick {cat.Name} elements";
                        }
                        else
                        {
                            // Fallback if category not active in doc for some reason
                            filter = new BuiltInCategorySelectionFilter(win.QuickCategory);
                            prompt = $"Pick {win.QuickCategory.ToString().Replace("OST_", "")} elements";
                        }
                        break;
                    case FilterMode.All3D:
                        filter = new ModelElementsSelectionFilter();
                        prompt = "Pick 3D (Model) elements";
                        break;
                    case FilterMode.All2D:
                        filter = new AnnotationElementsSelectionFilter();
                        prompt = "Pick 2D (Annotation) elements";
                        break;
                }

                if (filter != null)
                {
                    // Using PickElementsByRectangle replicates the original Python "1-shot box select" behavior
                    // Auto-finishing instantly when the user finishes dragging the box (no Finish button required).
                    var refs = uidoc.Selection.PickElementsByRectangle(filter, prompt);
                    if (refs != null && refs.Any())
                    {
                        uidoc.Selection.SetElementIds(refs.Select(r => r.Id).ToList());
                    }
                }
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled picking
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

    public class BuiltInCategorySelectionFilter : ISelectionFilter
    {
        private BuiltInCategory _bic;
        public BuiltInCategorySelectionFilter(BuiltInCategory bic) { _bic = bic; }
        public bool AllowElement(Element elem) => elem.Category != null && elem.Category.Id.Value == (long)_bic;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }

    public class ModelElementsSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            // Explicitly exclude Revit Links
            if (elem is RevitLinkInstance || elem is RevitLinkType) return false;

            // Explicitly exclude elements that only exist in a specific 2D view
            if (elem.ViewSpecific) return false;

            // Explicitly exclude categories we defined as 2D
            BuiltInCategory bic = (BuiltInCategory)elem.Category.Id.Value;
            if (bic == BuiltInCategory.OST_TextNotes ||
                bic == BuiltInCategory.OST_Lines ||
                bic == BuiltInCategory.OST_DetailComponents)
            {
                return false;
            }

            return elem.Category.CategoryType == CategoryType.Model;
        }
        public bool AllowReference(Reference reference, XYZ position) => false;
    }

    public class AnnotationElementsSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (elem.Category == null) return false;

            if (elem.Category.CategoryType == CategoryType.Annotation) return true;

            BuiltInCategory bic = (BuiltInCategory)elem.Category.Id.Value;
            if (bic == BuiltInCategory.OST_TextNotes ||
                bic == BuiltInCategory.OST_Lines ||
                bic == BuiltInCategory.OST_DetailComponents)
            {
                return true;
            }

            return false;
        }
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
