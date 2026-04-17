using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using antiGGGravity.Utilities;
using System;
using System.Reflection;

namespace antiGGGravity.Commands
{
    /// <summary>
    /// Base class for all Revit commands in antiGGGravity.
    /// Provides global error handling and license validation.
    /// 
    /// Security notes:
    ///   - License check uses indirect invocation via reflection (prevents "Find References" attack)
    ///   - Integrity check runs before AND after command execution
    ///   - Cross-validates IntegrityChecker via LicenseValidator.VerifyIntegrityCheckerIntact()
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
                // 0. Assembly Integrity Check — catch binary patching
                if (!IntegrityChecker.IsIntact())
                {
                    TaskDialog.Show("antiGGGravity",
                        "This installation appears to be corrupted.\nPlease reinstall from an official source.");
                    return Result.Failed;
                }

                // 0b. Cross-validation: verify IntegrityChecker itself wasn't patched
                if (!LicenseValidator.VerifyIntegrityCheckerIntact())
                {
                    TaskDialog.Show("antiGGGravity",
                        "Security validation failed.\nPlease reinstall from an official source.");
                    return Result.Failed;
                }

                // 1. License Check — indirect invocation via reflection
                //    Prevents a simple "Find References → NOP" attack in dnSpy
                if (RequiresLicense)
                {
                    var license = InvokeLicenseCheck();
                    if (license == null || !license.IsValid)
                    {
                        var dialog = new TaskDialog("antiGGGravity License")
                        {
                            MainIcon = TaskDialogIcon.TaskDialogIconWarning,
                            MainInstruction = "License Required",
                            MainContent = (license?.Message ?? "License validation failed.") + "\n\nWould you like to open the License Manager to activate a key?",
                            CommonButtons = TaskDialogCommonButtons.Close
                        };
                        dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Open License Manager");

                        var result = dialog.Show();
                        
                        if (result == TaskDialogResult.CommandLink1)
                        {
                            // Route them to the activation dialog
                            var cmd = new antiGGGravity.Commands.AntiGravity.GetHardwareIdCommand();
                            string dummyMsg = "";
                            cmd.Execute(commandData, ref dummyMsg, elements);
                        }

                        return Result.Cancelled;
                    }
                }

                // 2. Safe Execution
                var execResult = ExecuteSafe(commandData, ref message, elements);

                // 3. Post-execution integrity re-check (catches runtime patching)
                if (RequiresLicense && !IntegrityChecker.IsIntact())
                    return Result.Failed;

                return execResult;
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
        /// Invokes license validation via reflection with encrypted method name.
        /// This makes it harder for a hacker to find the call site in dnSpy,
        /// because there's no direct reference to LicenseValidator.ValidateLicense().
        /// </summary>
        private static LicenseResult InvokeLicenseCheck()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var validatorType = asm.GetType(SecurityStrings.LicenseValidator);
                if (validatorType == null) return null;

                var method = validatorType.GetMethod(SecurityStrings.ValidateLicense,
                    BindingFlags.Public | BindingFlags.Static);
                if (method == null) return null;

                return method.Invoke(null, null) as LicenseResult;
            }
            catch
            {
                return null; // Treat reflection failure as unlicensed
            }
        }

        /// <summary>
        /// Implement your command logic here. Is automatically wrapped in error handling.
        /// </summary>
        protected abstract Result ExecuteSafe(ExternalCommandData commandData, ref string message, ElementSet elements);
    }
}
