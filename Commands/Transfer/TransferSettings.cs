using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace antiGGGravity.Commands.Transfer
{
    public class TransferSettings
    {
        public string Standard1Path { get; set; } = "";
        public string Standard2Path { get; set; } = "";
        public string Folder1Path { get; set; } = "";
        public string Folder2Path { get; set; } = "";
        public string LastManagerFolderPath { get; set; } = "";

        // Not serialized — set at runtime so Save() knows which file to write
        [JsonIgnore]
        private string _settingsFilePath;

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "antiGGGravity");

        /// <summary>
        /// Load version-specific settings. Each Revit version gets its own file
        /// (e.g. transfer_settings_2022.json, transfer_settings_2026.json).
        /// </summary>
        public static TransferSettings Load(string revitVersion)
        {
            string filePath = GetSettingsFilePath(revitVersion);
            try
            {
                if (File.Exists(filePath))
                {
                    string json = File.ReadAllText(filePath);
                    var settings = JsonSerializer.Deserialize<TransferSettings>(json) ?? new TransferSettings();
                    settings._settingsFilePath = filePath;
                    return settings;
                }
            }
            catch { /* Return defaults on any error */ }

            var defaults = new TransferSettings();
            defaults._settingsFilePath = filePath;
            return defaults;
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { /* Silently fail if unable to save */ }
        }

        private static string GetSettingsFilePath(string revitVersion)
        {
            string fileName = string.IsNullOrEmpty(revitVersion)
                ? "transfer_settings.json"
                : $"transfer_settings_{revitVersion}.json";
            return Path.Combine(SettingsDir, fileName);
        }
    }
}

