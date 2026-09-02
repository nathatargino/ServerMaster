using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ServerMaster.Core.Services;

public class JavaManagerService
{
    private static readonly string RuntimesRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ServerMaster", "Runtimes");

    public Task<string> EnsureJavaInstalledAsync(string gameVersion, Action<string> logEmit, CancellationToken ct = default)
    {
        int javaVersion = GetRequiredJavaVersion(gameVersion);
        return EnsureJavaInstalledAsync(javaVersion, logEmit, ct);
    }

    public async Task<string> EnsureJavaInstalledAsync(int javaVersion, Action<string> logEmit, CancellationToken ct = default)
    {
        string javaDir = Path.Combine(RuntimesRoot, $"Java-{javaVersion}");
        
        Directory.CreateDirectory(RuntimesRoot);
        
        var javaExe = FindJavaExe(javaDir);
        if (javaExe != null)
        {
            return javaExe; // Already downloaded and extracted
        }

        logEmit($"[ServerMaster] Baixando Java {javaVersion}...");
        
        string downloadUrlGA = $"https://api.adoptium.net/v3/binary/latest/{javaVersion}/ga/windows/x64/jre/hotspot/normal/eclipse";
        string downloadUrlEA = $"https://api.adoptium.net/v3/binary/latest/{javaVersion}/ea/windows/x64/jre/hotspot/normal/eclipse";
        
        string zipPath = Path.Combine(RuntimesRoot, $"java-{javaVersion}-temp.zip");

        try
        {
            using var http = new HttpClient();
            HttpResponseMessage? responseRef = null;
            
            try 
            {
                responseRef = await http.GetAsync(downloadUrlGA, HttpCompletionOption.ResponseHeadersRead, ct);
                responseRef.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                // Fallback to Early Access (EA) if GA is not found
                responseRef = await http.GetAsync(downloadUrlEA, HttpCompletionOption.ResponseHeadersRead, ct);
                responseRef.EnsureSuccessStatusCode();
            }

            using var response = responseRef;

            var totalBytes = response.Content.Headers.ContentLength;
            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            int lastProgressPercent = 0;

            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                totalRead += bytesRead;

                if (totalBytes.HasValue)
                {
                    int progressPercent = (int)((totalRead * 100) / totalBytes.Value);
                    if (progressPercent >= lastProgressPercent + 10) // Log every 10%
                    {
                        logEmit($"[ServerMaster] Baixando Java {javaVersion}... {progressPercent}%");
                        lastProgressPercent = progressPercent;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            throw new InvalidOperationException($"Falha ao baixar o Java {javaVersion}: {ex.Message}", ex);
        }

        logEmit($"[ServerMaster] Extraindo Java {javaVersion}...");
        
        try
        {
            if (Directory.Exists(javaDir))
            {
                Directory.Delete(javaDir, true);
            }
            Directory.CreateDirectory(javaDir);
            ZipFile.ExtractToDirectory(zipPath, javaDir, overwriteFiles: true);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Falha ao extrair o Java {javaVersion}: {ex.Message}", ex);
        }
        finally
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
        }

        var newJavaExe = FindJavaExe(javaDir);
        if (newJavaExe == null)
        {
            throw new InvalidOperationException($"O Java {javaVersion} foi extraído, mas o executável java.exe não foi encontrado na pasta: {javaDir}");
        }

        logEmit($"[ServerMaster] Java {javaVersion} pronto para uso!");
        return newJavaExe;
    }

    private string? FindJavaExe(string directory)
    {
        if (!Directory.Exists(directory)) return null;
        
        var exePath = Directory.GetFiles(directory, "java.exe", SearchOption.AllDirectories).FirstOrDefault();
        return exePath;
    }

    private int GetRequiredJavaVersion(string gameVersion)
    {
        // Parse "1.X.Y"
        var parts = gameVersion.Split('.');
        if (parts.Length < 2) return 21; // Default to modern Java if unknown format

        if (int.TryParse(parts[1], out int minor))
        {
            if (minor >= 21) return 21; // 1.21+
            
            if (minor == 20)
            {
                if (parts.Length >= 3 && int.TryParse(parts[2], out int patch))
                {
                    if (patch >= 5) return 21; // 1.20.5+ requires Java 21
                }
                return 17; // 1.20 to 1.20.4
            }

            if (minor >= 17) return 17; // 1.17 to 1.19
            if (minor == 16) return 17; // We can use Java 17 for 1.16 generally, but let's be safe (or just return 11/17)
            
            return 8; // 1.15 and older
        }

        return 21; // Default
    }
}
