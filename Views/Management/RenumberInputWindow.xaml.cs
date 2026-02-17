using System.Windows;

namespace antiGGGravity.Views.Management
{
    public partial class RenumberInputWindow : Window
    {
        public string InputValue { get; private set; }

        public RenumberInputWindow()
        {
            InitializeComponent();
            InputTextBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputValue = InputTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
