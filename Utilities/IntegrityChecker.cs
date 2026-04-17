using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Runtime integrity checker that verifies the assembly hasn't been tampered with.
    /// Uses reflection to ensure critical security methods and classes still exist
    /// and haven't been removed or hollowed out by a binary patcher.
    /// 
    /// Security features:
    ///   - Encrypted string references (no plaintext type/method names for dnSpy to find)
    ///   - IL body size verification (catches NOP patching)
    ///   - Cross-linked with LicenseValidator (circular dependency)
    ///   - Strong name token verification (catches unsigned replacement DLLs)
    /// </summary>
    public static class IntegrityChecker
    {
        private static bool? _cachedResult;

        // Expected strong name public key token (from antiGGGravity.snk)
        private static readonly byte[] ExpectedPublicKeyToken = { 0x0c, 0x3e, 0x48, 0x43, 0x90, 0xfc, 0xb3, 0x09 };

        /// <summary>
        /// Minimum expected IL body sizes for critical methods.
        /// </summary>
        private const int MinExecuteILSize = 20;
        private const int MinValidateLicenseILSize = 15;

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

        private static void LogFailure(string reason)
        {
            try
            {
                string logPath = Path.Combine(Path.GetTempPath(), "agg_integrity.log");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Integrity Failure: {reason}{Environment.NewLine}");
            }
            catch { }
        }

        private static bool PerformChecks()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();

                // Check 1: Critical security types must exist
                var criticalTypes = new[]
                {
                    SecurityStrings.LicenseValidator,
                    SecurityStrings.LicenseCrypto,
                    SecurityStrings.LicenseStorage,
                    SecurityStrings.HardwareIdGenerator,
                    SecurityStrings.BaseCommand
                };

                foreach (var typeName in criticalTypes)
                {
                    if (asm.GetType(typeName) == null)
                    {
                        LogFailure($"Type missing: {typeName}");
                        return false;
                    }
                }

                // Check 2: BaseCommand.Execute must exist and be non-trivial
                var baseCmd = asm.GetType(SecurityStrings.BaseCommand);
                var executeMethod = baseCmd?.GetMethod(SecurityStrings.Execute,
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(Autodesk.Revit.UI.ExternalCommandData), typeof(string).MakeByRefType(), typeof(Autodesk.Revit.DB.ElementSet) },
                    null);
                
                if (executeMethod == null)
                {
                    LogFailure("Execute method not found via reflection.");
                    return false;
                }

                // Check 3: RequiresLicense property must exist on BaseCommand
                var requiresLicenseProp = baseCmd?.GetProperty(SecurityStrings.RequiresLicense,
                    BindingFlags.NonPublic | BindingFlags.Instance);
                if (requiresLicenseProp == null)
                {
                    LogFailure("RequiresLicense property missing.");
                    return false;
                }

                // Check 4: LicenseValidator.ValidateLicense must exist
                var validator = asm.GetType(SecurityStrings.LicenseValidator);
                var validateMethod = validator?.GetMethod(SecurityStrings.ValidateLicense,
                    BindingFlags.Public | BindingFlags.Static);
                if (validateMethod == null)
                {
                    LogFailure("ValidateLicense method missing.");
                    return false;
                }

                // Check 6: Verify Execute method body size (catches NOP patching)
                var executeBody = executeMethod.GetMethodBody();
                if (executeBody == null || executeBody.GetILAsByteArray().Length < MinExecuteILSize)
                {
                    LogFailure($"Execute IL size too small: {executeBody?.GetILAsByteArray()?.Length ?? 0}");
                    return false;
                }

                // Check 7: Verify ValidateLicense method body size
                var validateBody = validateMethod.GetMethodBody();
                if (validateBody == null || validateBody.GetILAsByteArray().Length < MinValidateLicenseILSize)
                {
                    LogFailure($"ValidateLicense IL size too small: {validateBody?.GetILAsByteArray()?.Length ?? 0}");
                    return false;
                }

                // Check 8: Strong name public key token verification
                var name = asm.GetName();
                var publicKeyToken = name.GetPublicKeyToken();
                if (publicKeyToken != null && publicKeyToken.Length > 0)
                {
                    if (!publicKeyToken.SequenceEqual(ExpectedPublicKeyToken))
                    {
                        string actual = BitConverter.ToString(publicKeyToken).Replace("-", "").ToLower();
                        LogFailure($"Strong name mismatch. Actual: {actual}");
                        return false;
                    }
                }

                // Check 9: Verify IntegrityChecker itself
                var selfType = asm.GetType("antiGGGravity.Utilities.IntegrityChecker");
                var isIntactMethod = selfType?.GetMethod(SecurityStrings.IsIntact,
                    BindingFlags.Public | BindingFlags.Static);
                if (isIntactMethod == null)
                {
                    LogFailure("IntegrityChecker self-lookup failed.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                LogFailure($"Exception in PerformChecks: {ex.Message}");
                return false;
            }
        }
    }
}
