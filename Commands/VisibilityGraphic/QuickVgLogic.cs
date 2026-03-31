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
    Infrastructure,
    Annotation,
    Links
}

namespace antiGGGravity.Commands.VisibilityGraphic
{
    public static class QuickVgLogic
    {
        public static (List<CategoryVisibilityModel> Structural, List<CategoryVisibilityModel> Coordinate) GetCategoryStates(View view, string slot = "A")
        {
            if (view == null || view.Document == null) return (new List<CategoryVisibilityModel>(), new List<CategoryVisibilityModel>());

            var allModels = new List<CategoryVisibilityModel>();
            
            // 1. Fetch Categories
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
                            IsVisible = !view.GetCategoryHidden(cat.Id),
                            IsLinkInstance = false,
                            IsAnnotation = (cat.CategoryType == CategoryType.Annotation)
                        });
                    }
                }
            }

            // 2. Fetch Revit Links
            var links = new FilteredElementCollector(view.Document)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var link in links)
            {
                allModels.Add(new CategoryVisibilityModel
                {
                    Name = $"[Link] {link.Name}",
                    Id = link.Id,
                    IsVisible = !link.IsHidden(view),
                    IsLinkInstance = true,
                    IsAnnotation = false
                });
            }
            
            var allModelsSorted = allModels.OrderBy(x => x.Name).ToList();

            // 3. Load Custom Selection
            var customEntries = LoadCustomCategoryIds(slot);
            var structModels = new List<CategoryVisibilityModel>();
            
            foreach (var entry in customEntries)
            {
                CategoryVisibilityModel match = null;
                if (entry.StartsWith("CAT:"))
                {
                    if (long.TryParse(entry.Substring(4), out long idVal))
                    {
                        var eid = RevitCompatibility.NewElementId(idVal);
                        match = allModelsSorted.FirstOrDefault(x => !x.IsLinkInstance && x.Id == eid);
                    }
                }
                else if (entry.StartsWith("LNK:"))
                {
                    string linkName = entry.Substring(4);
                    // Match link by name (name from model includes [Link] prefix)
                    match = allModelsSorted.FirstOrDefault(x => x.IsLinkInstance && x.Name == $"[Link] {linkName}");
                }

                if (match != null)
                {
                    structModels.Add(new CategoryVisibilityModel 
                    {
                        Name = match.Name,
                        Id = match.Id,
                        IsVisible = match.IsVisible,
                        IsLinkInstance = match.IsLinkInstance,
                        IsAnnotation = match.IsAnnotation
                    });
                }
            }

            return (structModels, allModelsSorted);
        }

        public static VgDisciplineFilter GetDiscipline(CategoryVisibilityModel model)
        {
            if (model.IsLinkInstance) return VgDisciplineFilter.Links;
            if (model.IsAnnotation) return VgDisciplineFilter.Annotation;
            
            long idVal = model.Id.GetIdValue();

            if (_structureCategories.Contains(idVal)) return VgDisciplineFilter.Structure;
            if (_mechanicalCategories.Contains(idVal)) return VgDisciplineFilter.Mechanical;
            if (_electricalCategories.Contains(idVal)) return VgDisciplineFilter.Electrical;
            if (_pipingCategories.Contains(idVal)) return VgDisciplineFilter.Piping;
            if (_infrastructureCategories.Contains(idVal)) return VgDisciplineFilter.Infrastructure;
            if (_architectureCategories.Contains(idVal)) return VgDisciplineFilter.Architecture;

            // Default fallback if it's a model category but not in explicit lists
            return VgDisciplineFilter.Architecture;
        }

        // Core Architecture Categories to "Tidy Up" the list
        private static readonly HashSet<long> _architectureCategories = new HashSet<long>
        {
            (long)BuiltInCategory.OST_Walls,
            (long)BuiltInCategory.OST_Doors,
            (long)BuiltInCategory.OST_Windows,
            (long)BuiltInCategory.OST_Floors,
            (long)BuiltInCategory.OST_Roofs,
            (long)BuiltInCategory.OST_Ceilings,
            (long)BuiltInCategory.OST_Stairs,
            (long)BuiltInCategory.OST_StairsRailing,
            (long)BuiltInCategory.OST_Ramps,
            (long)BuiltInCategory.OST_Furniture,
            (long)BuiltInCategory.OST_FurnitureSystems,
            (long)BuiltInCategory.OST_Casework,
            (long)BuiltInCategory.OST_SpecialityEquipment,
            (long)BuiltInCategory.OST_CurtainWallPanels,
            (long)BuiltInCategory.OST_CurtainWallMullions,
            (long)BuiltInCategory.OST_Columns, // Architectural Columns
            (long)BuiltInCategory.OST_Site,
            (long)BuiltInCategory.OST_Topography,
            (long)BuiltInCategory.OST_Planting,
            (long)BuiltInCategory.OST_Entourage,
            (long)BuiltInCategory.OST_Rooms,
            (long)BuiltInCategory.OST_Areas
        };

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

        public static List<string> LoadCustomCategoryIds(string slot = "A")
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
                    }.Select(b => "CAT:" + (long)b).ToList();
                }
                return new List<string>();
            }

            var entries = new List<string>();
            foreach (var line in System.IO.File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                
                // Compatibility with old format (raw numbers)
                if (long.TryParse(line, out long idVal))
                {
                    entries.Add("CAT:" + idVal);
                }
                else
                {
                    entries.Add(line);
                }
            }
            return entries;
        }

        public static void SaveCustomCategoryIds(IEnumerable<string> entries, string slot = "A")
        {
            string path = GetConfigFilePath(slot);
            System.IO.File.WriteAllLines(path, entries.ToArray());
        }

        public static void ApplyVisibility(View view, List<CategoryVisibilityModel> models)
        {
            if (view == null || !view.IsValidObject) return;
            using (var t = new Transaction(view.Document, "Quick VG Update"))
            {
                t.Start();

                // If any link instance is set to visible, ensure the Revit Links category is ON
                bool anyLinkVisible = models.Any(m => m.IsLinkInstance && m.IsVisible);
                if (anyLinkVisible)
                {
                    ElementId rvtLinksId = RevitCompatibility.NewElementId((long)BuiltInCategory.OST_RvtLinks);
                    if (view.CanCategoryBeHidden(rvtLinksId))
                    {
                        view.SetCategoryHidden(rvtLinksId, false);
                    }
                }

                foreach (var model in models)
                {
                    if (model.IsLinkInstance)
                    {
                        var element = view.Document.GetElement(model.Id);
                        if (element != null && element.CanBeHidden(view))
                        {
                            var ids = new List<ElementId> { model.Id };
                            if (model.IsVisible)
                                view.UnhideElements(ids);
                            else
                                view.HideElements(ids);
                        }
                    }
                    else
                    {
                        if (view.CanCategoryBeHidden(model.Id))
                        {
                            view.SetCategoryHidden(model.Id, !model.IsVisible);
                        }
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
        public bool IsLinkInstance { get; set; }
        public bool IsAnnotation { get; set; }
        
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

