using Autodesk.Revit.UI;
using antiGGGravity.StructuralRebar.Constants;
using antiGGGravity.StructuralRebar.UI.Panels;

namespace antiGGGravity.StructuralRebar.UI
{
    /// <summary>
    /// Interface for rebar windows to decouple the handler from specific window implementations.
    /// </summary>
    public interface IRebarWindow
    {
        ElementHostType SelectedHostType { get; }
        bool RemoveExisting { get; }
        bool EnableLapSplice { get; }
        DesignCodeStandard DesignCode { get; }

        BeamRebarPanel BeamPanel { get; }
        WallRebarPanel WallPanel { get; }
        ColumnRebarPanel ColumnPanel { get; }
        StripFootingRebarPanel StripFootingPanel { get; }
        FootingPadRebarPanel FootingPadPanel { get; }
        WallCornerLPanel WallCornerLPanel { get; }
        WallCornerUPanel WallCornerUPanel { get; }
        BeamAdvancePanel BeamAdvancePanel { get; }

        BeamRebarPanel GetOrCreateBeamPanel();
        void ReShow();
        void Hide();
        void Close();
    }
}
