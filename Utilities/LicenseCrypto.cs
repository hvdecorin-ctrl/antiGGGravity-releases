using System;
using System.Security.Cryptography;
using System.Text;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Core cryptographic engine for license key generation and validation.
    /// Uses ECDSA P-256 asymmetric signing — the shipped DLL contains ONLY the public key.
    /// The private key lives exclusively in the KeyGen app (never distributed).
    ///
    /// Key format (before encoding):
    ///   Bytes 0-3:   Expiry date as Unix timestamp (Int32, big-endian, epoch 2020-01-01)
    ///   Bytes 4-67:  ECDSA P-256 signature (64 bytes, IEEE P1363 format)
    ///   Total: 68 bytes → Base32 encoded → formatted as XXXXX-XXXXX-...
    /// </summary>
    public static class LicenseCrypto
    {
        // =====================================================================
        // PUBLIC KEY — Safe to ship in the DLL. Cannot be used to forge keys.
        // Generated: 2026-04-18. Algorithm: ECDSA P-256.
        // =====================================================================
        private static readonly byte[] PublicKeyX = Convert.FromBase64String("IlDLzcO57fOy43N+ntvaNb5ZfkeRcyOLCdc/ATQP7Pg=");
        private static readonly byte[] PublicKeyY = Convert.FromBase64String("Oh6O+fWPkmbPRED1n5uWeaMi06jCfDbf51QVkpJ9WF0=");

        // =====================================================================
        // PRIVATE KEY — Only used by KeyGen. Compiled out of the main DLL via
        // the KEYGEN_BUILD preprocessor constant.
        // Loaded from environment variable AGG_PRIVATE_KEY — never in source code.
        // =====================================================================
#if KEYGEN_BUILD
        private static byte[] LoadPrivateKey()
        {
            var b64 = Environment.GetEnvironmentVariable("AGG_PRIVATE_KEY");
            if (string.IsNullOrEmpty(b64))
                throw new InvalidOperationException(
                    "AGG_PRIVATE_KEY environment variable not set.\n" +
                    "Set it with: [System.Environment]::SetEnvironmentVariable('AGG_PRIVATE_KEY', '<key>', 'User')");
            return Convert.FromBase64String(b64);
        }
#endif

        // Base32 alphabet — 32 chars, no 0/O/1/I to avoid user confusion
        private static readonly char[] Base32Chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        /// <summary>
        /// Generates a signed activation key for a given HWID and expiry date.
        /// Only available in KeyGen builds (requires KEYGEN_BUILD constant).
        /// </summary>
#if KEYGEN_BUILD
        public static string GenerateActivationKey(string hwid, DateTime expiryUtc)
        {
            // 1. Encode expiry as 4-byte timestamp
            var expiryBytes = GetExpiryBytes(expiryUtc);

            // 2. Build the message to sign: HWID + ExpiryBytes
            var message = BuildMessage(hwid, expiryBytes);

            // 3. Sign with private key (IEEE P1363 format = fixed 64 bytes for P-256)
            using (var ecdsa = CreatePrivateKey())
            {
                var signature = ecdsa.SignData(message, HashAlgorithmName.SHA256);

                // 4. Combine: [4 bytes expiry] + [64 bytes signature] = 68 bytes
                var payload = new byte[68];
                Buffer.BlockCopy(expiryBytes, 0, payload, 0, 4);
                Buffer.BlockCopy(signature, 0, payload, 4, 64);

                // 5. Encode to user-friendly Base32 format
                return FormatKey(ToBase32(payload));
            }
        }
#endif

        /// <summary>
        /// Validates an activation key against the current machine's HWID.
        /// Uses only the PUBLIC key — cannot forge signatures.
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

                if (payload == null || payload.Length != 68)
                    return LicenseKeyResult.Invalid("Invalid key format.");

                // 2. Extract expiry (first 4 bytes)
                var expiryBytes = new byte[4];
                Buffer.BlockCopy(payload, 0, expiryBytes, 0, 4);
                var expiryUtc = GetExpiryDate(expiryBytes);

                // 3. Extract signature (remaining 64 bytes)
                var signature = new byte[64];
                Buffer.BlockCopy(payload, 4, signature, 0, 64);

                // 4. Build the message and verify with public key
                var message = BuildMessage(hwid, expiryBytes);

                using (var ecdsa = CreatePublicKey())
                {
                    bool isValid = ecdsa.VerifyData(message, signature, HashAlgorithmName.SHA256);

                    if (!isValid)
                        return LicenseKeyResult.Invalid("Invalid activation key. This key does not match your hardware.");
                }

                // 5. Check expiry
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

        /// <summary>
        /// Builds the message to sign/verify: UTF8(lowercase HWID) + expiryBytes.
        /// </summary>
        private static byte[] BuildMessage(string hwid, byte[] expiryBytes)
        {
            var hwidBytes = Encoding.UTF8.GetBytes(hwid.ToLowerInvariant());
            var message = new byte[hwidBytes.Length + expiryBytes.Length];
            Buffer.BlockCopy(hwidBytes, 0, message, 0, hwidBytes.Length);
            Buffer.BlockCopy(expiryBytes, 0, message, hwidBytes.Length, expiryBytes.Length);
            return message;
        }

        /// <summary>
        /// Creates an ECDsa instance with only the public key for verification.
        /// </summary>
        private static ECDsa CreatePublicKey()
        {
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = (byte[])PublicKeyX.Clone(),
                    Y = (byte[])PublicKeyY.Clone()
                }
            };
            return ECDsa.Create(parameters);
        }

#if KEYGEN_BUILD
        /// <summary>
        /// Creates an ECDsa instance with the private key for signing.
        /// Only available in KeyGen builds. Private key loaded from environment variable.
        /// </summary>
        private static ECDsa CreatePrivateKey()
        {
            var privateKeyD = LoadPrivateKey();
            var parameters = new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint
                {
                    X = (byte[])PublicKeyX.Clone(),
                    Y = (byte[])PublicKeyY.Clone()
                },
                D = (byte[])privateKeyD.Clone()
            };
            return ECDsa.Create(parameters);
        }
#endif

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
