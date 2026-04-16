using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;
using antiGGGravity.Utilities;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class WallRebarWindow : Window, IRebarWindow
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;

        public ElementHostType SelectedHostType { get; private set; } = ElementHostType.Wall;

        private WallRebarPanel _wallPanel;
        private WallCornerLPanel _wallCornerLPanel;
        private WallCornerUPanel _wallCornerUPanel;

        public WallRebarWindow(UIDocument uiDoc, ExternalEvent externalEvent)
        {
            _doc = uiDoc.Document;
            _externalEvent = externalEvent;
            
            // Merge shared resources before initializing component to prevent parsing delay
            this.Resources.MergedDictionaries.Add(SharedResources.GlobalResources);
            
            InitializeComponent();

            _wallPanel = new WallRebarPanel(_doc);
            UI_PanelHost.Content = _wallPanel;
        }

        private void ElementType_Changed(object sender, RoutedEventArgs e)
        {
            if (_doc == null || UI_PanelHost == null) return;
            SaveActivePanel();

            if (UI_Radio_WallStandard?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.Wall;
                if (_wallPanel == null) _wallPanel = new WallRebarPanel(_doc);
                UI_PanelHost.Content = _wallPanel;
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

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e) { SaveActivePanel(); Hide(); _externalEvent?.Raise(); }
        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e) { SaveActivePanel(); Close(); }

        public void ReShow() => Dispatcher.Invoke(() => { Show(); Activate(); });

        private void SaveActivePanel()
        {
            if (UI_PanelHost.Content is WallRebarPanel wp) wp.SaveSettings();
            else if (UI_PanelHost.Content is WallCornerLPanel wcl) wcl.SaveSettings();
            else if (UI_PanelHost.Content is WallCornerUPanel wcu) wcu.SaveSettings();
        }

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
        public bool EnableLapSplice => UI_Check_CutRebar.IsChecked == true;
        public bool IsCircularColumn => false;

        public DesignCodeStandard DesignCode
        {
            get
            {
                if (UI_Combo_DesignCode.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                {
                    string content = item.Content.ToString();
                    if (content == "ACI 318") return DesignCodeStandard.ACI318;
                    if (content == "AS 3600") return DesignCodeStandard.AS3600;
                    if (content == "Eurocode 2") return DesignCodeStandard.EC2;
                    if (content == "NZS 3101") return DesignCodeStandard.NZS3101;
                }
                return DesignCodeStandard.Custom;
            }
        }

        private void DesignCode_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_doc == null) return;
            // Walls typically don't use confinement zones that auto-update based on design code.
            // If future code requires applying design code to walls, implement it here.
        }

        public BeamRebarPanel BeamPanel => null;
        public WallRebarPanel WallPanel => _wallPanel;
        public ColumnRebarPanel ColumnPanel => null;
        public StripFootingRebarPanel StripFootingPanel => null;
        public FootingPadRebarPanel FootingPadPanel => null;
        public PadShapeRebarPanel PadShapePanel => null;
        public BoredPileRebarPanel BoredPilePanel => null;
        public CircularColumnPanel CircularColumnPanel => null;
        public WallCornerLPanel WallCornerLPanel => _wallCornerLPanel;
        public WallCornerUPanel WallCornerUPanel => _wallCornerUPanel;
        public BeamAdvancePanel BeamAdvancePanel => null;

        public BeamRebarPanel GetOrCreateBeamPanel() => null;
    }
}
