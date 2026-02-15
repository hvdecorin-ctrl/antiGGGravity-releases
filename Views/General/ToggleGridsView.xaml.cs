using System;
using System.Collections.Generic;
using System.Windows;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class ToggleGridsView : Window
    {
        public List<string> SelectedDirections { get; private set; }

        public ToggleGridsView()
        {
            InitializeComponent();
        }

        private void UI_Check_All_Click(object sender, RoutedEventArgs e)
        {
            bool state = UI_Check_All.IsChecked == true;
            UI_Check_Top.IsChecked = state;
            UI_Check_Bottom.IsChecked = state;
            UI_Check_Left.IsChecked = state;
            UI_Check_Right.IsChecked = state;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            SelectedDirections = new List<string>();
            if (UI_Check_Top.IsChecked == true) SelectedDirections.Add("Top");
            if (UI_Check_Bottom.IsChecked == true) SelectedDirections.Add("Bottom");
            if (UI_Check_Left.IsChecked == true) SelectedDirections.Add("Left");
            if (UI_Check_Right.IsChecked == true) SelectedDirections.Add("Right");

            if (SelectedDirections.Count == 0)
            {
                MessageBox.Show("Please select at least one direction.", "Toggle Grids");
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
