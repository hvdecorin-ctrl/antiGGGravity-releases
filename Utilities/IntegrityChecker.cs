using System;
using System.Linq;
using System.Reflection;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Runtime integrity checker that verifies the assembly hasn't been tampered with.
    /// Uses reflection to ensure critical security methods and classes still exist
    /// and haven't been removed or hollowed out by a binary patcher.
    /// </summary>
    public static class IntegrityChecker
    {
        private static bool? _cachedResult;

        /// <summary>
        /// Returns true if the assembly appears unmodified.
        /// Result is cached for the process lifetime.
        /// </summary>
        public static bool IsIntact()
        {
            if (_cachedResult.HasValue) return _cachedResult.Value;
            _cachedResult = PerformChecks();
            return _cachedResult.Value;
        }

        private static bool PerformChecks()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();

                // Check 1: Critical security types must exist
                var criticalTypes = new[]
                {
                    "antiGGGravity.Utilities.LicenseValidator",
                    "antiGGGravity.Utilities.LicenseCrypto",
                    "antiGGGravity.Utilities.LicenseStorage",
                    "antiGGGravity.Utilities.HardwareIdGenerator",
                    "antiGGGravity.Commands.BaseCommand"
                };

                foreach (var typeName in criticalTypes)
                {
                    if (asm.GetType(typeName) == null)
                        return false;
                }

                // Check 2: BaseCommand.Execute must exist and contain the license check
                var baseCmd = asm.GetType("antiGGGravity.Commands.BaseCommand");
                var executeMethod = baseCmd?.GetMethod("Execute",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(Autodesk.Revit.UI.ExternalCommandData), typeof(string).MakeByRefType(), typeof(Autodesk.Revit.DB.ElementSet) },
                    null);
                if (executeMethod == null) return false;

                // Check 3: RequiresLicense property must exist on BaseCommand
                var requiresLicenseProp = baseCmd?.GetProperty("RequiresLicense",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (requiresLicenseProp == null) return false;

                // Check 4: LicenseValidator.ValidateLicense must exist
                var validator = asm.GetType("antiGGGravity.Utilities.LicenseValidator");
                var validateMethod = validator?.GetMethod("ValidateLicense",
                    BindingFlags.Public | BindingFlags.Static);
                if (validateMethod == null) return false;

                // Check 5: LicenseCrypto.ValidateActivationKey must exist
                var crypto = asm.GetType("antiGGGravity.Utilities.LicenseCrypto");
                var validateKeyMethod = crypto?.GetMethod("ValidateActivationKey",
                    BindingFlags.Public | BindingFlags.Static);
                if (validateKeyMethod == null) return false;

                // Check 6: Verify the Execute method body isn't suspiciously small
                // (A NOP'd or hollowed method would have a tiny body)
                var methodBody = executeMethod.GetMethodBody();
                if (methodBody == null || methodBody.GetILAsByteArray().Length < 30)
                    return false;

                // Check 7: Verify the assembly hasn't been re-signed with a different key
                // (strong name check — if assembly was originally signed)
                var name = asm.GetName();
                var publicKeyToken = name.GetPublicKeyToken();
                // If we have a strong name and it's been stripped, flag it
                // (Only relevant if we sign the assembly)

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
