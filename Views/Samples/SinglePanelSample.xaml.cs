using System.Windows;

namespace antiGGGravity.Views.Samples
{
    public partial class SinglePanelSample : Window
    {
        public SinglePanelSample()
        {
            try { InitializeComponent(); }
            catch (System.Exception ex) { MessageBox.Show(ex.ToString(), "Single Panel Init Error"); }
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UI_Btn_Apply_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Confirmed (Sample Only)", "Single Panel Sample");
            Close();
        }
    }
}
