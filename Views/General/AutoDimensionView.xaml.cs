using System.Windows;
using antiGGGravity.Commands.General.AutoDimension;
using antiGGGravity.Utilities;

namespace antiGGGravity.Views.General
{
    public partial class AutoDimensionView : Window
    {
        private const string VIEW_NAME = "AutoDims";

        public AutoDimSettings Settings { get; private set; }

        public AutoDimensionView()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            UI_Chk_Grids.IsChecked = SettingsManager.GetBool(VIEW_NAME, "DimGrids", true);
            UI_Chk_Walls.IsChecked = SettingsManager.GetBool(VIEW_NAME, "DimWalls", true);
            UI_Chk_Columns.IsChecked = SettingsManager.GetBool(VIEW_NAME, "DimColumns", true);
            UI_Chk_Foundations.IsChecked = SettingsManager.GetBool(VIEW_NAME, "DimFoundations", true);

            UI_Txt_Offset1.Text = SettingsManager.Get(VIEW_NAME, "Offset1", "1000");
            UI_Txt_Offset2.Text = SettingsManager.Get(VIEW_NAME, "Offset2", "800");
            UI_Txt_ChainOffset.Text = SettingsManager.Get(VIEW_NAME, "ChainOffset", "500");
            UI_Txt_ChainGap.Text = SettingsManager.Get(VIEW_NAME, "ChainGap", "800");
            UI_Txt_ZeroTol.Text = SettingsManager.Get(VIEW_NAME, "ZeroTol", "5");
            UI_Txt_IntersectTol.Text = SettingsManager.Get(VIEW_NAME, "IntersectTol", "50");
        }

        private void SaveSettings()
        {
            SettingsManager.Set(VIEW_NAME, "DimGrids", (UI_Chk_Grids.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "DimWalls", (UI_Chk_Walls.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "DimColumns", (UI_Chk_Columns.IsChecked == true).ToString());
            SettingsManager.Set(VIEW_NAME, "DimFoundations", (UI_Chk_Foundations.IsChecked == true).ToString());

            SettingsManager.Set(VIEW_NAME, "Offset1", UI_Txt_Offset1.Text);
            SettingsManager.Set(VIEW_NAME, "Offset2", UI_Txt_Offset2.Text);
            SettingsManager.Set(VIEW_NAME, "ChainOffset", UI_Txt_ChainOffset.Text);
            SettingsManager.Set(VIEW_NAME, "ChainGap", UI_Txt_ChainGap.Text);
            SettingsManager.Set(VIEW_NAME, "ZeroTol", UI_Txt_ZeroTol.Text);
            SettingsManager.Set(VIEW_NAME, "IntersectTol", UI_Txt_IntersectTol.Text);

            SettingsManager.SaveAll();
        }

        private void UI_Btn_Cancel_Click(object sender, RoutedEventArgs e) => Close();

        private void UI_Btn_Run_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            double.TryParse(UI_Txt_Offset1.Text, out double offset1);
            double.TryParse(UI_Txt_Offset2.Text, out double offset2);
            double.TryParse(UI_Txt_ChainOffset.Text, out double chainOffset);
            double.TryParse(UI_Txt_ChainGap.Text, out double chainGap);
            double.TryParse(UI_Txt_ZeroTol.Text, out double zeroTol);
            double.TryParse(UI_Txt_IntersectTol.Text, out double intersectTol);

            Settings = new AutoDimSettings
            {
                DimGrids = UI_Chk_Grids.IsChecked == true,
                DimWalls = UI_Chk_Walls.IsChecked == true,
                DimColumns = UI_Chk_Columns.IsChecked == true,
                DimFoundations = UI_Chk_Foundations.IsChecked == true,

                Offset1Mm = offset1 > 0 ? offset1 : 1000,
                Offset2Mm = offset2 > 0 ? offset2 : 800,
                OffsetChain1Mm = chainOffset > 0 ? chainOffset : 500,
                OffsetChainGapMm = chainGap > 0 ? chainGap : 800,
                ZeroTolMm = zeroTol > 0 ? zeroTol : 5,
                IntersectTolMm = intersectTol > 0 ? intersectTol : 50,
            };

            DialogResult = true;
            Close();
        }
    }
}
