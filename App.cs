using Autodesk.Revit.UI;
using System;
using System.Reflection;
using System.Windows.Media.Imaging;
using antiGGGravity.Utilities;

namespace antiGGGravity
{
    public class App : IExternalApplication
    {
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
                // Clean up any global resources or event handlers here
                // For now, ensuring we return Succeeded
                return Result.Succeeded;
            }
            catch (Exception)
            {
                return Result.Failed;
            }
        }
    }
}
