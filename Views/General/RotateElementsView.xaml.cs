using System;
using System.Windows;

namespace antiGGGravity.Views.General
{
    public partial class RotateElementsView : Window
    {
        public double AngleRadians { get; private set; }

        public RotateElementsView()
        {
            InitializeComponent();
            UI_Text_Angle.Focus();
            UI_Text_Angle.SelectAll();
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(UI_Text_Angle.Text, out double angleDegrees))
            {
                AngleRadians = angleDegrees * Math.PI / 180.0;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please enter a valid numeric angle.", "Rotate Elements");
            }
        }
    }
}
