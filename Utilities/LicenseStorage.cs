using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Handles reading and writing license data and anti-tamper state to disk.
    /// All files stored in %AppData%\antiGGGravity\ alongside existing settings.
    /// </summary>
    public static class LicenseStorage
    {
        private static readonly string AppDataFolder;
        private static readonly string LicenseFilePath;
        private static readonly string StateFilePath;
        private static readonly string InstallDateFilePath;
        private static readonly object _lock = new object();

        // Registry key path for redundant last-seen storage
        private const string RegistryKeyPath = @"Software\antiGGGravity";
        private const string RegistryLastSeenValue = "LastSeen";

        // Simple XOR key for obfuscating the state file (not crypto-grade, just anti-casual-edit)
        private static readonly byte[] StateObfuscationKey = { 0xA7, 0x3B, 0xF1, 0x5C, 0x82, 0xD4, 0x69, 0xE0 };

        static LicenseStorage()
        {
            AppDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "antiGGGravity");
            LicenseFilePath = Path.Combine(AppDataFolder, "license.key");
            StateFilePath = Path.Combine(AppDataFolder, ".state");
            InstallDateFilePath = Path.Combine(AppDataFolder, ".install");
        }

        #region License Key Storage

        /// <summary>
        /// Saves the activation key string to disk.
        /// </summary>
        public static void SaveLicenseKey(string activationKey)
        {
            lock (_lock)
            {
                EnsureFolder();
                var plainBytes = Encoding.UTF8.GetBytes(activationKey.Trim());
                var obfuscated = XorObfuscate(plainBytes);
                File.WriteAllBytes(LicenseFilePath, obfuscated);
            }
        }

        /// <summary>
        /// Reads the stored activation key from disk.
        /// Returns null if no key is stored.
        /// </summary>
        public static string LoadLicenseKey()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(LicenseFilePath)) return null;

                    var obfuscated = File.ReadAllBytes(LicenseFilePath);
                    var plainBytes = XorObfuscate(obfuscated);
                    var key = Encoding.UTF8.GetString(plainBytes).Trim();

                    // Sanity check: valid keys only contain Base32 chars and dashes
                    if (key.Length > 0 && key.Replace("-", "").Replace(" ", "").Length > 10)
                        return key;
                }
                catch { }

                // Fallback: try reading as plaintext (legacy/migration)
                try
                {
                    if (File.Exists(LicenseFilePath))
                    {
                        var plainText = File.ReadAllText(LicenseFilePath).Trim();
                        if (!string.IsNullOrEmpty(plainText) && plainText.Contains("-"))
                        {
                            // Re-save as obfuscated and return
                            var plainBytes = Encoding.UTF8.GetBytes(plainText);
                            File.WriteAllBytes(LicenseFilePath, XorObfuscate(plainBytes));
                            return plainText;
                        }
                    }
                }
                catch { }
                return null;
            }
        }

        /// <summary>
        /// Deletes the stored license key (for deactivation).
        /// </summary>
        public static void DeleteLicenseKey()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(LicenseFilePath))
                        File.Delete(LicenseFilePath);
                }
                catch { }
            }
        }

        #endregion

        #region Anti-Clock Tampering (Last-Seen Date)

        /// <summary>
        /// Saves the current UTC timestamp as the last-seen date.
        /// Writes to BOTH the obfuscated file AND the Windows Registry for redundancy.
        /// </summary>
        public static void SaveLastSeenDate(DateTime utcNow)
        {
            lock (_lock)
            {
                try
                {
                    EnsureFolder();
                    var timestamp = utcNow.ToString("O"); // ISO 8601
                    var plainBytes = Encoding.UTF8.GetBytes(timestamp);
                    var obfuscated = XorObfuscate(plainBytes);
                    File.WriteAllBytes(StateFilePath, obfuscated);
                }
                catch { }

                // Redundant write to Registry
                SaveLastSeenToRegistry(utcNow);
            }
        }

        /// <summary>
        /// Reads the last-seen UTC date from BOTH file and Registry.
        /// Returns the most recent of the two (high-water mark).
        /// If only one source has a value, returns that one.
        /// </summary>
        public static DateTime? LoadLastSeenDate()
        {
            lock (_lock)
            {
                var fileDate = LoadLastSeenFromFile();
                var registryDate = LoadLastSeenFromRegistry();

                if (fileDate == null && registryDate == null)
                    return null;
                if (fileDate == null) return registryDate;
                if (registryDate == null) return fileDate;

                // Return whichever is newer — prevents rollback via deleting one source
                return fileDate.Value > registryDate.Value ? fileDate : registryDate;
            }
        }

        private static DateTime? LoadLastSeenFromFile()
        {
            try
            {
                if (!File.Exists(StateFilePath)) return null;

                var obfuscated = File.ReadAllBytes(StateFilePath);
                var plainBytes = XorObfuscate(obfuscated); // XOR is symmetric
                var timestamp = Encoding.UTF8.GetString(plainBytes);

                if (DateTime.TryParse(timestamp, null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                {
                    return result;
                }
            }
            catch { }
            return null;
        }

        private static void SaveLastSeenToRegistry(DateTime utcNow)
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    key?.SetValue(RegistryLastSeenValue, utcNow.ToString("O"));
                }
            }
            catch { }
        }

        private static DateTime? LoadLastSeenFromRegistry()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                {
                    var value = key?.GetValue(RegistryLastSeenValue) as string;
                    if (value != null && DateTime.TryParse(value, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                    {
                        return result;
                    }
                }
            }
            catch { }
            return null;
        }

        #endregion

        #region Trial / Install Date

        /// <summary>
        /// Records the first-ever install date for the 7-day free trial.
        /// Only writes if the file doesn't already exist.
        /// </summary>
        public static void EnsureInstallDateRecorded()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(InstallDateFilePath)) return;

                    EnsureFolder();
                    var timestamp = DateTime.UtcNow.ToString("O");
                    var plainBytes = Encoding.UTF8.GetBytes(timestamp);
                    var obfuscated = XorObfuscate(plainBytes);
                    File.WriteAllBytes(InstallDateFilePath, obfuscated);
                }
                catch { }
            }
        }

        /// <summary>
        /// Gets the original install date for trial calculation.
        /// Returns null if unknown (treats as expired trial).
        /// </summary>
        public static DateTime? GetInstallDate()
        {
            lock (_lock)
            {
                try
                {
                    if (!File.Exists(InstallDateFilePath)) return null;

                    var obfuscated = File.ReadAllBytes(InstallDateFilePath);
                    var plainBytes = XorObfuscate(obfuscated);
                    var timestamp = Encoding.UTF8.GetString(plainBytes);

                    if (DateTime.TryParse(timestamp, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var result))
                    {
                        return result;
                    }
                }
                catch { }
                return null;
            }
        }

        #endregion

        #region Helpers

        private static void EnsureFolder()
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);
        }

        /// <summary>
        /// Simple XOR obfuscation — not cryptographic, just prevents casual editing.
        /// </summary>
        private static byte[] XorObfuscate(byte[] data)
        {
            var result = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
                result[i] = (byte)(data[i] ^ StateObfuscationKey[i % StateObfuscationKey.Length]);
            return result;
        }

        #endregion
    }
}
