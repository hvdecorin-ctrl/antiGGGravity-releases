using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class ModifyWallConstraintsView : Window
    {
        public bool ModifyBase => UI_Check_Base.IsChecked == true;
        public bool ModifyTop => UI_Check_Top.IsChecked == true;
        public Level SelectedBaseLevel => UI_Combo_Base.SelectedItem as Level;
        public Level SelectedTopLevel => UI_Combo_Top.SelectedItem as Level;

        public ModifyWallConstraintsView(Document doc)
        {
            InitializeComponent();
            var levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();
            
            UI_Combo_Base.ItemsSource = levels;
            UI_Combo_Base.DisplayMemberPath = "Name";
            UI_Combo_Top.ItemsSource = levels;
            UI_Combo_Top.DisplayMemberPath = "Name";
            
            if (levels.Any())
            {
                UI_Combo_Base.SelectedIndex = 0;
                UI_Combo_Top.SelectedIndex = levels.Count > 1 ? levels.Count - 1 : 0;
            }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            if (!ModifyBase && !ModifyTop)
            {
                MessageBox.Show("Please select at least one constraint to modify.", "Modify Wall Constraints");
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
