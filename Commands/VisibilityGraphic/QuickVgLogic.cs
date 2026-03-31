using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using antiGGGravity.Utilities;

// Discipline filter matching Revit's native VG dialog "Filter list" dropdown
public enum VgDisciplineFilter
{
    All,
    Architecture,
    Structure,
    Mechanical,
    Electrical,
    Piping,
    Infrastructure
}

namespace antiGGGravity.Commands.VisibilityGraphic
{
    public static class QuickVgLogic
    {
        public static (List<CategoryVisibilityModel> Structural, List<CategoryVisibilityModel> Coordinate) GetCategoryStates(View view, string slot = "A")
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

            var customIds = LoadCustomCategoryIds(slot);
            var structModels = new List<CategoryVisibilityModel>();
            foreach (var id in customIds)
            {
                var eid = RevitCompatibility.NewElementId(id);
                if (dict.TryGetValue(eid, out var model))
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

        /// <summary>
        /// Determines the discipline of a Revit category, matching the native VG "Filter list" dropdown.
        /// The Revit API does not expose discipline directly, so we use a BuiltInCategory mapping.
        /// Categories not in the map default to Architecture (matching Revit's behavior).
        /// </summary>
        public static VgDisciplineFilter GetDiscipline(ElementId categoryId)
        {
            long idVal = categoryId.GetIdValue();

            if (_structureCategories.Contains(idVal)) return VgDisciplineFilter.Structure;
            if (_mechanicalCategories.Contains(idVal)) return VgDisciplineFilter.Mechanical;
            if (_electricalCategories.Contains(idVal)) return VgDisciplineFilter.Electrical;
            if (_pipingCategories.Contains(idVal)) return VgDisciplineFilter.Piping;
            if (_infrastructureCategories.Contains(idVal)) return VgDisciplineFilter.Infrastructure;

            // Default: Architecture (matches Revit behavior)
            return VgDisciplineFilter.Architecture;
        }

        // Structure discipline categories
        private static readonly HashSet<long> _structureCategories = new HashSet<long>
        {
            (long)BuiltInCategory.OST_StructuralColumns,
            (long)BuiltInCategory.OST_StructuralFraming,
            (long)BuiltInCategory.OST_StructuralFoundation,
            (long)BuiltInCategory.OST_Rebar,
            (long)BuiltInCategory.OST_StructConnections,
            (long)BuiltInCategory.OST_StructuralStiffener,
            (long)BuiltInCategory.OST_StructuralTruss,
            (long)BuiltInCategory.OST_FabricAreas,
            (long)BuiltInCategory.OST_FabricReinforcement,
            (long)BuiltInCategory.OST_Coupler,
            (long)BuiltInCategory.OST_StructuralFramingSystem,
            (long)BuiltInCategory.OST_StructuralAnnotations,
            (long)BuiltInCategory.OST_AnalyticalNodes,
        };

        // Mechanical discipline categories
        private static readonly HashSet<long> _mechanicalCategories = new HashSet<long>
        {
            (long)BuiltInCategory.OST_MechanicalEquipment,
            (long)BuiltInCategory.OST_DuctCurves,
            (long)BuiltInCategory.OST_DuctFitting,
            (long)BuiltInCategory.OST_DuctAccessory,
            (long)BuiltInCategory.OST_DuctTerminal,
            (long)BuiltInCategory.OST_DuctInsulations,
            (long)BuiltInCategory.OST_DuctLinings,
            (long)BuiltInCategory.OST_PlaceHolderDucts,
            (long)BuiltInCategory.OST_FlexDuctCurves,
            (long)BuiltInCategory.OST_HVAC_Zones,
        };

        // Electrical discipline categories
        private static readonly HashSet<long> _electricalCategories = new HashSet<long>
        {
            (long)BuiltInCategory.OST_ElectricalFixtures,
            (long)BuiltInCategory.OST_ElectricalEquipment,
            (long)BuiltInCategory.OST_LightingFixtures,
            (long)BuiltInCategory.OST_LightingDevices,
            (long)BuiltInCategory.OST_CableTray,
            (long)BuiltInCategory.OST_CableTrayFitting,
            (long)BuiltInCategory.OST_Conduit,
            (long)BuiltInCategory.OST_ConduitFitting,
            (long)BuiltInCategory.OST_CommunicationDevices,
            (long)BuiltInCategory.OST_DataDevices,
            (long)BuiltInCategory.OST_FireAlarmDevices,
            (long)BuiltInCategory.OST_NurseCallDevices,
            (long)BuiltInCategory.OST_SecurityDevices,
            (long)BuiltInCategory.OST_TelephoneDevices,
            (long)BuiltInCategory.OST_ElectricalCircuit,
            (long)BuiltInCategory.OST_Wire,
        };

        // Piping discipline categories
        private static readonly HashSet<long> _pipingCategories = new HashSet<long>
        {
            (long)BuiltInCategory.OST_PipeCurves,
            (long)BuiltInCategory.OST_PipeFitting,
            (long)BuiltInCategory.OST_PipeAccessory,
            (long)BuiltInCategory.OST_PipeInsulations,
            (long)BuiltInCategory.OST_PlumbingFixtures,
            (long)BuiltInCategory.OST_Sprinklers,
            (long)BuiltInCategory.OST_PlaceHolderPipes,
            (long)BuiltInCategory.OST_FlexPipeCurves,
            (long)BuiltInCategory.OST_PipingSystem,
        };

        // Infrastructure discipline categories
        private static readonly HashSet<long> _infrastructureCategories = new HashSet<long>
        {
            // Infrastructure categories are relatively new (Revit 2025+)
            // Using numeric IDs for categories that may not exist in older API versions
        };

        private static string GetConfigFilePath(string slot = "A")
        {
            string appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            string configDir = System.IO.Path.Combine(appData, "antiGGGravity", "Config");
            if (!System.IO.Directory.Exists(configDir))
                System.IO.Directory.CreateDirectory(configDir);

            // Slot A uses original filename for backward compatibility
            string fileName = slot == "A" ? "QuickVgCustomCategories.txt" : $"QuickVgCustomCategories_{slot}.txt";
            return System.IO.Path.Combine(configDir, fileName);
        }

        public static List<long> LoadCustomCategoryIds(string slot = "A")
        {
            string path = GetConfigFilePath(slot);
            if (!System.IO.File.Exists(path))
            {
                // Default categories for slot A only
                if (slot == "A")
                {
                    return new List<BuiltInCategory>
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
                    }.Select(b => (long)b).ToList();
                }
                return new List<long>();
            }

            var ids = new List<long>();
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (long.TryParse(line, out long id))
                    ids.Add(id);
            }
            return ids;
        }

        public static void SaveCustomCategoryIds(IEnumerable<long> ids, string slot = "A")
        {
            string path = GetConfigFilePath(slot);
            System.IO.File.WriteAllLines(path, ids.Select(id => id.ToString()).ToArray());
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

