using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;

namespace antiGGGravity.Views.General
{
    public partial class LineStyleSelectionView : Window
    {
        public GraphicsStyle SelectedStyle => UI_List_Styles.SelectedItem as GraphicsStyle;

        public LineStyleSelectionView(IEnumerable<GraphicsStyle> styles)
        {
            InitializeComponent();
            
            // Order and bind styles
            UI_List_Styles.ItemsSource = styles.OrderBy(s => s.Name).ToList();
            
            if (UI_List_Styles.Items.Count > 0)
                UI_List_Styles.SelectedIndex = 0;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStyle == null)
            {
                MessageBox.Show("Please select a line style from the list.", "antiGGGravity");
                return;
            }
            DialogResult = true;
            Close();
        }

        private void UI_List_Styles_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedStyle != null)
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
