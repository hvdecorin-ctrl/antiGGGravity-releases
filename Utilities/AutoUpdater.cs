using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

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

        private static readonly string UpdateRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "antiGGGravity", "updates");

        private static string GetStagedDir(string revitVersion = null)
        {
            if (string.IsNullOrEmpty(revitVersion))
            {
                // Root staging for current version
                revitVersion = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileVersionInfo.FileDescription.Contains("20") 
                    ? GetCurrentRevitYear() : "2026";
            }
            return Path.Combine(UpdateRoot, "staged", $"R{revitVersion}");
        }

        private static string GetVersionFile(string revitVersion = null) => Path.Combine(GetStagedDir(revitVersion), "version.txt");
        private static readonly string DownloadDir = Path.Combine(UpdateRoot, "download");

        private static string GetCurrentRevitYear()
        {
            try { return System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.Split('\\').Last(x => x.StartsWith("Revit 20")).Replace("Revit ", ""); }
            catch { return "2026"; }
        }

        static AutoUpdater()
        {
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
                var currentYear = GetCurrentRevitYear();
                var stagedDir = GetStagedDir(currentYear);
                var versionFile = GetVersionFile(currentYear);

                if (!File.Exists(versionFile))
                    return null;

                var stagedVersion = File.ReadAllText(versionFile).Trim();
                var installDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(installDir)) return null;

                // 1. Verify Signature of staged assembly BEFORE applying
                var stagedDll = Path.Combine(stagedDir, "antiGGGravity.dll");
                if (!VerifySignature(stagedDll))
                {
                    LogFailure($"Staged update for v{stagedVersion} failed signature check. Aborting.");
                    Directory.Delete(stagedDir, true);
                    return "Update failed: Invalid signature.";
                }

                var revitPid = System.Diagnostics.Process.GetCurrentProcess().Id;
                var psStagedDir = stagedDir.Replace("'", "''");
                var psInstallDir = installDir.Replace("'", "''");

                var script = $@"
$ErrorActionPreference = 'SilentlyContinue'
$revitPid = {revitPid}
$stagedDir = '{psStagedDir}'
$installDir = '{psInstallDir}'

# 1. Wait for Revit to exit
$process = Get-Process -Id $revitPid -ErrorAction SilentlyContinue
if ($process) {{ $process | Wait-Process }}

Start-Sleep -Seconds 2

# 2. Swap files
if (Test-Path $stagedDir) {{
    Get-ChildItem -Path $stagedDir -Exclude 'version.txt' | ForEach-Object {{
        Copy-Item -Path $_.FullName -Destination $installDir -Force -Recurse
    }}
    Remove-Item -Path $stagedDir -Recurse -Force
}}
";
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

        private static bool VerifySignature(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                var name = AssemblyName.GetAssemblyName(path);
                var token = name.GetPublicKeyToken();
                var expected = new byte[] { 0x0c, 0x3e, 0x48, 0x43, 0x90, 0xfc, 0xb3, 0x09 };
                return token != null && System.Linq.Enumerable.SequenceEqual(token, expected);
            }
            catch { return false; }
        }

        private static void LogFailure(string msg)
        {
            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), "agg_updater.log"), $"[{DateTime.Now}] {msg}\n"); } catch { }
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
            string downloadUrl, string newVersion, bool allVersions = false, 
            IProgress<double> progress = null)
        {
            try
            {
                CleanDirectory(DownloadDir);
                Directory.CreateDirectory(DownloadDir);

                var zipPath = Path.Combine(DownloadDir, "update.zip");

                using (var response = await _client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    var totalBytes = response.Content.Headers.ContentLength ?? -1;
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var fileStream = new FileStream(zipPath, FileMode.Create))
                    {
                        var buffer = new byte[8192];
                        long totalRead = 0;
                        int bytesRead;
                        while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, bytesRead).ConfigureAwait(false);
                            totalRead += bytesRead;
                            if (totalBytes > 0) progress?.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }

                var extractDir = Path.Combine(DownloadDir, "extracted");
                ZipFile.ExtractToDirectory(zipPath, extractDir);

                // Determine which versions to stage
                var versionsToStage = allVersions 
                    ? new[] { "2022", "2023", "2024", "2025", "2026", "2027" } 
                    : new[] { GetCurrentRevitYear() };

                int successfullyStaged = 0;
                foreach (var v in versionsToStage)
                {
                    var addinSubDir = Path.Combine(extractDir, $"R{v}", "antiGGGravity");
                    if (!Directory.Exists(addinSubDir)) continue;

                    // Verify signature of the DLL in the zip before staging
                    if (!VerifySignature(Path.Combine(addinSubDir, "antiGGGravity.dll"))) continue;

                    var stagedDir = GetStagedDir(v);
                    CleanDirectory(stagedDir);
                    CopyDirectory(addinSubDir, stagedDir);
                    File.WriteAllText(GetVersionFile(v), newVersion.TrimStart('v', 'V'));
                    successfullyStaged++;
                }

                try { Directory.Delete(DownloadDir, true); } catch { }

                if (successfullyStaged == 0)
                    return new DownloadResult { Success = false, ErrorMessage = "No compatible versions found in update package or signature verification failed." };

                return new DownloadResult { Success = true };
            }
            catch (Exception ex)
            {
                return new DownloadResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public static bool HasStagedUpdate() => File.Exists(GetVersionFile(GetCurrentRevitYear()));

        public static string GetStagedVersion()
        {
            try { return HasStagedUpdate() ? File.ReadAllText(GetVersionFile(GetCurrentRevitYear())).Trim() : null; }
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
