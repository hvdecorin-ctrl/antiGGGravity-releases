using System;
using System.Windows;

namespace antiGGGravity.Utilities
{
    public static class SharedResources
    {
        private static ResourceDictionary _globalResources;

        public static ResourceDictionary GlobalResources
        {
            get
            {
                if (_globalResources == null)
                {
                    Load();
                }
                return _globalResources;
            }
        }

        public static void Load()
        {
            if (_globalResources != null) return;

            try
            {
                var uri = new Uri("/antiGGGravity;component/Resources/Pre_BrandStyles.xaml", UriKind.RelativeOrAbsolute);
                _globalResources = Application.LoadComponent(uri) as ResourceDictionary;
            }
            catch (Exception ex)
            {
                // Fallback or log error
                _globalResources = new ResourceDictionary();
            }
        }
    }
}
