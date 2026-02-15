using System;
using System.Windows;
using Autodesk.Revit.DB;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views.Model
{
    public partial class BracingParametersView : Window
    {
        private Document _doc;
        private string _mode;
        private const string VIEW_NAME = "BracingTool";

        public double OffsetMm { get; private set; } = 0;
        public int NumBraces { get; private set; } = 3;

        public BracingParametersView(Document doc, string mode)
        {
            InitializeComponent();
            _doc = doc;
            _mode = mode;
            UI_Title.Text = mode.ToUpper() + " BRACING";
            
            ConfigureUI();
            LoadSettings();
        }

        private void ConfigureUI()
        {
            if (_mode == "H-Frame")
            {
                UI_Panel_HFrame.Visibility = System.Windows.Visibility.Visible;
            }
            else if (_mode == "K-Brace")
            {
                UI_K_Instructions.Visibility = System.Windows.Visibility.Visible;
            }
        }

        private void LoadSettings()
        {
            UI_Text_Offset.Text = SettingsManager.Get(VIEW_NAME, "Offset", "0");
            UI_Text_Count.Text = SettingsManager.Get(VIEW_NAME, "Count", "3");
        }

        private void SaveSettings()
        {
            SettingsManager.Set(VIEW_NAME, "Offset", UI_Text_Offset.Text);
            SettingsManager.Set(VIEW_NAME, "Count", UI_Text_Count.Text);
            SettingsManager.SaveAll();
        }

        private void UI_Btn_Generate_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(UI_Text_Offset.Text, out double offset))
            {
                MessageBox.Show("Please enter a valid offset value.");
                return;
            }
            OffsetMm = offset;

            if (_mode == "H-Frame")
            {
                if (!int.TryParse(UI_Text_Count.Text, out int count) || count < 1)
                {
                    MessageBox.Show("Please enter a valid brace count (>= 1).");
                    return;
                }
                NumBraces = count;
            }

            SaveSettings();
            this.DialogResult = true;
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
    }
}
