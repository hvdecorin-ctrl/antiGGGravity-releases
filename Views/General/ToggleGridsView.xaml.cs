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
            SetupEventHandlers();
            UpdateStatus();
        }

        private void SetupEventHandlers()
        {
            // Wire up checkbox changes for status updates
            UI_Check_Top.Checked += (s, e) => UpdateStatus();
            UI_Check_Top.Unchecked += (s, e) => UpdateStatus();
            UI_Check_Bottom.Checked += (s, e) => UpdateStatus();
            UI_Check_Bottom.Unchecked += (s, e) => UpdateStatus();
            UI_Check_Left.Checked += (s, e) => UpdateStatus();
            UI_Check_Left.Unchecked += (s, e) => UpdateStatus();
            UI_Check_Right.Checked += (s, e) => UpdateStatus();
            UI_Check_Right.Unchecked += (s, e) => UpdateStatus();
        }

        private void UpdateStatus()
        {
            int count = 0;
            if (UI_Check_Top.IsChecked == true) count++;
            if (UI_Check_Bottom.IsChecked == true) count++;
            if (UI_Check_Left.IsChecked == true) count++;
            if (UI_Check_Right.IsChecked == true) count++;

            if (count == 0) UI_Status_Text.Text = "Select directions to toggle";
            else UI_Status_Text.Text = $"{count} direction(s) selected";

            // Sync Select All state without triggering Click event
            bool allChecked = (UI_Check_Top.IsChecked == true && 
                               UI_Check_Bottom.IsChecked == true && 
                               UI_Check_Left.IsChecked == true && 
                               UI_Check_Right.IsChecked == true);
            
            // Unwire temporarily to avoid recursion if needed, but Click only fires on user click
            UI_Check_All.IsChecked = allChecked;
        }

        private void UI_Check_All_Click(object sender, RoutedEventArgs e)
        {
            bool state = UI_Check_All.IsChecked == true;
            UI_Check_Top.IsChecked = state;
            UI_Check_Bottom.IsChecked = state;
            UI_Check_Left.IsChecked = state;
            UI_Check_Right.IsChecked = state;
            UpdateStatus();
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

            DialogResult = true;
            Close();
        }
    }
}
