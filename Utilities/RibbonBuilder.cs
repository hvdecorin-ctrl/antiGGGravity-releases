using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Media.Imaging;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Builds the Revit ribbon UI from YAML configuration.
    /// </summary>
    public class RibbonBuilder
    {
        private readonly string _assemblyPath;
        private readonly string _currentPanelName;
        private readonly string _currentPulldownName;

        public RibbonBuilder()
        {
            _assemblyPath = Assembly.GetExecutingAssembly().Location;
        }

        /// <summary>
        /// Build the complete ribbon from configuration.
        /// </summary>
        public void Build(UIControlledApplication application, RibbonConfiguration config)
        {
            // Create the tab
            string tabName = config.Tab.Name;
            application.CreateRibbonTab(tabName);

            // Create panels in order specified in YAML
            foreach (var panelConfig in config.Panels)
            {
                RibbonPanel panel = application.CreateRibbonPanel(tabName, panelConfig.Name);
                BuildPanelItems(panel, panelConfig);
            }
        }

        /// <summary>
        /// Build items within a panel.
        /// </summary>
        private void BuildPanelItems(RibbonPanel panel, PanelConfig panelConfig)
        {
            foreach (var item in panelConfig.Items)
            {
                switch (item.Type?.ToLower())
                {
                    case "stacked":
                        BuildStackedButtons(panel, item, panelConfig.Name);
                        break;
                    case "pulldown":
                        BuildPulldown(panel, item, panelConfig.Name);
                        break;
                    case "pushbutton":
                    case "button":
                        BuildPushButton(panel, item, panelConfig.Name);
                        break;
                    default:
                        // Default to button if type not specified
                        if (!string.IsNullOrEmpty(item.Name))
                            BuildPushButton(panel, item, panelConfig.Name);
                        break;
                }
            }
        }

        /// <summary>
        /// Build stacked buttons (2 or 3 small buttons vertically).
        /// </summary>
        private void BuildStackedButtons(RibbonPanel panel, PanelItemConfig item, string panelName)
        {
            var buttonDataList = new List<RibbonItemData>();
            var itemConfigs = new List<ButtonConfig>();

            foreach (var btn in item.Buttons)
            {
                if (btn.Separator) continue;

                if (btn.Type?.ToLower() == "pulldown")
                {
                    buttonDataList.Add(CreatePulldownButtonData(btn));
                }
                else
                {
                    buttonDataList.Add(CreatePushButtonData(btn, panelName, null));
                }
                itemConfigs.Add(btn);
            }

            // AddStackedItems requires 2 or 3 items. If only 1, add as a standard item.
            IList<RibbonItem> items = null;
            if (buttonDataList.Count == 1)
            {
                var ribbonItem = panel.AddItem(buttonDataList[0]);
                if (ribbonItem is PulldownButton pulldown && itemConfigs[0].Type?.ToLower() == "pulldown")
                {
                    PopulatePulldown(pulldown, itemConfigs[0], panelName);
                }
            }
            else if (buttonDataList.Count == 2)
            {
                items = panel.AddStackedItems(buttonDataList[0], buttonDataList[1]);
            }
            else if (buttonDataList.Count >= 3)
            {
                items = panel.AddStackedItems(buttonDataList[0], buttonDataList[1], buttonDataList[2]);
            }

            // Populate pulldowns if any were created
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] is PulldownButton pulldown && itemConfigs[i].Type?.ToLower() == "pulldown")
                    {
                        PopulatePulldown(pulldown, itemConfigs[i], panelName);
                    }
                }
            }
        }

        /// <summary>
        /// Build a pulldown button with nested buttons.
        /// </summary>
        private void BuildPulldown(RibbonPanel panel, PanelItemConfig item, string panelName)
        {
            var pullData = CreatePulldownButtonData(new ButtonConfig { Name = item.Name, Tooltip = item.Tooltip, Icon = item.Icon });
            PulldownButton pulldown = panel.AddItem(pullData) as PulldownButton;
            
            // Map PanelItemConfig to ButtonConfig for reuse of PopulatePulldown
            var btnConfig = new ButtonConfig
            {
                Name = item.Name,
                Tooltip = item.Tooltip,
                Icon = item.Icon,
                Buttons = item.Buttons
            };

            PopulatePulldown(pulldown, btnConfig, panelName);
        }

        /// <summary>
        /// Creates PulldownButtonData from configuration.
        /// </summary>
        private PulldownButtonData CreatePulldownButtonData(ButtonConfig btn)
        {
            string displayName = btn.Name.Replace(" ", "\n");
            return new PulldownButtonData(btn.Name.Replace(" ", "").Replace("&", "And"), displayName);
        }

        /// <summary>
        /// Populates a pulldown button with its children.
        /// </summary>
        private void PopulatePulldown(PulldownButton pulldown, ButtonConfig config, string panelName)
        {
            if (pulldown == null) return;

            if (!string.IsNullOrEmpty(config.Tooltip))
                pulldown.ToolTip = config.Tooltip;

            // Set pulldown icons
            if (!string.IsNullOrEmpty(config.Icon))
            {
                pulldown.LargeImage = GetLargeIcon(panelName, null, config.Icon);
                pulldown.Image = GetSmallIcon(panelName, null, config.Icon);
            }

            // Add buttons to pulldown
            foreach (var btn in config.Buttons)
            {
                if (btn.Separator)
                {
                    pulldown.AddSeparator();
                }
                else
                {
                    pulldown.AddPushButton(CreatePushButtonData(btn, panelName, config.Name));
                }
            }
        }

        /// <summary>
        /// Build a single large pushbutton.
        /// </summary>
        private void BuildPushButton(RibbonPanel panel, PanelItemConfig item, string panelName)
        {
            if (item.Buttons.Count > 0)
            {
                var btn = item.Buttons[0];
                var btnData = CreatePushButtonData(btn, panelName, null);
                panel.AddItem(btnData);
            }
            else if (!string.IsNullOrEmpty(item.Command))
            {
                // Create ButtonConfig from PanelItemConfig for reuse
                var btnConfig = new ButtonConfig
                {
                    Name = item.Name,
                    Command = item.Command,
                    Icon = item.Icon,
                    Tooltip = item.Tooltip
                };
                var btnData = CreatePushButtonData(btnConfig, panelName, null);
                panel.AddItem(btnData);
            }
        }

        /// <summary>
        /// Create PushButtonData from button configuration.
        /// </summary>
        private PushButtonData CreatePushButtonData(ButtonConfig btn, string panelName, string pulldownName)
        {
            // Format display name with newline for large buttons
            string displayName = btn.Name.Contains("\n") ? btn.Name : btn.Name.Replace(" ", "\n");
            
            // Safety: Remove characters forbidden by Revit API (% is definitely forbidden, filtering others just in case)
            displayName = displayName.Replace("%", " Pct").Replace("&", "And");
            
            // Build full command class name - intelligently discover if in sub-namespace
            string commandClass = ResolveCommandNamespace(btn.Command, panelName);

            var btnData = new PushButtonData(
                btn.Name.Replace(" ", "").Replace("-", "").Replace("&", "And"),
                displayName,
                _assemblyPath,
                commandClass);

            if (!string.IsNullOrEmpty(btn.Tooltip))
                btnData.ToolTip = btn.Tooltip;

            // Apply icons if specified or default to button name
            string iconName = btn.Icon ?? btn.Name;
            if (!string.IsNullOrEmpty(iconName))
            {
                btnData.LargeImage = GetLargeIcon(panelName, pulldownName, iconName);
                btnData.Image = GetSmallIcon(panelName, pulldownName, iconName);
            }

            return btnData;
        }

        /// <summary>
        /// Get large icon (32x32) for ribbon display.
        /// </summary>
        private BitmapSource GetLargeIcon(string panelName, string pulldownName, string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            return TryLoadIconBySize(panelName, pulldownName, iconName, 32);
        }

        /// <summary>
        /// Get small icon (32x32 scaled to 16x16) for QAT and small contexts.
        /// </summary>
        private BitmapSource GetSmallIcon(string panelName, string pulldownName, string iconName)
        {
            if (string.IsNullOrEmpty(iconName)) return null;
            var icon = TryLoadIconBySize(panelName, pulldownName, iconName, 32);
            return ScaleIcon(icon, 16);
        }

        private BitmapSource TryLoadIconBySize(string panelName, string pulldownName, string iconName, int size)
        {
            string sizeSuffix = "(32x32)";
            string filename = $"{iconName}{sizeSuffix}.png";

            var paths = new List<string>();
            string[] suffixes = { sizeSuffix, "" }; // Try with size suffix first, then without

            foreach (var suffix in suffixes)
            {
                string curFilename = $"{iconName}{suffix}.png";
                
                if (!string.IsNullOrEmpty(pulldownName))
                {
                    paths.Add($"Resources/Icons/{panelName}/{pulldownName}/{curFilename}");
                    paths.Add($"Resources/Icons/{pulldownName}/{curFilename}");
                }
                
                paths.Add($"Resources/Icons/{panelName}/{curFilename}");
                paths.Add($"Resources/Icons/{curFilename}");
            }

            foreach (var path in paths)
            {
                var icon = TryLoadIcon(path);
                if (icon != null) return icon;
            }
            return null;
        }

        /// <summary>
        /// Attempt to load an icon from the specified path.
        /// </summary>
        private BitmapSource TryLoadIcon(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;

            try
            {
                // Ensure separators are correct for Pack URI
                string cleanPath = relativePath.Replace("\\", "/").TrimStart('/');
                
                // For Pack URIs, spaces should be %20, but parentheses should be literal
                string escapedPath = cleanPath.Replace(" ", "%20");
                
                string uriString = $"pack://application:,,,/antiGGGravity;component/{escapedPath}";
                
                var image = new BitmapImage();
                image.BeginInit();
                image.UriSource = new Uri(uriString, UriKind.Absolute);
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.EndInit();
                
                // Normalize DPI to 96 so all icons render at consistent pixel size
                // (icons with high DPI like 300 would otherwise appear tiny in WPF)
                if (Math.Abs(image.DpiX - 96) > 1 || Math.Abs(image.DpiY - 96) > 1)
                {
                    int stride = image.PixelWidth * ((image.Format.BitsPerPixel + 7) / 8);
                    byte[] pixels = new byte[stride * image.PixelHeight];
                    image.CopyPixels(pixels, stride, 0);
                    
                    var normalized = BitmapSource.Create(
                        image.PixelWidth, image.PixelHeight,
                        96, 96, image.Format, image.Palette, pixels, stride);
                    normalized.Freeze();
                    return normalized;
                }
                
                return image;
            }
            catch
            {
                return null;
            }
        }
        /// <summary>
        /// Scales an icon to the target pixel size.
        /// </summary>
        private BitmapSource ScaleIcon(BitmapSource source, int targetSize)
        {
            if (source == null) return null;

            double scaleX = targetSize / (double)source.PixelWidth;
            double scaleY = targetSize / (double)source.PixelHeight;

            var scaledBitmap = new TransformedBitmap(source, new System.Windows.Media.ScaleTransform(scaleX, scaleY));
            return scaledBitmap;
        }

        /// <summary>
        /// Intelligently resolves the fully qualified name of a command class,
        /// checking both the root Commands namespace and panel-specific sub-namespaces.
        /// </summary>
        private string ResolveCommandNamespace(string commandName, string panelName)
        {
            // 1. Try traditional flat namespace first
            string flatName = $"antiGGGravity.Commands.{commandName}";
            if (Assembly.GetExecutingAssembly().GetType(flatName) != null) return flatName;

            // 2. Try panel-specific sub-namespace (converted from "Project Audit" to "ProjectAudit")
            string panelNamespace = panelName.Replace(" ", "");
            
            // Special case for Rebar to avoid clash with Revit class name
            // if (panelNamespace == "Rebar") panelNamespace = "RebarPanel";
            
            string nestedName = $"antiGGGravity.Commands.{panelNamespace}.{commandName}";
            if (Assembly.GetExecutingAssembly().GetType(nestedName) != null) return nestedName;

            // 3. Last resort: scan assembly for any type matching the name and implementing IExternalCommand
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.Name == commandName && typeof(IExternalCommand).IsAssignableFrom(type))
                {
                    return type.FullName;
                }
            }

            // Fallback to flat name if all else fails
            return flatName;
        }
    }
}
