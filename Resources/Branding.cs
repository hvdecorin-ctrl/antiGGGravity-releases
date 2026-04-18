namespace antiGGGravity.Resources
{
    /// <summary>
    /// Centralized Branding Control for C# code.
    /// This file is located in the Resources folder for easy access.
    /// To change the brand name globally, update the COMPANY_NAME constant below 
    /// AND the 'CompanyName' string in Pre_BrandStyles.xaml.
    /// </summary>
    public static class Branding
    {
        public const string COMPANY_NAME = "antiGGGravity";

        /// <summary>
        /// Current add-in version. Bump this when tagging a new release.
        /// The auto-updater compares this against the latest GitHub release tag.
        /// Format: major.minor.patch (e.g., "1.2.0")
        /// </summary>
        public const string VERSION = "1.8.0";

        /// <summary>
        /// GitHub repository for the auto-updater to check for releases.
        /// This must be a PUBLIC repo (the main code repo is private).
        /// Only release assets (zip files) are published here — no source code.
        /// </summary>
        public const string GITHUB_REPO = "hvdecorin-ctrl/antiGGGravity-releases";
    }
}
