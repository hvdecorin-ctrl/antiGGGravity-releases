using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Manages persistent settings for rebar tools across sessions.
    /// Settings are stored per-view in a JSON file in AppData.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsFolder;
        private static readonly string SettingsFilePath;
        private static Dictionary<string, Dictionary<string, string>> _allSettings;
        private static readonly object _lock = new object();

        static SettingsManager()
        {
            SettingsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "antiGGGravity");
            SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");
            _allSettings = new Dictionary<string, Dictionary<string, string>>();
            Load();
        }

        private static void Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        string json = File.ReadAllText(SettingsFilePath);
                        _allSettings = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json) 
                            ?? new Dictionary<string, Dictionary<string, string>>();
                    }
                }
                catch
                {
                    _allSettings = new Dictionary<string, Dictionary<string, string>>();
                }
            }
        }

        private static void Save()
        {
            lock (_lock)
            {
                try
                {
                    if (!Directory.Exists(SettingsFolder))
                        Directory.CreateDirectory(SettingsFolder);

                    var options = new JsonSerializerOptions { WriteIndented = true };
                    string json = JsonSerializer.Serialize(_allSettings, options);
                    File.WriteAllText(SettingsFilePath, json);
                }
                catch
                {
                    // Silently fail if we can't save
                }
            }
        }

        /// <summary>
        /// Get a setting value for a specific view.
        /// </summary>
        public static string Get(string viewName, string key, string defaultValue = "")
        {
            lock (_lock)
            {
                if (_allSettings.TryGetValue(viewName, out var viewSettings))
                {
                    if (viewSettings.TryGetValue(key, out var value))
                        return value;
                }
                return defaultValue;
            }
        }

        /// <summary>
        /// Get a setting value as integer.
        /// </summary>
        public static int GetInt(string viewName, string key, int defaultValue = 0)
        {
            string val = Get(viewName, key, defaultValue.ToString());
            return int.TryParse(val, out int result) ? result : defaultValue;
        }

        /// <summary>
        /// Get a setting value as double.
        /// </summary>
        public static double GetDouble(string viewName, string key, double defaultValue = 0)
        {
            string val = Get(viewName, key, defaultValue.ToString());
            return double.TryParse(val, out double result) ? result : defaultValue;
        }

        /// <summary>
        /// Get a setting value as boolean.
        /// </summary>
        public static bool GetBool(string viewName, string key, bool defaultValue = false)
        {
            string val = Get(viewName, key, defaultValue.ToString());
            return bool.TryParse(val, out bool result) ? result : defaultValue;
        }

        /// <summary>
        /// Set a setting value for a specific view.
        /// </summary>
        public static void Set(string viewName, string key, string value)
        {
            lock (_lock)
            {
                if (!_allSettings.ContainsKey(viewName))
                    _allSettings[viewName] = new Dictionary<string, string>();

                _allSettings[viewName][key] = value;
            }
        }

        /// <summary>
        /// Set multiple values at once.
        /// </summary>
        public static void Set(string viewName, Dictionary<string, string> values)
        {
            lock (_lock)
            {
                if (!_allSettings.ContainsKey(viewName))
                    _allSettings[viewName] = new Dictionary<string, string>();

                foreach (var kvp in values)
                    _allSettings[viewName][kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// Save all settings to disk. Call this when closing a dialog with "Generate".
        /// </summary>
        public static void SaveAll()
        {
            Save();
        }
    }
}
