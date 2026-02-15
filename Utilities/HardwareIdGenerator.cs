using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Generates a unique hardware ID based on machine-specific information.
    /// Uses WMI to get motherboard, processor, and BIOS identifiers.
    /// </summary>
    public static class HardwareIdGenerator
    {
        /// <summary>
        /// Gets the hardware ID as a SHA256 hash of combined hardware identifiers.
        /// </summary>
        /// <returns>64-character hexadecimal string representing the hardware ID.</returns>
        public static string GetHardwareId()
        {
            var sb = new StringBuilder();
            
            // Motherboard serial number
            sb.Append(GetWmiValue("Win32_BaseBoard", "SerialNumber"));
            
            // Processor ID - unique CPU identifier
            sb.Append(GetWmiValue("Win32_Processor", "ProcessorId"));
            
            // BIOS serial number
            sb.Append(GetWmiValue("Win32_BIOS", "SerialNumber"));
            
            // Machine name for additional uniqueness
            sb.Append(Environment.MachineName);
            
            return ComputeSha256Hash(sb.ToString());
        }
        
        /// <summary>
        /// Retrieves a single property value from a WMI class.
        /// </summary>
        private static string GetWmiValue(string wmiClass, string property)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        var value = obj[property]?.ToString();
                        if (!string.IsNullOrEmpty(value) && 
                            value != "To Be Filled By O.E.M." && 
                            value != "Default string")
                        {
                            return value;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Silently handle WMI access errors
            }
            return "";
        }
        
        /// <summary>
        /// Computes SHA256 hash of the input string.
        /// </summary>
        private static string ComputeSha256Hash(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb = new StringBuilder(64);
                foreach (var b in bytes)
                {
                    sb.Append(b.ToString("x2"));
                }
                return sb.ToString();
            }
        }
    }
}
