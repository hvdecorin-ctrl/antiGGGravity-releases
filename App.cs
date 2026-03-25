using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Media.Imaging;
using antiGGGravity.Utilities;

namespace antiGGGravity
{
    public class App : IExternalApplication
    {
        // Store references to licensed buttons so we can enable/disable them
        private static readonly List<RibbonItem> _licensedButtons = new List<RibbonItem>();

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Pre-load global UI resources for all windows to prevent latency
                antiGGGravity.Utilities.SharedResources.Load();

                // Load ribbon configuration from embedded YAML
                var config = RibbonConfigLoader.Load();
                
                // Build ribbon UI from configuration
                var builder = new RibbonBuilder();
                builder.Build(application, config);

                // After ribbon is built, check license and disable buttons if needed
                DisableButtonsIfUnlicensed(application, config);
                
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // Log error and show message if ribbon creation fails
                System.Windows.MessageBox.Show(
                    $"Failed to create antiGGGravity ribbon:\n\n{ex.Message}\n\n{ex.StackTrace}",
                    $"{antiGGGravity.Resources.Branding.COMPANY_NAME} Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                return Result.Succeeded;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }

        /// <summary>
        /// Checks current license status and disables buttons for commands that require a license.
        /// Free commands (RequiresLicense=false) remain enabled.
        /// </summary>
        private void DisableButtonsIfUnlicensed(UIControlledApplication application, RibbonConfiguration config)
        {
            try
            {
                var license = LicenseValidator.ValidateLicense();
                if (license.IsValid) return; // Fully licensed — all buttons stay enabled

                // Find all ribbon panels we created and disable licensed buttons
                var panels = application.GetRibbonPanels(config.Tab.Name);
                if (panels == null) return;

                foreach (var panel in panels)
                {
                    foreach (var item in panel.GetItems())
                    {
                        DisableIfLicensed(item);
                    }
                }
            }
            catch
            {
                // Don't crash Revit if this fails — license is still enforced at command level
            }
        }

        /// <summary>
        /// Disables a ribbon item if the underlying command requires a license.
        /// Recursively handles pulldown buttons.
        /// </summary>
        private void DisableIfLicensed(RibbonItem item)
        {
            try
            {
                if (item is PulldownButton pulldown)
                {
                    // Check each button in the pulldown
                    foreach (var subItem in pulldown.GetItems())
                    {
                        DisableIfLicensed(subItem);
                    }
                    return;
                }

                // For push buttons, check if the command requires a license
                if (item is PushButton pushButton)
                {
                    var commandType = Assembly.GetExecutingAssembly().GetType(pushButton.ClassName);
                    if (commandType == null) return;

                    // Check if this command has RequiresLicense = true (the default)
                    var requiresProp = commandType.GetProperty("RequiresLicense",
                        BindingFlags.NonPublic | BindingFlags.Instance);

                    if (requiresProp != null)
                    {
                        var instance = Activator.CreateInstance(commandType);
                        var requiresLicense = (bool)requiresProp.GetValue(instance);
                        
                        if (requiresLicense)
                        {
                            pushButton.Enabled = false;
                            pushButton.ToolTip = (pushButton.ToolTip ?? "") + 
                                "\n\n🔒 Requires a valid license. Open License Manager to activate.";
                        }
                    }
                }
            }
            catch
            {
                // Silently continue — don't break the ribbon for one button
            }
        }
    }
}
