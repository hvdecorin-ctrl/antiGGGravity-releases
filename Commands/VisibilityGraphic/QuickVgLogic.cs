using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace antiGGGravity.Commands.VisibilityGraphic
{
    public static class QuickVgLogic
    {
        public static (List<CategoryVisibilityModel> Structural, List<CategoryVisibilityModel> Coordinate) GetCategoryStates(View view)
        {
            if (view == null || view.Document == null) return (new List<CategoryVisibilityModel>(), new List<CategoryVisibilityModel>());

            var allModels = new List<CategoryVisibilityModel>();
            foreach (Category cat in view.Document.Settings.Categories)
            {
                if (cat.CategoryType == CategoryType.Model || cat.CategoryType == CategoryType.Annotation)
                {
                    if (view.CanCategoryBeHidden(cat.Id))
                    {
                        allModels.Add(new CategoryVisibilityModel
                        {
                            Name = cat.Name,
                            Id = cat.Id,
                            IsVisible = !view.GetCategoryHidden(cat.Id)
                        });
                    }
                }
            }
            
            var dict = allModels.ToDictionary(x => x.Id, x => x);

            var structuralBics = new List<BuiltInCategory>
            {
                BuiltInCategory.OST_StructuralFoundation,
                BuiltInCategory.OST_Walls,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_Roofs,
                BuiltInCategory.OST_Stairs,
                BuiltInCategory.OST_Rebar,
                BuiltInCategory.OST_StructConnections,
                BuiltInCategory.OST_Grids,
                BuiltInCategory.OST_Levels,
                BuiltInCategory.OST_CLines,
                BuiltInCategory.OST_VolumeOfInterest,
                BuiltInCategory.OST_RvtLinks
            };

            var structModels = new List<CategoryVisibilityModel>();
            var doc = view.Document;
            foreach (var bic in structuralBics)
            {
                var cat = Category.GetCategory(doc, bic);
                if (cat != null && dict.TryGetValue(cat.Id, out var model))
                {
                    structModels.Add(new CategoryVisibilityModel 
                    {
                        Name = model.Name,
                        Id = model.Id,
                        IsVisible = model.IsVisible
                    });
                }
            }

            return (structModels, allModels.OrderBy(x => x.Name).ToList());
        }

        public static void ApplyVisibility(View view, List<CategoryVisibilityModel> models)
        {
            if (view == null || !view.IsValidObject) return;
            using (var t = new Transaction(view.Document, "Quick VG Update"))
            {
                t.Start();
                foreach (var model in models)
                {
                    if (view.CanCategoryBeHidden(model.Id))
                    {
                        view.SetCategoryHidden(model.Id, !model.IsVisible);
                    }
                }
                t.Commit();
            }
        }
    }

    public class CategoryVisibilityModel : INotifyPropertyChanged
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        
        private bool _isVisible;
        public bool IsVisible 
        { 
            get => _isVisible; 
            set 
            {
                if (_isVisible != value)
                {
                    _isVisible = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

