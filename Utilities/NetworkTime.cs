using System;
using System.Globalization;
using System.Net.Http;
using System.Threading.Tasks;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Fetches trusted UTC time from an HTTP server's Date header.
    /// Uses a short timeout so it never blocks the UI when offline.
    /// </summary>
    public static class NetworkTime
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(3)
        };

        /// <summary>
        /// Attempts to get the current UTC time from Google's Date header.
        /// Returns null if offline, timed out, or any error occurs.
        /// </summary>
        public static DateTime? GetUtcNow()
        {
            try
            {
                return GetUtcNowAsync().GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Async version — sends a HEAD request to Google and parses the Date header.
        /// </summary>
        public static async Task<DateTime?> GetUtcNowAsync()
        {
            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com"))
                {
                    var response = await _client.SendAsync(request).ConfigureAwait(false);

                    if (response.Headers.Date.HasValue)
                    {
                        return response.Headers.Date.Value.UtcDateTime;
                    }
                }
            }
            catch
            {
                // Offline, timeout, DNS failure, etc. — all silently ignored.
            }

            return null;
        }
    }
}
