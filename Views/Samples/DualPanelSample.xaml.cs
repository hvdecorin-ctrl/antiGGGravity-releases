using System.Windows;

namespace antiGGGravity.Views.Samples
{
    public partial class DualPanelSample : Window
    {
        public DualPanelSample()
        {
            try { InitializeComponent(); }
            catch (System.Exception ex) { MessageBox.Show(ex.ToString(), "Dual Panel Init Error"); }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Transfer Complete (Sample Only)", "Dual Panel Sample");
            Close();
        }
    }
}
