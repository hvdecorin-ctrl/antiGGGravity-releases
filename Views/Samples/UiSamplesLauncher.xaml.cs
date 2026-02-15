using System.Windows;

namespace antiGGGravity.Views.Samples
{
    public partial class UiSamplesLauncher : Window
    {
        public UiSamplesLauncher()
        {
            try { InitializeComponent(); }
            catch (System.Exception ex) { MessageBox.Show(ex.ToString(), "Launcher Init Error"); }
        }

        private void BtnSingle_Click(object sender, RoutedEventArgs e)
        {
            new SinglePanelSample().Show();
        }

        private void BtnDual_Click(object sender, RoutedEventArgs e)
        {
            new DualPanelSample().Show();
        }

        private void BtnTriple_Click(object sender, RoutedEventArgs e)
        {
            new TriplePanelSample().Show();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
