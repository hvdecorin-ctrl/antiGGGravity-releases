using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.ProjectAudit
{
    /// <summary>
    /// Custom load options to force overwrite when loading family.
    /// Matches Python FamilyLoadOptions behavior.
    /// </summary>
    public class FamilyLoadOptions : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse,
            out FamilySource source, out bool overwriteParameterValues)
        {
            overwriteParameterValues = true;
            source = FamilySource.Family;
            return true;
        }
    }
}
