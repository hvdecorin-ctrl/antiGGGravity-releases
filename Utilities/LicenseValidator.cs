using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Validates licenses against a GitHub Gist containing license entries.
    /// </summary>
    public static class LicenseValidator
    {
        // TODO: Replace with your actual Gist raw URL after creating it
        private const string LicenseUrl = "https://gist.githubusercontent.com/YOUR_USERNAME/GIST_ID/raw/licenses.txt";
        
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        
        /// <summary>
        /// Validates the current machine's license asynchronously.
        /// </summary>
        public static async Task<LicenseResult> ValidateLicenseAsync()
        {
            var hardwareId = HardwareIdGenerator.GetHardwareId();
            
            try
            {
                // Use ConfigureAwait(false) to prevent deadlocks in UI thread
                var content = await _httpClient.GetStringAsync(LicenseUrl).ConfigureAwait(false);
                return ParseLicenseContent(content, hardwareId);
            }
            catch (HttpRequestException)
            {
                return LicenseResult.Error("Unable to connect to license server. Check your internet connection.");
            }
            catch (TaskCanceledException)
            {
                return LicenseResult.Error("License server request timed out.");
            }
            catch (Exception ex)
            {
                return LicenseResult.Error($"Validation error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Synchronous license validation for use in Revit commands.
        /// Safely waits for the task to complete without deadlocking.
        /// </summary>
        public static LicenseResult ValidateLicense()
        {
            try
            {
                // Run on thread pool with a 15-second total timeout
                var task = Task.Run(() => ValidateLicenseAsync());
                if (task.Wait(TimeSpan.FromSeconds(15)))
                {
                    return task.Result;
                }
                return LicenseResult.Error("License validation timed out (15s). Please check your internet.");
            }
            catch (Exception ex)
            {
                return LicenseResult.Error($"License system error: {ex.Message}");
            }
        }
        
        private static LicenseResult ParseLicenseContent(string content, string hardwareId)
        {
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var parts = line.Trim().Split('|');
                if (parts.Length >= 2 && 
                    string.Equals(parts[0].Trim(), hardwareId, StringComparison.OrdinalIgnoreCase))
                {
                    if (DateTime.TryParse(parts[1].Trim(), out var expiry))
                    {
                        if (expiry.Date >= DateTime.Today)
                        {
                            return LicenseResult.Valid(expiry);
                        }
                        return LicenseResult.Expired(expiry);
                    }
                }
            }
            
            return LicenseResult.NotFound();
        }
    }
    
    /// <summary>
    /// Represents the result of a license validation check.
    /// </summary>
    public class LicenseResult
    {
        public bool IsValid { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string Message { get; private set; }
        
        private LicenseResult() { }
        
        public static LicenseResult Valid(DateTime expiry) => new LicenseResult
        {
            IsValid = true,
            ExpiryDate = expiry,
            Message = $"License valid until {expiry:yyyy-MM-dd}"
        };
        
        public static LicenseResult Expired(DateTime expiry) => new LicenseResult
        {
            IsValid = false,
            ExpiryDate = expiry,
            Message = $"License expired on {expiry:yyyy-MM-dd}. Please contact support for renewal."
        };
        
        public static LicenseResult NotFound() => new LicenseResult
        {
            IsValid = false,
            Message = "License not found. Please contact support with your Hardware ID."
        };
        
        public static LicenseResult Error(string error) => new LicenseResult
        {
            IsValid = false,
            Message = error
        };
    }
}
