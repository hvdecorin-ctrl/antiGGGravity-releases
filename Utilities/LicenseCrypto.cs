using System;
using System.Security.Cryptography;
using System.Text;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Core cryptographic engine for license key generation and validation.
    /// Uses HMAC-SHA256 signed keys bound to a specific Hardware ID.
    /// 
    /// Key format (before encoding):
    ///   Bytes 0-3:   Expiry date as Unix timestamp (Int32, big-endian)
    ///   Bytes 4-35:  HMAC-SHA256(HWID + ExpiryBytes, SecretSalt) — 32 bytes
    ///   Total: 36 bytes → Base32 encoded → formatted as XXXXX-XXXXX-...
    /// </summary>
    public static class LicenseCrypto
    {
        // =====================================================================
        // SECRET SALT — This is the ONLY secret. Obfuscar HideStrings hides it.
        // Change this to your own random string before publishing.
        // Generate one with: [System.Guid]::NewGuid().ToString("N") + [System.Guid]::NewGuid().ToString("N")
        // =====================================================================
        private const string SecretSalt = "aG7x2Qm9vKpL4wBnE8rT1sYd6jC0fHiU3oZaNbXcDeFgJkMlPqRtWuVyAzS5x";

        // Base32 alphabet — 32 chars, no 0/O/1/I to avoid user confusion
        private static readonly char[] Base32Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        /// <summary>
        /// Generates a signed activation key for a given HWID and expiry date.
        /// Called by the private KeyGen app only.
        /// </summary>
        public static string GenerateActivationKey(string hwid, DateTime expiryUtc)
        {
            // 1. Encode expiry as 4-byte Unix timestamp (seconds since epoch)
            var expiryBytes = GetExpiryBytes(expiryUtc);

            // 2. Compute HMAC signature: HMAC(HWID + ExpiryBytes, SecretSalt)
            var signature = ComputeSignature(hwid, expiryBytes);

            // 3. Combine: [4 bytes expiry] + [32 bytes signature] = 36 bytes
            var payload = new byte[36];
            Buffer.BlockCopy(expiryBytes, 0, payload, 0, 4);
            Buffer.BlockCopy(signature, 0, payload, 4, 32);

            // 4. Encode to user-friendly Base32 format
            return FormatKey(ToBase32(payload));
        }

        /// <summary>
        /// Validates an activation key against the current machine's HWID.
        /// Returns whether the key is valid, the expiry date, and any error message.
        /// </summary>
        public static LicenseKeyResult ValidateActivationKey(string key, string hwid)
        {
            if (string.IsNullOrWhiteSpace(key))
                return LicenseKeyResult.Invalid("No activation key provided.");

            try
            {
                // 1. Strip formatting and decode Base32
                var cleanKey = key.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
                var payload = FromBase32(cleanKey);

                if (payload == null || payload.Length != 36)
                    return LicenseKeyResult.Invalid("Invalid key format.");

                // 2. Extract expiry (first 4 bytes)
                var expiryBytes = new byte[4];
                Buffer.BlockCopy(payload, 0, expiryBytes, 0, 4);
                var expiryUtc = GetExpiryDate(expiryBytes);

                // 3. Extract signature (remaining 32 bytes)
                var storedSignature = new byte[32];
                Buffer.BlockCopy(payload, 4, storedSignature, 0, 32);

                // 4. Recompute expected signature
                var expectedSignature = ComputeSignature(hwid, expiryBytes);

                // 5. Constant-time comparison to prevent timing attacks
                if (!ConstantTimeEquals(storedSignature, expectedSignature))
                    return LicenseKeyResult.Invalid("Invalid activation key. This key does not match your hardware.");

                // 6. Check expiry
                if (expiryUtc.Date < DateTime.UtcNow.Date)
                    return LicenseKeyResult.Expired(expiryUtc);

                return LicenseKeyResult.Valid(expiryUtc);
            }
            catch (Exception)
            {
                return LicenseKeyResult.Invalid("Invalid activation key format.");
            }
        }

        #region Internal Crypto Helpers

        private static byte[] GetExpiryBytes(DateTime expiryUtc)
        {
            var epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var seconds = (int)(expiryUtc - epoch).TotalSeconds;
            var bytes = BitConverter.GetBytes(seconds);
            // Use big-endian for consistent cross-platform behavior
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        private static DateTime GetExpiryDate(byte[] expiryBytes)
        {
            var bytes = (byte[])expiryBytes.Clone();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            var seconds = BitConverter.ToInt32(bytes, 0);
            var epoch = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return epoch.AddSeconds(seconds);
        }

        private static byte[] ComputeSignature(string hwid, byte[] expiryBytes)
        {
            var saltBytes = Encoding.UTF8.GetBytes(SecretSalt);
            using (var hmac = new HMACSHA256(saltBytes))
            {
                // Combine HWID bytes + expiry bytes as the message
                var hwidBytes = Encoding.UTF8.GetBytes(hwid.ToLowerInvariant());
                var message = new byte[hwidBytes.Length + expiryBytes.Length];
                Buffer.BlockCopy(hwidBytes, 0, message, 0, hwidBytes.Length);
                Buffer.BlockCopy(expiryBytes, 0, message, hwidBytes.Length, expiryBytes.Length);
                return hmac.ComputeHash(message);
            }
        }

        /// <summary>
        /// Constant-time byte array comparison to prevent timing side-channel attacks.
        /// </summary>
        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        #endregion

        #region Base32 Encoding/Decoding

        private static string ToBase32(byte[] data)
        {
            var sb = new StringBuilder((data.Length * 8 + 4) / 5);
            int buffer = 0, bitsLeft = 0;

            foreach (var b in data)
            {
                buffer = (buffer << 8) | b;
                bitsLeft += 8;
                while (bitsLeft >= 5)
                {
                    bitsLeft -= 5;
                    sb.Append(Base32Chars[(buffer >> bitsLeft) & 0x1F]);
                }
            }
            if (bitsLeft > 0)
            {
                sb.Append(Base32Chars[(buffer << (5 - bitsLeft)) & 0x1F]);
            }
            return sb.ToString();
        }

        private static byte[] FromBase32(string encoded)
        {
            if (string.IsNullOrEmpty(encoded)) return null;

            var base32Lookup = new int[128];
            for (int i = 0; i < 128; i++) base32Lookup[i] = -1;
            for (int i = 0; i < Base32Chars.Length; i++)
                base32Lookup[Base32Chars[i]] = i;

            var output = new byte[encoded.Length * 5 / 8];
            int buffer = 0, bitsLeft = 0, index = 0;

            foreach (var c in encoded)
            {
                if (c >= 128 || base32Lookup[c] < 0) return null;
                buffer = (buffer << 5) | base32Lookup[c];
                bitsLeft += 5;
                if (bitsLeft >= 8)
                {
                    bitsLeft -= 8;
                    if (index < output.Length)
                        output[index++] = (byte)(buffer >> bitsLeft);
                }
            }
            return output;
        }

        /// <summary>
        /// Formats a raw Base32 string as XXXXX-XXXXX-XXXXX-... groups.
        /// </summary>
        private static string FormatKey(string raw)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && i % 5 == 0) sb.Append('-');
                sb.Append(raw[i]);
            }
            return sb.ToString();
        }

        #endregion
    }

    /// <summary>
    /// Result of a license key cryptographic validation.
    /// </summary>
    public class LicenseKeyResult
    {
        public bool IsValid { get; private set; }
        public bool IsExpired { get; private set; }
        public DateTime? ExpiryDate { get; private set; }
        public string Message { get; private set; }

        private LicenseKeyResult() { }

        public static LicenseKeyResult Valid(DateTime expiry) => new LicenseKeyResult
        {
            IsValid = true,
            IsExpired = false,
            ExpiryDate = expiry,
            Message = $"License valid until {expiry:yyyy-MM-dd}"
        };

        public static LicenseKeyResult Expired(DateTime expiry) => new LicenseKeyResult
        {
            IsValid = false,
            IsExpired = true,
            ExpiryDate = expiry,
            Message = $"License expired on {expiry:yyyy-MM-dd}. Please contact support for renewal."
        };

        public static LicenseKeyResult Invalid(string reason) => new LicenseKeyResult
        {
            IsValid = false,
            IsExpired = false,
            Message = reason
        };
    }
}
