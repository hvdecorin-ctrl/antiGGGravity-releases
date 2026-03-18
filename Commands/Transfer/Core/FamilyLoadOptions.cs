using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Core
{
    /// <summary>
    /// Implements IFamilyLoadOptions to silently overwrite existing parameters and geometry
    /// when updating an existing family in the target document.
    /// </summary>
    public class FamilyLoadOptionsOverwrite : IFamilyLoadOptions
    {
        public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
        {
            // true means we want to load and overwrite
            overwriteParameterValues = true;
            return true;
        }

        public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
        {
            // If the shared family is already loaded and is a nested family, 
            // 'FamilySource.Family' means overwrite with the version contained in the parent family being loaded.
            source = FamilySource.Family;
            overwriteParameterValues = true;
            return true;
        }
    }
}
