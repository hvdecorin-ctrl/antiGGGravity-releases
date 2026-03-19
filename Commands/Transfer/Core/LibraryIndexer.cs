using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using antiGGGravity.Commands.Transfer.DTO;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;

namespace antiGGGravity.Commands.Transfer.Core
{
    public class LibraryIndexer
    {
        private static readonly string IndexDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "antiGGGravity", "Indexes");

        public static string GetIndexPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)) return null;
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(folderPath.ToLowerInvariant()));
                string hashStr = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                return Path.Combine(IndexDir, $"LibraryIndex_{hashStr}.json");
            }
        }

        public static List<FamilyManagerItem> BuildIndex(Application app, Document targetDoc, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return new List<FamilyManagerItem>();

            if (!Directory.Exists(IndexDir))
                Directory.CreateDirectory(IndexDir);

            // Use the existing engine to do the heavy lifting of scanning the folder
            var engine = new FamilyManagerEngine(app);
            var items = engine.ScanFolder(folderPath, targetDoc);

            // Save to JSON
            string indexPath = GetIndexPath(folderPath);
            try
            {
                string json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(indexPath, json);
            }
            catch { /* Silently fail cache save, worst case we return live data */ }

            return items;
        }

        public static List<FamilyManagerItem> LoadIndex(string folderPath)
        {
            try
            {
                string indexPath = GetIndexPath(folderPath);
                if (indexPath != null && File.Exists(indexPath))
                {
                    string json = File.ReadAllText(indexPath);
                    return JsonSerializer.Deserialize<List<FamilyManagerItem>>(json) ?? new List<FamilyManagerItem>();
                }
            }
            catch { }
            return null; // Return null if index doesn't exist or is corrupted, indicating a build is required
        }
    }
}
