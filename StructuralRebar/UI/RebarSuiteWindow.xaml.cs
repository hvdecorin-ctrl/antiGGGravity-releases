using System.Windows;
using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class RebarSuiteWindow : Window
    {
        private readonly Document _doc;
        public bool IsConfirmed { get; private set; } = false;
        public ElementHostType SelectedHostType { get; private set; } = ElementHostType.Beam;

        // Active panels
        private BeamRebarPanel _beamPanel;
        private WallRebarPanel _wallPanel;
        private ColumnRebarPanel _columnPanel;
        private StripFootingRebarPanel _stripFootingPanel;
        private FootingPadRebarPanel _footingPadPanel;
        private WallCornerLPanel _wallCornerLPanel;
        private WallCornerUPanel _wallCornerUPanel;
        
        public RebarSuiteWindow(Document doc)
        {
            _doc = doc;
            InitializeComponent();

            // Initialize Beam panel (default) — after InitializeComponent so _doc is set
            _beamPanel = new BeamRebarPanel(_doc);
            UI_PanelHost.Content = _beamPanel;
        }

        private void ElementType_Changed(object sender, RoutedEventArgs e)
        {
            // Guard: this fires during XAML parse before _doc or UI components are set
            if (_doc == null || UI_PanelHost == null) return;

            if (UI_Radio_Beam?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.Beam;
                if (_beamPanel == null) _beamPanel = new BeamRebarPanel(_doc);
                UI_PanelHost.Content = _beamPanel;
            }
            else if (UI_Radio_Wall?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.Wall;
                if (_wallPanel == null) _wallPanel = new WallRebarPanel(_doc);
                UI_PanelHost.Content = _wallPanel;
            }
            else if (UI_Radio_Column?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.Column;
                if (_columnPanel == null) _columnPanel = new ColumnRebarPanel(_doc);
                UI_PanelHost.Content = _columnPanel;
            }
            else if (UI_Radio_StripFooting?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.StripFooting;
                if (_stripFootingPanel == null) _stripFootingPanel = new StripFootingRebarPanel(_doc);
                UI_PanelHost.Content = _stripFootingPanel;
            }
            else if (UI_Radio_FootingPad?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.FootingPad;
                if (_footingPadPanel == null) _footingPadPanel = new FootingPadRebarPanel(_doc);
                UI_PanelHost.Content = _footingPadPanel;
            }
            else if (UI_Radio_WallCornerL?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.WallCornerL;
                if (_wallCornerLPanel == null) _wallCornerLPanel = new WallCornerLPanel(_doc);
                UI_PanelHost.Content = _wallCornerLPanel;
            }
            else if (UI_Radio_WallCornerU?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.WallCornerU;
                if (_wallCornerUPanel == null) _wallCornerUPanel = new WallCornerUPanel(_doc);
                UI_PanelHost.Content = _wallCornerUPanel;
            }
            // Future: Wall, Column, etc. panels will be added here
        }

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e)
        {
            // Save settings on active panel
            if (UI_PanelHost.Content is BeamRebarPanel bp) bp.SaveSettings();
            // WallRebarPanel doesn't have SaveSettings yet (using direct DTO extraction), 
            // but we can add it if we implement local persistence later.

            IsConfirmed = true;
            Close();
        }

        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            Close();
        }

        // --- Accessors for the engine ---
        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
        public BeamRebarPanel BeamPanel => _beamPanel;
        public WallRebarPanel WallPanel => _wallPanel;
        public ColumnRebarPanel ColumnPanel => _columnPanel;
        public StripFootingRebarPanel StripFootingPanel => _stripFootingPanel;
        public FootingPadRebarPanel FootingPadPanel => _footingPadPanel;
        public WallCornerLPanel WallCornerLPanel => _wallCornerLPanel;
        public WallCornerUPanel WallCornerUPanel => _wallCornerUPanel;
    }
}
