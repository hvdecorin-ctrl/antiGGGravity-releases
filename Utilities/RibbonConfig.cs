using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Collections.Generic;
using System.IO;
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
    /// Loads ribbon configuration from embedded YAML resource.
    /// </summary>
    public static class RibbonConfigLoader
    {
        private const string ResourceName = "antiGGGravity.Resources.ribbon.yaml";

        /// <summary>
        /// Load and parse the embedded ribbon.yaml configuration.
        /// </summary>
        public static RibbonConfiguration Load()
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            using (var stream = assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    // List available resources for debugging
                    var available = string.Join(", ", assembly.GetManifestResourceNames());
                    throw new FileNotFoundException(
                        $"Embedded resource '{ResourceName}' not found. Available: {available}");
                }

                using (var reader = new StreamReader(stream))
                {
                    var yaml = reader.ReadToEnd();
                    
                    var deserializer = new DeserializerBuilder()
                        .WithNamingConvention(CamelCaseNamingConvention.Instance)
                        .IgnoreUnmatchedProperties()
                        .Build();

                    return deserializer.Deserialize<RibbonConfiguration>(yaml);
                }
            }
        }
    }
}
