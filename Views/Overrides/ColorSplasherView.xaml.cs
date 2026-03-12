using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace antiGGGravity.Views.Overrides
{
    public partial class ColorSplasherView : Window
    {
        private UIApplication _uiApp;
        private UIDocument _uidoc;
        private Document _doc;
        
        // External Events
        private ExternalEvent _applyEvent;
        private ExternalEvent _resetEvent;
        private ExternalEvent _legendEvent;
        private ExternalEvent _filtersEvent;
        private ColorSplashHandler _handler;

        public ObservableCollection<CategoryItem> Categories { get; set; }
        
        // Full list for filtering
        private List<ParameterItem> _allParameters; 
        
        // Bound collection
        public ObservableCollection<ParameterItem> Parameters { get; set; }
        public ObservableCollection<ValueItem> Values { get; set; }

        public ColorSplasherView(UIApplication uiApp)
        {
            InitializeComponent();
            _uiApp = uiApp;
            _uidoc = uiApp.ActiveUIDocument;
            _doc = _uidoc.Document;

            // Initialize Handlers
            _handler = new ColorSplashHandler(this);
            _applyEvent = ExternalEvent.Create(_handler);

            Categories = new ObservableCollection<CategoryItem>();
            _allParameters = new List<ParameterItem>();
            Parameters = new ObservableCollection<ParameterItem>();
            Values = new ObservableCollection<ValueItem>();

            UI_List_Parameters.ItemsSource = Parameters;
            UI_Grid_Values.ItemsSource = Values;
            
            LoadCategories();
        }

        private void LoadCategories()
        {
            var collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                .WhereElementIsNotElementType()
                .WhereElementIsViewIndependent();

            var catSet = new HashSet<CategoryIdComparer>();
            
            foreach (Element e in collector)
            {
                if (e.Category != null && (e.Category.HasMaterialQuantities || e.Category.CategoryType == CategoryType.Model)) 
                {
                    catSet.Add(new CategoryIdComparer(e.Category));
                }
            }

            Categories.Clear();
            foreach (var wrapper in catSet.OrderBy(x => x.Category.Name))
            {
                Categories.Add(new CategoryItem { Name = wrapper.Category.Name, Category = wrapper.Category });
            }

            UI_Combo_Category.ItemsSource = Categories;
            UI_Combo_Category.DisplayMemberPath = "Name";
        }

        private void UI_Combo_Category_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UI_Combo_Category.SelectedItem is CategoryItem catItem)
            {
                LoadParameters(catItem.Category);
            }
        }

        private void LoadParameters(Category cat)
        {
            _allParameters.Clear();
            Parameters.Clear();
            UI_Txt_Search.Text = "";
            
            Element elem = new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                .OfCategoryId(cat.Id)
                .FirstOrDefault();

            if (elem != null)
            {
                var uniqueParams = new Dictionary<string, ParameterItem>();

                // 1. Instance Parameters
                foreach (Parameter p in elem.Parameters)
                {
                    if (p.StorageType != StorageType.None && !uniqueParams.ContainsKey(p.Definition.Name))
                    {
                        uniqueParams.Add(p.Definition.Name, new ParameterItem { Name = p.Definition.Name, StorageType = p.StorageType, Definition = p.Definition, Id = p.Id });
                    }
                }

                // 2. Type Parameters
                Element typeElem = _doc.GetElement(elem.GetTypeId());
                if (typeElem != null)
                {
                    foreach (Parameter p in typeElem.Parameters)
                    {
                        if (p.StorageType != StorageType.None && !uniqueParams.ContainsKey(p.Definition.Name))
                        {
                            uniqueParams.Add(p.Definition.Name, new ParameterItem { Name = p.Definition.Name, StorageType = p.StorageType, Definition = p.Definition, IsTypeParameter = true, Id = p.Id });
                        }
                    }
                }
                
                // Sort
                _allParameters = uniqueParams.Values.OrderBy(x => x.Name).ToList();
                
                // Populate visible list
                foreach (var p in _allParameters)
                {
                    Parameters.Add(p);
                }
                AdjustColumnWidth();
            }
        }
        
        private void UI_Txt_Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = UI_Txt_Search.Text.ToLower();
            Parameters.Clear();
            foreach(var item in _allParameters)
            {
                if (string.IsNullOrEmpty(filter) || item.Name.ToLower().Contains(filter))
                {
                    Parameters.Add(item);
                }
            }
            AdjustColumnWidth();
        }

        private void AdjustColumnWidth()
        {
            if (Parameters.Count == 0) return;

            double maxWidth = 0;
            // Get typeface from the ListBox's font settings
            var typeface = new Typeface(this.FontFamily, this.FontStyle, this.FontWeight, this.FontStretch);
            
            foreach (var p in Parameters)
            {
                var formattedText = new System.Windows.Media.FormattedText(
                    p.Name,
                    System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    this.FontSize,
                    Brushes.Black,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                
                if (formattedText.Width > maxWidth) maxWidth = formattedText.Width;
            }

            // Calculate width to fit: 
            // Max Text Width + ListBox Item Padding (8*2) + ListBox Border + Container Padding (15*2)
            double calculatedWidth = maxWidth + 65; 
            
            // Limit range for usability
            if (calculatedWidth < 200) calculatedWidth = 200;
            if (calculatedWidth > 450) calculatedWidth = 450;

            UI_Column_Left.Width = new GridLength(calculatedWidth);
            UI_Column_Left.MinWidth = calculatedWidth;
        }

        private void UI_List_Parameters_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UI_Combo_Category.SelectedItem is CategoryItem catItem && 
                UI_List_Parameters.SelectedItem is ParameterItem paramItem)
            {
                LoadValues(catItem.Category, paramItem);
            }
        }

        private void LoadValues(Category cat, ParameterItem paramItem)
        {
            Values.Clear();
            
            var collector = new FilteredElementCollector(_doc, _doc.ActiveView.Id)
                .OfCategoryId(cat.Id);

            Dictionary<string, (int Count, double Internal)> data = new Dictionary<string, (int, double)>();

            foreach (Element e in collector)
            {
                // Check Instance first, then Type if needed
                Parameter p = null;
                
                if (paramItem.IsTypeParameter)
                {
                    Element typeElem = _doc.GetElement(e.GetTypeId());
                    if (typeElem != null) p = typeElem.LookupParameter(paramItem.Name);
                }
                else
                {
                    p = e.LookupParameter(paramItem.Name);
                }

                if (p == null) continue;

                double internalVal = (p.StorageType == StorageType.Double) ? p.AsDouble() : 0;
                string val = p.AsValueString() ?? p.AsString();
                
                if (val == null)
                {
                    if (p.StorageType == StorageType.Double) val = p.AsDouble().ToString("F2");
                    else if (p.StorageType == StorageType.Integer) val = p.AsInteger().ToString();
                    else if (p.StorageType == StorageType.ElementId) val = p.AsElementId().ToString();
                    else val = "<null>";
                }
                
                if (string.IsNullOrEmpty(val)) val = "<empty>";

                if (data.ContainsKey(val)) 
                {
                    var existing = data[val];
                    data[val] = (existing.Count + 1, existing.Internal);
                }
                else 
                {
                    data[val] = (1, internalVal);
                }
            }

            foreach (var kvp in data)
            {
                Values.Add(new ValueItem { Value = kvp.Key, Count = kvp.Value.Count, DoubleValue = kvp.Value.Internal, ColorBrush = Brushes.Gray });
            }
            
            RandomizeColors();
        }

        private void RandomizeColors()
        {
            Random r = new Random();
            foreach (var item in Values)
            {
                if (item.Value == "<null>")
                {
                    item.ColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
                    item.RevitColor = new Autodesk.Revit.DB.Color(200, 200, 200);
                }
                else
                {
                    byte red = (byte)r.Next(256);
                    byte green = (byte)r.Next(256);
                    byte blue = (byte)r.Next(256);
                    item.ColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
                    item.RevitColor = new Autodesk.Revit.DB.Color(red, green, blue);
                }
            }
        }

        private void ColorBorder_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is ValueItem item)
            {
                var dialog = new ColorSelectionDialog();

                if (dialog.Show() == ItemSelectionDialogResult.Confirmed)
                {
                    var revitColor = dialog.SelectedColor;
                    item.RevitColor = revitColor;
                    item.ColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(revitColor.Red, revitColor.Green, revitColor.Blue));
                }
            }
        }

        private void GradientColors()
        {
            int total = Values.Count;
            if (total == 0) return;

            for (int i = 0; i < total; i++)
            {
                if (Values[i].Value == "<null>")
                {
                    Values[i].ColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
                    Values[i].RevitColor = new Autodesk.Revit.DB.Color(200, 200, 200);
                    continue;
                }

                byte r = (byte)(255 * (1.0 - (double)i / total));
                byte b = (byte)(255 * ((double)i / total));
                Values[i].ColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, 0, b));
                Values[i].RevitColor = new Autodesk.Revit.DB.Color(r, 0, b);
            }
        }

        private void UI_Btn_Randomize_Click(object sender, RoutedEventArgs e) => RandomizeColors();
        private void UI_Btn_Gradient_Click(object sender, RoutedEventArgs e) => GradientColors();

        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e) 
        {
            _handler.CurrentAction = ColorSplashAction.Apply;
            _applyEvent.Raise();
        }
        
        private void UI_Btn_Reset_Click(object sender, RoutedEventArgs e)
        {
             _handler.CurrentAction = ColorSplashAction.Reset;
             _applyEvent.Raise();
        }

        private void UI_Btn_Legend_Click(object sender, RoutedEventArgs e)
        {
            _handler.CurrentAction = ColorSplashAction.CreateLegend;
            _applyEvent.Raise();
        }

        private void UI_Btn_Filters_Click(object sender, RoutedEventArgs e)
        {
            _handler.CurrentAction = ColorSplashAction.CreateFilters;
            _applyEvent.Raise();
        }

        private void UI_Btn_Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    // Helper Classes
    public class CategoryItem
    {
        public string Name { get; set; }
        public Category Category { get; set; }
    }

    public class ParameterItem
    {
        public string Name { get; set; }
        public StorageType StorageType { get; set; }
        public Definition Definition { get; set; }
        public bool IsTypeParameter { get; set; }
        public ElementId Id { get; set; }
    }

    public class ValueItem : System.ComponentModel.INotifyPropertyChanged
    {
        public string Value { get; set; }
        public double DoubleValue { get; set; }
        public int Count { get; set; }
        
        private SolidColorBrush _colorBrush;
        public SolidColorBrush ColorBrush 
        { 
            get => _colorBrush; 
            set 
            { 
                _colorBrush = value; 
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ColorBrush)));
            } 
        }
        
        public Autodesk.Revit.DB.Color RevitColor { get; set; } = new Autodesk.Revit.DB.Color(128, 128, 128);

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    public class CategoryIdComparer : IEquatable<CategoryIdComparer>
    {
        public Category Category { get; }
        public CategoryIdComparer(Category cat) { Category = cat; }
        public bool Equals(CategoryIdComparer other) => other != null && Category.Id == other.Category.Id;
        public override int GetHashCode() => Category.Id.GetHashCode();
    }
}
