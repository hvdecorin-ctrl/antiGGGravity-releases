using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using antiGGGravity.Commands.ProjectAudit;

namespace antiGGGravity.Views.ProjectAudit
{
    public partial class ProjectTextStyleView : Window
    {
        private readonly ExternalEvent _externalEvent;
        private readonly ProjectTextStyleHandler _handler;
        private readonly Document _doc;

        public ProjectTextStyleView(ExternalEvent externalEvent, ProjectTextStyleHandler handler, Document doc)
        {
            InitializeComponent();
            _externalEvent = externalEvent;
            _handler = handler;
            _doc = doc;
            _handler.StatusCallback = (msg) => Dispatcher.Invoke(() => UI_Status.Text = msg);
            _handler.OperationCompleted = () => Dispatcher.Invoke(() => LoadTextStyles(_doc));

            LoadTextStyles(_doc);
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelAlign == null || PanelConvert == null) return;

            if (TabAlign.IsChecked == true)
            {
                PanelAlign.Visibility = System.Windows.Visibility.Visible;
                PanelConvert.Visibility = System.Windows.Visibility.Collapsed;
            }
            else
            {
                PanelAlign.Visibility = System.Windows.Visibility.Collapsed;
                PanelConvert.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void LoadTextStyles(Document doc)
        {
            var allTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(TextNoteType))
                .Cast<TextNoteType>()
                .ToList();

            // Use a list of unique sizes (internal feet units)
            var uniqueSizes = allTypes
                .Select(x => x.get_Parameter(BuiltInParameter.TEXT_SIZE).AsDouble())
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var items = new List<TextSizeItem>();
            items.Add(new TextSizeItem { Name = "< All Sizes >", Value = null });

            foreach (var feetValue in uniqueSizes)
            {
                double mmValue = feetValue * 304.8;
                // Format to 1 decimal place if it's a common size like 2.5
                string display = mmValue.ToString("F1") + " mm";
                items.Add(new TextSizeItem { Name = display, Value = feetValue });
            }

            ComboTextStyle.ItemsSource = items;
            
            // Set 2.5 mm as default if available
            var defaultSize = items.FirstOrDefault(i => i.Name.Contains("2.5 mm"));
            if (defaultSize != null)
                ComboTextStyle.SelectedItem = defaultSize;
            else
                ComboTextStyle.SelectedIndex = 0;

            // Populate Convert controls
            var styleItems = allTypes
                .OrderBy(x => x.Name)
                .Select(x => new TextStyleItem { Name = x.Name, Id = x.Id })
                .ToList();

            ListConvertFrom.ItemsSource = styleItems;
            ComboConvertTo.ItemsSource = styleItems;
        }

        private class TextStyleItem
        {
            public string Name { get; set; }
            public ElementId Id { get; set; }
        }

        private class TextSizeItem
        {
            public string Name { get; set; }
            public double? Value { get; set; }
        }

        private void ChkToggleAll_Changed(object sender, RoutedEventArgs e)
        {
            if (ModPanel == null || ChkToggleAll == null) return;
            bool isChecked = ChkToggleAll.IsChecked == true;
            foreach (var child in ModPanel.Children)
            {
                if (child is System.Windows.Controls.CheckBox cb) cb.IsChecked = isChecked;
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            // 1. Set Scope (Universal)
            if (RadEntireProject.IsChecked == true)
                _handler.Scope = ApplicationScope.EntireProject;
            else
                _handler.Scope = ApplicationScope.ActiveView;

            // 2. Set Mode and Parameters
            if (TabConvert.IsChecked == true)
            {
                _handler.OperationMode = TextToolMode.Convert;
                
                // Collect Multi-Selected Source IDs
                _handler.SourceStyleIds = ListConvertFrom.SelectedItems
                    .Cast<TextStyleItem>()
                    .Select(x => x.Id)
                    .ToList();

                _handler.TargetStyleId = (ComboConvertTo.SelectedItem as TextStyleItem)?.Id;
                _handler.DeleteSources = ChkDeleteSources.IsChecked == true;

                if (_handler.SourceStyleIds.Count == 0 || _handler.TargetStyleId == null)
                {
                    UI_Status.Text = "Select source(s) and target style.";
                    return;
                }
            }
            else
            {
                _handler.OperationMode = TextToolMode.Align;
                _handler.AlignLeft = ChkAlignLeft.IsChecked == true;
                _handler.LeaderTopLeft = ChkLeaderTopLeft.IsChecked == true;
                _handler.LeaderTopRight = ChkLeaderTopRight.IsChecked == true;

                if (ComboTextStyle.SelectedItem is TextSizeItem selected)
                {
                    _handler.FilterTextSize = selected.Value;
                }
            }

            _externalEvent.Raise();
            UI_Status.Text = "Applying modifications...";
        }
    
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
