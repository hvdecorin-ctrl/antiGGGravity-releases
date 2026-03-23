using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;
using antiGGGravity.Utilities;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class FoundationRebarWindow : Window, IRebarWindow
    {
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;

        public ElementHostType SelectedHostType { get; private set; } = ElementHostType.StripFooting;

        private StripFootingRebarPanel _stripFootingPanel;
        private FootingPadRebarPanel _footingPadPanel;
        private PadShapeRebarPanel _padShapePanel;
        private BoredPileRebarPanel _boredPilePanel;

        public FoundationRebarWindow(UIDocument uiDoc, ExternalEvent externalEvent)
        {
            _doc = uiDoc.Document;
            _externalEvent = externalEvent;
            
            // Merge shared resources before initializing component to prevent parsing delay
            this.Resources.MergedDictionaries.Add(SharedResources.GlobalResources);
            
            InitializeComponent();

            _stripFootingPanel = new StripFootingRebarPanel(_doc);
            UI_PanelHost.Content = _stripFootingPanel;
        }

        private void ElementType_Changed(object sender, RoutedEventArgs e)
        {
            if (_doc == null || UI_PanelHost == null) return;
            SaveActivePanel();

            if (UI_Radio_StripFooting?.IsChecked == true)
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
            else if (UI_Radio_PadShape?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.PadShape;
                if (_padShapePanel == null) _padShapePanel = new PadShapeRebarPanel(_doc);
                UI_PanelHost.Content = _padShapePanel;
            }
            else if (UI_Radio_BoredPile?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.BoredPile;
                if (_boredPilePanel == null) _boredPilePanel = new BoredPileRebarPanel(_doc);
                UI_PanelHost.Content = _boredPilePanel;
            }
        }

        private void UI_Button_Generate_Click(object sender, RoutedEventArgs e)
        {
            SaveActivePanel();
            Hide();
            _externalEvent?.Raise();
        }

        private void UI_Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            SaveActivePanel();
            Close();
        }

        public void ReShow() => Dispatcher.Invoke(() => { Show(); Activate(); });

        private void SaveActivePanel()
        {
            if (UI_PanelHost.Content is StripFootingRebarPanel sfp) sfp.SaveSettings();
            else if (UI_PanelHost.Content is FootingPadRebarPanel fpp) fpp.SaveSettings();
            else if (UI_PanelHost.Content is PadShapeRebarPanel psp) psp.SaveSettings();
            else if (UI_PanelHost.Content is BoredPileRebarPanel bpp) bpp.SaveSettings();
        }

        private void DesignCode_Changed(object sender, SelectionChangedEventArgs e) { }

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
        public bool EnableLapSplice => UI_Check_CutRebar.IsChecked == true;
        public bool IsCircularColumn => false;
        public DesignCodeStandard DesignCode => DesignCodeStandard.ACI318; // Default

        public BeamRebarPanel BeamPanel => null;
        public WallRebarPanel WallPanel => null;
        public ColumnRebarPanel ColumnPanel => null;
        public StripFootingRebarPanel StripFootingPanel => _stripFootingPanel;
        public FootingPadRebarPanel FootingPadPanel => _footingPadPanel;
        public PadShapeRebarPanel PadShapePanel => _padShapePanel;
        public BoredPileRebarPanel BoredPilePanel => _boredPilePanel;
        public CircularColumnPanel CircularColumnPanel => null;
        public WallCornerLPanel WallCornerLPanel => null;
        public WallCornerUPanel WallCornerUPanel => null;
        public BeamAdvancePanel BeamAdvancePanel => null;

        public BeamRebarPanel GetOrCreateBeamPanel() => null;
    }
}
