using System.Windows;

namespace antiGGGravity.Views.Samples
{
    public partial class TriplePanelSample : Window
    {
        public TriplePanelSample()
        {
            try { InitializeComponent(); }
            catch (System.Exception ex) { MessageBox.Show(ex.ToString(), "Triple Panel Init Error"); }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Process Started (Sample Only)", "Triple Panel Sample");
            Close();
        }
    }
}
