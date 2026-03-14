using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Utility to audit ribbon.yaml against Resources/Icons.
    /// Ensures every tool has its individual (32x32) icon.
    /// </summary>
    public static class IconAuditor
    {
        public static void RunAudit(string projectRoot)
        {
            string yamlPath = Path.Combine(projectRoot, "Resources", "ribbon.yaml");
            string iconsDir = Path.Combine(projectRoot, "Resources", "Icons");

            if (!File.Exists(yamlPath)) return;

            string[] yamlLines = File.ReadAllLines(yamlPath);
            var iconMatches = yamlLines
                .Where(l => l.Trim().StartsWith("icon:"))
                .Select(l => l.Split(':')[1].Trim())
                .Distinct()
                .ToList();

            Console.WriteLine($"--- Icon Audit Start ---");
            foreach (var iconName in iconMatches)
            {
                string expectedFile = $"{iconName}(32x32).png";
                string fullPath = Path.Combine(iconsDir, expectedFile);

                if (!File.Exists(fullPath))
                {
                    Console.WriteLine($"[MISSING] Icon: {iconName} -> Expected: {expectedFile}");
                }
            }
            Console.WriteLine($"--- Icon Audit End ---");
        }
    }
}
