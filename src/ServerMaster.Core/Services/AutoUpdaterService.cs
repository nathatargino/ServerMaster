using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;

namespace ServerMaster.Core.Services;

public sealed class AutoUpdaterService
{
    // The target github repository to poll for updates
    private const string RepoOwner = "nathatargino";
    private const string RepoName = "ServerMaster";
    
    // Fallback static version if AssemblyInformationalVersion is null.
    // Set to 1.0.0 initially so any push will be considered an update by the timestamp rule.
    private static string GetCurrentVersion()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return "1.0.0";
        var fvi = FileVersionInfo.GetVersionInfo(exePath);
        return fvi.FileVersion ?? "1.0.0";
    }

    /// <summary>
    /// Deletes residual update files (.old.exe) if they persist after a swap.
    /// </summary>
    public static void CleanupOldFiles()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        var currentExe = Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(currentExe)) return;
        
        var oldExe = currentExe.Replace(".exe", ".old.exe");
        if (File.Exists(oldExe))
        {
            try
            {
                File.Delete(oldExe);
            }
            catch (Exception)
            {
                // File might still be locked by Windows closing process; ignore.
            }
        }
    }

    /// <summary>
    /// Queries GitHub API for the latest release asset and performs hot-swapping if available.
    /// </summary>
    public static async Task CheckAndUpdateAsync()
    {
#if DEBUG
        return;
#endif

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ServerMasterApp", "1.0"));

            var apiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var response = await http.GetAsync(apiUrl);
            
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Expected tag: v1.0.0-2026...
            var latestTag = root.GetProperty("tag_name").GetString();
            var currentVersion = GetCurrentVersion();
            
            if (string.IsNullOrEmpty(latestTag)) return;
            
            if (latestTag == currentVersion || latestTag == "v" + currentVersion || latestTag.StartsWith("v" + currentVersion + "-")) return;

            // Locate the .exe asset
            var assets = root.GetProperty("assets");
            string downloadUrl = string.Empty;
            foreach (var asset in assets.EnumerateArray())
            {
                var assetName = asset.GetProperty("name").GetString();
                if (assetName != null && assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                {
                    downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl)) return; // No .exe asset found

            // Actually Download the File
            var currentExePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath)) return;

            var oldExePath = currentExePath.Replace(".exe", ".old.exe");
            
            // Delete old if it exists from previous manual failures
            if (File.Exists(oldExePath)) File.Delete(oldExePath);
            
            var exeBytes = await http.GetByteArrayAsync(downloadUrl);

            // HOT-SWAPPING (Windows allows renaming running executables)
            File.Move(currentExePath, oldExePath);
            
            // Write the new executable to exactly the standard path
            await File.WriteAllBytesAsync(currentExePath, exeBytes);

            // Execute the freshly downloaded app and die
            Process.Start(currentExePath);
            Environment.Exit(0);
        }
        catch (Exception)
        {
            // Silently fail if updater network fails
        }
    }
}


