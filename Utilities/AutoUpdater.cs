using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace antiGGGravity.Utilities
{
    /// <summary>
    /// Handles automatic updates from GitHub Releases.
    /// 
    /// Flow:
    ///   1. User clicks "Check Update" → CheckForUpdateAsync() hits GitHub API
    ///   2. If newer version found → DownloadUpdateAsync() downloads zip to staging
    ///   3. On next Revit startup → ApplyStagedUpdate() copies staged files over installed ones
    /// 
    /// All operations are wrapped in try/catch to never crash Revit.
    /// </summary>
    public static class AutoUpdater
    {
        private static readonly HttpClient _client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        // Use LocalAppData so updates are per-machine (not roamed)
        private static readonly string UpdateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "antiGGGravity", "updates");

        private static readonly string StagedDir = Path.Combine(UpdateRoot, "staged");
        private static readonly string DownloadDir = Path.Combine(UpdateRoot, "download");
        private static readonly string VersionFile = Path.Combine(StagedDir, "version.txt");

        static AutoUpdater()
        {
            // GitHub API requires a User-Agent header
            if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
                _client.DefaultRequestHeaders.Add("User-Agent", "antiGGGravity-Updater");
        }

        #region Apply Staged Update (runs on Revit startup)

        /// <summary>
        /// Checks if a staged update exists. If so, starts a background process
        /// that waits for Revit to exit and then swaps the files.
        /// This is the only way to update the DLLs since Revit locks them while running.
        /// </summary>
        public static string ApplyStagedUpdate()
        {
            try
            {
                if (!File.Exists(VersionFile))
                    return null;

                var stagedVersion = File.ReadAllText(VersionFile).Trim();
                if (string.IsNullOrEmpty(stagedVersion))
                    return null;

                var installDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(installDir)) return null;

                // Create a small PowerShell script to handle the swap after Revit closes
                var revitPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                
                // Escape paths for PowerShell
                var psStagedDir = StagedDir.Replace("'", "''");
                var psInstallDir = installDir.Replace("'", "''");

                var script = $@"
$ErrorActionPreference = 'SilentlyContinue'
$revitPid = {revitPid}
$stagedDir = '{psStagedDir}'
$installDir = '{psInstallDir}'

# 1. Wait for Revit to exit
$process = Get-Process -Id $revitPid
if ($process) {{
    $process | Wait-Process
}}

# 2. Brief wait to ensure file handles are released
Start-Sleep -Seconds 2

# 3. Swap the files
if (Test-Path $stagedDir) {{
    # Copy staged files over installed ones (excluding the version marker)
    Get-ChildItem -Path $stagedDir -Exclude 'version.txt' | ForEach-Object {{
        Copy-Item -Path $_.FullName -Destination $installDir -Force -Recurse
    }}
    
    # 4. Clean up staging directory
    Remove-Item -Path $stagedDir -Recurse -Force
}}
";

                // Launch PowerShell hidden
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script.Replace("\"", "\\\"")}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                System.Diagnostics.Process.Start(psi);

                return $"Update v{stagedVersion} staged. Will be applied after Revit closes.";
            }
            catch (Exception ex)
            {
                return $"Update failed to stage: {ex.Message}";
            }
        }

        #endregion

        #region Check for Update (user-initiated)

        /// <summary>
        /// Checks the public releases repository for a newer version by reading version.txt.
        /// Returns an UpdateInfo with the result, or null if check failed.
        /// </summary>
        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            try
            {
                var repo = antiGGGravity.Resources.Branding.GITHUB_REPO;
                
                // Construct raw URLs
                // Format: https://raw.githubusercontent.com/OWNER/REPO/BRANCH/PATH
                var baseUrl = $"https://raw.githubusercontent.com/{repo}/main";
                var versionUrl = $"{baseUrl}/version.txt";
                var downloadUrl = $"{baseUrl}/antiGGGravity_Installer_AllVersions.zip";

                // 1. Fetch the latest version number
                var response = await _client.GetAsync(versionUrl).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    return new UpdateInfo
                    {
                        HasError = true,
                        ErrorMessage = response.StatusCode == System.Net.HttpStatusCode.NotFound
                            ? "Updates are currently unavailable. No version.txt found in the public repo."
                            : $"Network error: {response.StatusCode}"
                    };
                }

                var remoteVersionStr = (await response.Content.ReadAsStringAsync().ConfigureAwait(false)).Trim();
                
                // Parse versions for comparison
                var remoteVersion = ParseVersion(remoteVersionStr);
                var localVersion = ParseVersion(antiGGGravity.Resources.Branding.VERSION);

                var isNewer = remoteVersion != null && localVersion != null && remoteVersion > localVersion;

                return new UpdateInfo
                {
                    IsUpdateAvailable = isNewer,
                    CurrentVersion = antiGGGravity.Resources.Branding.VERSION,
                    LatestVersion = remoteVersionStr.TrimStart('v', 'V'),
                    ReleaseName = $"Version {remoteVersionStr}",
                    ReleaseNotes = "Performance improvements and new features.",
                    DownloadUrl = downloadUrl
                };
            }
            catch (TaskCanceledException)
            {
                return new UpdateInfo { HasError = true, ErrorMessage = "Connection timed out. Check your internet connection." };
            }
            catch (HttpRequestException ex)
            {
                return new UpdateInfo { HasError = true, ErrorMessage = $"Network error: {ex.Message}" };
            }
            catch (Exception ex)
            {
                return new UpdateInfo { HasError = true, ErrorMessage = $"Update check failed: {ex.Message}" };
            }
        }

        #endregion

        #region Download Update (user-initiated)

        /// <summary>
        /// Downloads the update zip and extracts the correct Revit version
        /// to the staging directory. Call after user confirms they want to update.
        /// 
        /// revitVersion should be the year string like "2026" — used to pick
        /// the correct subfolder from the zip (e.g., "R2026/").
        /// </summary>
        public static async Task<DownloadResult> DownloadUpdateAsync(
            string downloadUrl, string newVersion, string revitVersion,
            IProgress<double> progress = null)
        {
            try
            {
                // Clean up any previous staging/download
                CleanDirectory(StagedDir);
                CleanDirectory(DownloadDir);

                Directory.CreateDirectory(DownloadDir);
                Directory.CreateDirectory(StagedDir);

                var zipPath = Path.Combine(DownloadDir, "update.zip");

                // Download the zip with progress
                using (var response = await _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1;

                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;

                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                            totalRead += bytesRead;

                            if (totalBytes > 0)
                                progress?.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }

                // Extract the zip
                var extractDir = Path.Combine(DownloadDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Find the correct Revit version subfolder (e.g., "R2026/antiGGGravity/")
                var versionFolder = $"R{revitVersion}";
                var addinSubDir = Path.Combine(extractDir, versionFolder, "antiGGGravity");

                if (!Directory.Exists(addinSubDir))
                {
                    // Fallback: try just the antiGGGravity folder at root level
                    addinSubDir = Path.Combine(extractDir, "antiGGGravity");
                }

                if (!Directory.Exists(addinSubDir))
                {
                    // Last fallback: if the zip is flat (files directly in root)
                    addinSubDir = extractDir;
                }

                // Copy the DLLs to staged directory
                CopyDirectory(addinSubDir, StagedDir);

                // Write the version marker (signals that staging is complete and ready)
                File.WriteAllText(VersionFile, newVersion.TrimStart('v', 'V'));

                // Clean up download directory
                try { Directory.Delete(DownloadDir, recursive: true); } catch { }

                return new DownloadResult { Success = true };
            }
            catch (Exception ex)
            {
                // Clean up on failure
                try { CleanDirectory(StagedDir); } catch { }
                try { CleanDirectory(DownloadDir); } catch { }

                return new DownloadResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        /// <summary>
        /// Checks if a staged update is waiting to be applied.
        /// </summary>
        public static bool HasStagedUpdate()
        {
            return File.Exists(VersionFile);
        }

        /// <summary>
        /// Gets the version of the staged update, or null if none.
        /// </summary>
        public static string GetStagedVersion()
        {
            try
            {
                return File.Exists(VersionFile) ? File.ReadAllText(VersionFile).Trim() : null;
            }
            catch { return null; }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Parses a version string like "1.2.0" or "v1.2.0" into a comparable Version object.
        /// </summary>
        private static Version ParseVersion(string versionStr)
        {
            if (string.IsNullOrEmpty(versionStr)) return null;

            // Strip leading 'v' or 'V'
            versionStr = versionStr.TrimStart('v', 'V');

            return Version.TryParse(versionStr, out var version) ? version : null;
        }

        private static void CleanDirectory(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }

        #endregion
    }

    /// <summary>
    /// Result of checking for updates.
    /// </summary>
    public class UpdateInfo
    {
        public bool IsUpdateAvailable { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseName { get; set; }
        public string ReleaseNotes { get; set; }
        public string PublishedAt { get; set; }
        public string DownloadUrl { get; set; }
        public double DownloadSizeMB { get; set; }
    }

    /// <summary>
    /// Result of downloading an update.
    /// </summary>
    public class DownloadResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}
