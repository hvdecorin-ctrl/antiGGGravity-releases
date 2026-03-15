using System;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;

namespace antiGGGravity.StructuralRebar.UI
{
    public partial class BeamRebarWindow : Window, IRebarWindow
    {
        private readonly UIDocument _uiDoc;
        private readonly Document _doc;
        private readonly ExternalEvent _externalEvent;

        public ElementHostType SelectedHostType { get; private set; } = ElementHostType.Beam;

        private BeamRebarPanel _beamPanel;
        private BeamAdvancePanel _beamAdvancePanel;

        // IRebarWindow interface implementations for panels not hosted in this window
        public WallRebarPanel WallPanel => null;
        public ColumnRebarPanel ColumnPanel => null;
        public StripFootingRebarPanel StripFootingPanel => null;
        public CircularColumnPanel CircularColumnPanel => null;
        public FootingPadRebarPanel FootingPadPanel => null;
        public PadShapeRebarPanel PadShapePanel => null;
        public BoredPileRebarPanel BoredPilePanel => null;
        public WallCornerLPanel WallCornerLPanel => null;
        public WallCornerUPanel WallCornerUPanel => null;

        public BeamRebarWindow(UIDocument uiDoc, ExternalEvent externalEvent)
        {
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
            _externalEvent = externalEvent;
            
            // Merge shared resources before initializing component to prevent parsing delay
            this.Resources.MergedDictionaries.Add(SharedResources.GlobalResources);
            
            InitializeComponent();

            // Default panel
            _beamPanel = new BeamRebarPanel(_doc);
            UI_PanelHost.Content = _beamPanel;
        }

        private void ElementType_Changed(object sender, RoutedEventArgs e)
        {
            if (_doc == null || UI_PanelHost == null) return;

            SaveActivePanel();

            if (UI_Radio_Beam?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.Beam;
                if (_beamPanel == null) _beamPanel = new BeamRebarPanel(_doc);
                _beamPanel.UpdateZoneInfo(DesignCode);
                UI_PanelHost.Content = _beamPanel;
            }
            else if (UI_Radio_BeamAdvance?.IsChecked == true)
            {
                SelectedHostType = ElementHostType.BeamAdvance;
                if (_beamAdvancePanel == null) _beamAdvancePanel = new BeamAdvancePanel(_uiDoc, this);
                UI_PanelHost.Content = _beamAdvancePanel;
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

        private void UI_Button_Detailing_Click(object sender, RoutedEventArgs e)
        {
            var refWindow = new DesignCodeReferenceWindow();
            // Set UI_Tabs to index 3 (Beam Detailing tab)
            if (refWindow.FindName("UI_Tabs") is System.Windows.Controls.TabControl tabs)
            {
                tabs.SelectedIndex = 3;
            }
            
            // Set owner to this window to keep it on top
            refWindow.Owner = this;
            refWindow.Show();
        }

        public void ReShow()
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                Activate();
            });
        }

        private void SaveActivePanel()
        {
            if (UI_PanelHost.Content is BeamRebarPanel bp) bp.SaveSettings();
            else if (UI_PanelHost.Content is BeamAdvancePanel bA) bA.SaveSettings();
        }

        private void DesignCode_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_doc == null) return;
            _beamPanel?.UpdateZoneInfo(DesignCode);
        }

        public bool RemoveExisting => UI_Check_RemoveExisting.IsChecked == true;
        public bool EnableLapSplice => UI_Check_CutRebar.IsChecked == true;
        public bool IsCircularColumn => false;

        public DesignCodeStandard DesignCode
        {
            get
            {
                if (UI_Combo_DesignCode.SelectedItem is ComboBoxItem item)
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

        public BeamRebarPanel BeamPanel => _beamPanel;
        public BeamRebarPanel GetOrCreateBeamPanel()
        {
            if (_beamPanel == null) _beamPanel = new BeamRebarPanel(_doc);
            return _beamPanel;
        }
        public BeamAdvancePanel BeamAdvancePanel => _beamAdvancePanel;
    }
}
