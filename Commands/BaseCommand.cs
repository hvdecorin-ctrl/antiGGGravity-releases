using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;
using System;

namespace antiGGGravity.Commands
{
    /// <summary>
    /// Base class for all Revit commands in antiGGGravity.
    /// Provides global error handling and license validation.
    /// </summary>
    public abstract class BaseCommand : IExternalCommand
    {
        /// <summary>
        /// Set to true if this command requires a valid license to run.
        /// </summary>
        protected virtual bool RequiresLicense => true;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // 1. License Check (DISABLED for Conversion Phase)
                /*
                if (RequiresLicense)
                {
                    var license = LicenseValidator.ValidateLicense();
                    if (!license.IsValid)
                    {
                        TaskDialog.Show("antiGGGravity License", 
                            $"Validation Failed:\n{license.Message}\n\nPlease check your internet or contact support.");
                        return Result.Cancelled;
                    }
                }
                */

                // 2. Safe Execution
                return ExecuteSafe(commandData, ref message, elements);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("antiGGGravity Error", 
                    $"An unexpected error occurred and the command was safely cancelled.\n\nError: {ex.Message}");
                message = ex.ToString();
                return Result.Failed;
            }
        }

        /// <summary>
        /// Implement your command logic here. Is automatically wrapped in error handling.
        /// </summary>
        protected abstract Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements);
    }
}
