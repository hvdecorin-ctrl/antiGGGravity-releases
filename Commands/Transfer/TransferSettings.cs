using System;
using System.IO;
using System.Text.Json;

namespace antiGGGravity.Commands.Transfer
{
    public class TransferSettings
    {
        public string Standard1Path { get; set; } = "";
        public string Standard2Path { get; set; } = "";
        public string Folder1Path { get; set; } = "";
        public string Folder2Path { get; set; } = "";
        public string LastManagerFolderPath { get; set; } = "";

        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "antiGGGravity");

        private static readonly string SettingsFile = Path.Combine(SettingsDir, "transfer_settings.json");

        public static TransferSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string json = File.ReadAllText(SettingsFile);
                    return JsonSerializer.Deserialize<TransferSettings>(json) ?? new TransferSettings();
                }
            }
            catch { /* Return defaults on any error */ }
            return new TransferSettings();
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(SettingsDir))
                    Directory.CreateDirectory(SettingsDir);

                string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
            }
            catch { /* Silently fail if unable to save */ }
        }
    }
}
