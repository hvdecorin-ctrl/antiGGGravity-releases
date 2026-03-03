using System.Windows;
using System.Windows.Controls;

namespace antiGGGravity.Views.Rebar
{
    public partial class AssignMarkView : Window
    {
        public enum NamingMode { TypeMark, CustomName, TypeMarkXY }
        public enum NumberingRule { Auto, Manual }
        public enum ScopeOption { ActiveView, EntireProject }

        // Expose selected options
        public NamingMode SelectedNamingMode =>
            UI_Radio_TypeMark.IsChecked == true ? NamingMode.TypeMark :
            UI_Radio_TypeMarkXY.IsChecked == true ? NamingMode.TypeMarkXY :
            NamingMode.CustomName;
        public NumberingRule SelectedNumberingRule => UI_Radio_Auto.IsChecked == true ? NumberingRule.Auto : NumberingRule.Manual;
        public ScopeOption SelectedScope => UI_Radio_ActiveView.IsChecked == true ? ScopeOption.ActiveView : ScopeOption.EntireProject;
        public string CustomNamePrefix => UI_TextBox_CustomName.Text?.Trim() ?? "";

        public string SelectedPrefixSource
        {
            get
            {
                var item = UI_Combo_PrefixSource.SelectedItem as ComboBoxItem;
                return item?.Tag as string ?? "TypeMark";
            }
        }

        /// <summary>
        /// Returns the Tag string of the selected category (e.g. "OST_Walls"), or null for "All Categories".
        /// </summary>
        public string SelectedCategoryTag
        {
            get
            {
                var item = UI_Combo_Category.SelectedItem as ComboBoxItem;
                return item?.Tag as string;
            }
        }

        public bool UserConfirmed { get; private set; } = false;

        public AssignMarkView()
        {
            InitializeComponent();
        }

        private void UI_NamingMode_Changed(object sender, RoutedEventArgs e)
        {
            if (UI_CustomName_Panel != null)
            {
                UI_CustomName_Panel.Visibility = UI_Radio_CustomName.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
        }

        private void UI_Close_Click(object sender, RoutedEventArgs e)
        {
            UserConfirmed = false;
            Close();
        }

        private void UI_Assign_Click(object sender, RoutedEventArgs e)
        {
            // Validate custom name if selected
            if (SelectedNamingMode == NamingMode.CustomName && string.IsNullOrWhiteSpace(CustomNamePrefix))
            {
                MessageBox.Show("Please enter a custom name prefix.", "Assign Element Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                UI_TextBox_CustomName.Focus();
                return;
            }

            UserConfirmed = true;
            Close();
        }
    }
}
