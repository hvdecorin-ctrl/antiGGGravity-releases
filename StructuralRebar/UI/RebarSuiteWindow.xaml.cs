using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class RebarSuiteWindow : Window
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;

        public ElementHostType SelectedHostType { get; private set; } = ElementHostType.StripFooting;

        // Active panels
        private BeamRebarPanel _beamPanel;
        private WallRebarPanel _wallPanel;
        private ColumnRebarPanel _columnPanel;
        private StripFootingRebarPanel _stripFootingPanel;
        private FootingPadRebarPanel _footingPadPanel;
        private WallCornerLPanel _wallCornerLPanel;
        private WallCornerUPanel _wallCornerUPanel;
        
        public RebarSuiteWindow(Document doc, ExternalEvent externalEvent)
        {
            _doc = doc;
            _externalEvent = externalEvent;
            InitializeComponent();

            // Initialize Strip Footing panel (default)
            _stripFootingPanel = new StripFootingRebarPanel(_doc);
            UI_PanelHost.Content = _stripFootingPanel;
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
        }

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e)
        {
            // Save settings on active panel
            SaveActivePanel();

            // Hide the window so the user can interact with Revit (select elements)
            Hide();

            // Raise the external event — Revit will call our handler on the main thread
            _externalEvent?.Raise();
        }

        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            // Save settings before closing
            SaveActivePanel();
            Close();
        }

        /// <summary>
        /// Called by RebarGenerateHandler after execution to bring the window back.
        /// </summary>
        public void ReShow()
        {
            // Must dispatch to the WPF thread since the handler runs on the Revit thread
            Dispatcher.Invoke(() =>
            {
                Show();
                Activate();
            });
        }

        private void SaveActivePanel()
        {
            if (UI_PanelHost.Content is BeamRebarPanel bp) bp.SaveSettings();
            else if (UI_PanelHost.Content is WallRebarPanel wp) wp.SaveSettings();
            else if (UI_PanelHost.Content is ColumnRebarPanel cp) cp.SaveSettings();
            else if (UI_PanelHost.Content is StripFootingRebarPanel sfp) sfp.SaveSettings();
            else if (UI_PanelHost.Content is FootingPadRebarPanel fpp) fpp.SaveSettings();
            else if (UI_PanelHost.Content is WallCornerLPanel wcl) wcl.SaveSettings();
            else if (UI_PanelHost.Content is WallCornerUPanel wcu) wcu.SaveSettings();
        }

        // --- Accessors for the handler ---
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
