using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class ColumnRebarWindow : Window, IRebarWindow
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;

        public ElementHostType SelectedHostType => ElementHostType.Column;

        private ColumnRebarPanel _columnPanel;
        private CircularColumnPanel _circularPanel;

        public ColumnRebarWindow(UIDocument uiDoc, ExternalEvent externalEvent)
        {
            _doc = uiDoc.Document;
            _externalEvent = externalEvent;
            
            // Merge shared resources before initializing component to prevent parsing delay
            this.Resources.MergedDictionaries.Add(SharedResources.GlobalResources);
            
            InitializeComponent();

            _columnPanel = new ColumnRebarPanel(_doc);
            UI_PanelHost.Content = _columnPanel;
        }

        private void ElementType_Changed(object sender, RoutedEventArgs e)
        {
            if (_doc == null || UI_PanelHost == null) return;
            SaveActivePanel();

            if (UI_Radio_Column?.IsChecked == true)
            {
                if (_columnPanel == null) _columnPanel = new ColumnRebarPanel(_doc);
                UI_PanelHost.Content = _columnPanel;
            }
            else if (UI_Radio_CircularColumn?.IsChecked == true)
            {
                if (_circularPanel == null) _circularPanel = new CircularColumnPanel(_doc);
                UI_PanelHost.Content = _circularPanel;
            }
        }

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e) { SaveActivePanel(); Hide(); _externalEvent?.Raise(); }
        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e) { SaveActivePanel(); Close(); }

        public void ReShow() => Dispatcher.Invoke(() => { Show(); Activate(); });

        private void SaveActivePanel()
        {
            _columnPanel?.SaveSettings();
            _circularPanel?.SaveSettings();
        }

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
        public bool EnableLapSplice => UI_Check_CutRebar.IsChecked == true;
        public bool IsCircularColumn => UI_Radio_CircularColumn?.IsChecked == true;
        
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
            _columnPanel?.UpdateZoneInfo(DesignCode);
        }

        public BeamRebarPanel BeamPanel => null;
        public WallRebarPanel WallPanel => null;
        public ColumnRebarPanel ColumnPanel => _columnPanel;
        public CircularColumnPanel CircularColumnPanel => _circularPanel;
        public StripFootingRebarPanel StripFootingPanel => null;
        public FootingPadRebarPanel FootingPadPanel => null;
        public PadShapeRebarPanel PadShapePanel => null;
        public BoredPileRebarPanel BoredPilePanel => null;
        public WallCornerLPanel WallCornerLPanel => null;
        public WallCornerUPanel WallCornerUPanel => null;
        public BeamAdvancePanel BeamAdvancePanel => null;

        public BeamRebarPanel GetOrCreateBeamPanel() => null;
    }
}
