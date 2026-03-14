using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Root configuration for the ribbon tab.
    /// </summary>
    public class RibbonConfiguration
    {
        public TabConfig Tab { get; set; }
        public List<PanelConfig> Panels { get; set; } = new();
    }

    /// <summary>
    /// Tab configuration.
    /// </summary>
    public class TabConfig
    {
        public string Name { get; set; }
    }

    /// <summary>
    /// Panel configuration - appears as a group on the ribbon.
    /// </summary>
    public class PanelConfig
    {
        public string Name { get; set; }
        public List<PanelItemConfig> Items { get; set; } = new();
    }

    /// <summary>
    /// An item within a panel (stacked buttons, pulldown, or single button).
    /// </summary>
    public class PanelItemConfig
    {
        public string Type { get; set; } // "stacked", "pulldown", "button"
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public string Command { get; set; }
        public List<ButtonConfig> Buttons { get; set; } = new();
    }

    /// <summary>
    /// Button configuration within a pulldown or stacked group.
    /// </summary>
    public class ButtonConfig
    {
        public string Type { get; set; } // "pulldown", "pushbutton"
        public string Name { get; set; }
        public string Command { get; set; }
        public string Icon { get; set; }
        public string Tooltip { get; set; }
        public bool Separator { get; set; } // If true, this is a separator, not a button
        public List<ButtonConfig> Buttons { get; set; } = new(); // Nested buttons for pulldowns
    }

    /// <summary>
    /// Loads ribbon configuration from modular embedded YAML resources.
    /// </summary>
    public static class RibbonConfigLoader
    {
        private const string ResourcePrefix = "antiGGGravity.Resources.ribbon.";

        /// <summary>
        /// Load and parse all embedded ribbon.*.yaml configurations.
        /// </summary>
        public static RibbonConfiguration Load()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resources = assembly.GetManifestResourceNames().OrderBy(x => x).ToList();
            
            var combinedConfig = new RibbonConfiguration();
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            foreach (var resource in resources)
            {
                if (!resource.StartsWith(ResourcePrefix) || !resource.EndsWith(".yaml"))
                    continue;

                using (var stream = assembly.GetManifestResourceStream(resource))
                {
                    if (stream == null) continue;

                    using (var reader = new StreamReader(stream))
                    {
                        var yaml = reader.ReadToEnd();
                        var configPart = deserializer.Deserialize<RibbonConfiguration>(yaml);
                        
                        if (configPart == null) continue;

                        // Set tab name from the first part that has it
                        if (combinedConfig.Tab == null && configPart.Tab != null)
                        {
                            combinedConfig.Tab = configPart.Tab;
                        }

                        // Accumulate panels
                        if (configPart.Panels != null)
                        {
                            combinedConfig.Panels.AddRange(configPart.Panels);
                        }
                    }
                }
            }

            if (combinedConfig.Tab == null)
            {
                combinedConfig.Tab = new TabConfig { Name = "antiGGGravity" };
            }

            return combinedConfig;
        }
    }
}
