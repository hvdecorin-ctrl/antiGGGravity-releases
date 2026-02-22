using Autodesk.Revit.DB.Structure;

namespace antiGGGravity.StructuralRebar.DTO
{
    public class HookViewModel
    {
        public RebarHookType Hook { get; }
        
        public string Name => Hook == null ? "(None)" : Hook.Name;

        public HookViewModel(RebarHookType hook)
        {
            Hook = hook;
        }
    }
}
