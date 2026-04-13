using System;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Validates licenses using offline HMAC-signed keys.
    /// Supports a 14-day free trial period from first install.
    /// Results are cached per Revit session for performance.
    /// </summary>
    public static class LicenseValidator
    {
        /// <summary>
        /// Number of free trial days from first install.
        /// </summary>
        public const int TrialDays = 14;

        /// <summary>
        /// Tolerance in hours before flagging clock tampering.
        /// </summary>
        private const int ClockToleranceHours = 1;

        // Session-level cache — avoids re-reading files on every command
        private static LicenseResult _cachedResult;
        private static DateTime _cacheTime = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Validates the current machine's license.
        /// Uses cached result within the cache window for performance.
        /// </summary>
        public static LicenseResult ValidateLicense()
        {
            // Return cached result if still fresh
            if (_cachedResult != null && (DateTime.UtcNow - _cacheTime) < CacheDuration)
                return _cachedResult;

            var result = PerformValidation();
            _cachedResult = result;
            _cacheTime = DateTime.UtcNow;
            return result;
        }

        /// <summary>
        /// Clears the cached result. Call after activating a new key.
        /// </summary>
        public static void InvalidateCache()
        {
            _cachedResult = null;
            _cacheTime = DateTime.MinValue;
        }

        /// <summary>
        /// Gets the current license status without cache (for UI display).
        /// </summary>
        public static LicenseResult GetCurrentStatus()
        {
            return PerformValidation();
        }

        private static LicenseResult PerformValidation()
        {
#if EMBED_LICENSE
            // For distribution builds, we strictly enforce the 14-day trial
            LicenseStorage.EnsureInstallDateRecorded();
            var effectiveNow = GetEffectiveUtcNow();
            return CheckTrialPeriod(effectiveNow);
#else
            // 1. Record install date for trial tracking
            LicenseStorage.EnsureInstallDateRecorded();

            // 2. Anti-clock tampering check
            if (IsClockTampered())
            {
                return LicenseResult.Error(
                    "System clock tampering detected. Please set your system time correctly and restart Revit.");
            }

            // 3. Compute the effective "now" using high-water mark approach
            //    This prevents clock rollback from re-validating an expired license.
            var effectiveNow = GetEffectiveUtcNow();

            // 4. Update the last-seen timestamp with the effective time
            LicenseStorage.SaveLastSeenDate(effectiveNow);

            // 5. Try to validate stored activation key
            var storedKey = LicenseStorage.LoadLicenseKey();
            if (!string.IsNullOrEmpty(storedKey))
            {
                var hwid = HardwareIdGenerator.GetHardwareId();
                var keyResult = LicenseCrypto.ValidateActivationKey(storedKey, hwid);

                if (keyResult.IsValid)
                {
                    // Key signature is valid — now check expiry against effectiveNow
                    if (keyResult.ExpiryDate.Value.Date < effectiveNow.Date)
                        return LicenseResult.Expired(keyResult.ExpiryDate.Value);

                    return LicenseResult.Valid(keyResult.ExpiryDate.Value);
                }
                else if (keyResult.IsExpired)
                {
                    return LicenseResult.Expired(keyResult.ExpiryDate.Value);
                }
                else
                {
                    // Key is invalid (wrong HWID, corrupt, etc.)
                    return LicenseResult.Error(
                        $"License key invalid: {keyResult.Message}\n\nPlease contact support or re-enter your activation key.");
                }
            }

            // 6. No activation key — check free trial
            return CheckTrialPeriod(effectiveNow);
#endif
        }

        private static LicenseResult CheckTrialPeriod(DateTime effectiveNow)
        {
            var installDate = LicenseStorage.GetInstallDate();

            if (installDate == null)
            {
                // Should not happen (EnsureInstallDateRecorded was called above),
                // but treat as expired trial for safety
                return LicenseResult.TrialExpired();
            }

            var daysSinceInstall = (effectiveNow - installDate.Value).TotalDays;

            if (daysSinceInstall <= TrialDays)
            {
                int daysRemaining = Math.Max(1, (int)Math.Ceiling(TrialDays - daysSinceInstall));
                return LicenseResult.Trial(daysRemaining);
            }

            return LicenseResult.TrialExpired();
        }

        private static bool IsClockTampered()
        {
            var lastSeen = LicenseStorage.LoadLastSeenDate();
            if (lastSeen == null) return false; // First run, no tampering possible

            // If current time is more than 1 hour BEFORE the last-seen time,
            // the user likely set the clock back
            return DateTime.UtcNow < lastSeen.Value.AddHours(-ClockToleranceHours);
        }

        /// <summary>
        /// Computes the effective "current time" using the high-water mark approach:
        ///   effectiveNow = max(DateTime.UtcNow, lastSeen, networkTime)
        /// This ensures even if the clock is rolled back, we remember the real date.
        /// </summary>
        private static DateTime GetEffectiveUtcNow()
        {
            var now = DateTime.UtcNow;
            var effectiveNow = now;

            // Use last-seen date if it's newer than system clock (clock was rolled back)
            var lastSeen = LicenseStorage.LoadLastSeenDate();
            if (lastSeen.HasValue && lastSeen.Value > effectiveNow)
                effectiveNow = lastSeen.Value;

            // Try network time — non-blocking, 3s timeout
            try
            {
                var networkTime = NetworkTime.GetUtcNow();
                if (networkTime.HasValue && networkTime.Value > effectiveNow)
                    effectiveNow = networkTime.Value;
            }
            catch { }

            return effectiveNow;
        }
    }

    /// <summary>
    /// Represents the result of a license validation check.
    /// </summary>
    public class LicenseResult
    {
        public bool IsValid { get; private set; }
        public bool IsTrial { get; private set; }
        public int TrialDaysRemaining { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string Message { get; private set; }

        private LicenseResult() { }

        public static LicenseResult Valid(DateTime expiry) => new LicenseResult
        {
            IsValid = true,
            IsTrial = false,
            ExpiryDate = expiry,
            Message = $"License valid until {expiry:yyyy-MM-dd}"
        };

        public static LicenseResult Trial(int daysRemaining) => new LicenseResult
        {
            IsValid = true,
            IsTrial = true,
            TrialDaysRemaining = daysRemaining,
            Message = $"Free trial: {daysRemaining} day{(daysRemaining != 1 ? "s" : "")} remaining"
        };

        public static LicenseResult TrialExpired() => new LicenseResult
        {
            IsValid = false,
            IsTrial = true,
            TrialDaysRemaining = 0,
            Message = "Your 14-day free trial has expired.\nPlease activate a license to continue using antiGGGravity."
        };

        public static LicenseResult Expired(DateTime expiry) => new LicenseResult
        {
            IsValid = false,
            ExpiryDate = expiry,
            Message = $"License expired on {expiry:yyyy-MM-dd}.\nPlease contact support for renewal."
        };

        public static LicenseResult NotFound() => new LicenseResult
        {
            IsValid = false,
            Message = "License not found. Please activate with your Hardware ID."
        };

        public static LicenseResult Error(string error) => new LicenseResult
        {
            IsValid = false,
            Message = error
        };
    }
}
