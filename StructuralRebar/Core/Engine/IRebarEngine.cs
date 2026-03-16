using Autodesk.Revit.DB;
using antiGGGravity.StructuralRebar.DTO;

namespace antiGGGravity.StructuralRebar.Core.Engine
{
    public interface IRebarEngine
    {
        bool Execute(Element host, RebarRequest request);
    }
}
