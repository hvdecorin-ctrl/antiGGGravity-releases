using System.Windows;
using System.Windows.Controls;

namespace antiGGGravity.Views.Overrides
{
    public partial class CadStyleSelectionView : Window
    {
        public int SelectedStyle { get; private set; } = -1;
        public bool ApplyToProject { get; private set; } = false;
        public bool IsCancelled { get; private set; } = true;

        public CadStyleSelectionView()
        {
            InitializeComponent();
        }

        private void Style_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && int.TryParse(btn.Tag?.ToString(), out int styleIndex))
            {
                SelectedStyle = styleIndex;
                
                // Highlight selected button
                foreach (var child in ((StackPanel)btn.Parent).Children)
                {
                    if (child is Button b) b.Opacity = 0.6;
                }
                btn.Opacity = 1.0;
                btn.BorderBrush = System.Windows.Media.Brushes.DeepSkyBlue;
            }
        }

        private void Scope_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedStyle == -1)
            {
                MessageBox.Show("Please select a style first.");
                return;
            }

            if (sender is Button btn)
            {
                ApplyToProject = btn.Tag?.ToString() == "Project";
                IsCancelled = false;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsCancelled = true;
            this.DialogResult = false;
            this.Close();
        }
    }
}
