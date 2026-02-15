using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class GeometricConversionView : Window
    {
        public ElementType SelectedType => UI_Combo_Type.SelectedItem as ElementType;
        public Level SelectedLevel => UI_Combo_Level.SelectedItem as Level;
        public double Offset { get; private set; }

        public GeometricConversionView(Document doc, string title, string label, IEnumerable<ElementType> types)
        {
            InitializeComponent();
            UI_Title.Text = title.ToUpper();
            UI_Label_Type.Text = label;
            
            UI_Combo_Type.ItemsSource = types.OrderBy(t => t.Name).ToList();
            UI_Combo_Type.DisplayMemberPath = "Name";
            
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            UI_Combo_Level.ItemsSource = levels;
            UI_Combo_Level.DisplayMemberPath = "Name";

            if (levels.Any()) UI_Combo_Level.SelectedItem = doc.ActiveView.GenLevel ?? levels.First();
            if (types.Any()) UI_Combo_Type.SelectedIndex = 0;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(UI_Text_Offset.Text, out double offsetMm))
            {
                Offset = offsetMm / 304.8;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid numeric value for the offset.", "Geometric Conversion");
            }
        }
    }
}
