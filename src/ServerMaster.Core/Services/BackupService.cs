using System.IO.Compression;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Drive.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using ServerMaster.Core.Abstractions;
using ServerMaster.Core.Models;
using File = System.IO.File;

namespace ServerMaster.Core.Services;

public class BackupService : IBackupService
{
    private static readonly string[] Scopes = { DriveService.Scope.DriveFile };
    private const string ApplicationName = "Server Master";
    private UserCredential? _credential;

    public async Task<bool> IsGoogleDriveAuthenticatedAsync()
    {
        var credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServerMaster", "Credentials");
        var store = new FileDataStore(credPath, true);
        var token = await store.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>("user");
        return token != null;
    }

    public async Task<bool> AuthenticateGoogleDriveAsync()
    {
        try
        {
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServerMaster");
            var credPath = Path.Combine(appDataPath, "Credentials");
            
            // Read client_secrets.json from the embedded assembly resource
            using var stream = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("ServerMaster.Core.Resources.client_secrets.json");
                
            if (stream == null)
            {
                throw new FileNotFoundException("O arquivo de credenciais embutido (client_secrets.json) não foi encontrado no assembly.");
            }

            _credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true));
                
            return _credential != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao autenticar no Google Drive: {ex.Message}");
            throw;
        }
    }

    public async Task LogoutGoogleDriveAsync()
    {
        var credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ServerMaster", "Credentials");
        var store = new FileDataStore(credPath, true);
        await store.ClearAsync();
        _credential = null;
    }

    public async Task<bool> RunBackupAsync(ServerProfile profile, Action<string> onProgress)
    {
        if (string.IsNullOrWhiteSpace(profile.Backup.LocalBackupDirectory) && !profile.Backup.EnableGoogleDriveBackup)
        {
            onProgress("Nenhum destino de backup configurado.");
            return false;
        }

        string tempZipPath = Path.Combine(Path.GetTempPath(), $"Backup_{profile.Name}_{DateTime.Now:yyyyMMdd_HHmmss}.zip");

        try
        {
            onProgress("Compactando arquivos do servidor...");
            if (File.Exists(tempZipPath)) File.Delete(tempZipPath);
            
            await Task.Run(() => CreateZipFromDirectorySafe(profile.ServerDirectory, tempZipPath));

            if (!string.IsNullOrWhiteSpace(profile.Backup.LocalBackupDirectory))
            {
                onProgress("Salvando backup local...");
                Directory.CreateDirectory(profile.Backup.LocalBackupDirectory);
                var destPath = Path.Combine(profile.Backup.LocalBackupDirectory, Path.GetFileName(tempZipPath));
                File.Copy(tempZipPath, destPath, true);
                
                onProgress("Aplicando política de retenção (5 dias)...");
                await CleanOldBackupsAsync(profile.Backup.LocalBackupDirectory, 5);
            }

            if (profile.Backup.EnableGoogleDriveBackup)
            {
                onProgress("Fazendo upload para o Google Drive...");
                if (_credential == null)
                {
                    await AuthenticateGoogleDriveAsync();
                }

                if (_credential != null)
                {
                    await UploadToGoogleDriveAsync(tempZipPath, profile.Backup.GoogleDriveFolderId);
                }
            }

            onProgress("Backup concluído com sucesso!");
            return true;
        }
        catch (Exception ex)
        {
            onProgress($"Erro no backup: {ex.Message}");
            return false;
        }
        finally
        {
            if (File.Exists(tempZipPath))
            {
                File.Delete(tempZipPath);
            }
        }
    }

    private Task CleanOldBackupsAsync(string directory, int daysToKeep)
    {
        return Task.Run(() =>
        {
            var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
            var files = Directory.GetFiles(directory, "*.zip");
            
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTime < cutoffDate)
                {
                    try
                    {
                        fileInfo.Delete();
                    }
                    catch
                    {
                        // Ignore delete errors
                    }
                }
            }
        });
    }

    private async Task UploadToGoogleDriveAsync(string filePath, string folderId)
    {
        var service = new DriveService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = _credential,
            ApplicationName = ApplicationName,
        });

        var fileMetadata = new Google.Apis.Drive.v3.Data.File()
        {
            Name = Path.GetFileName(filePath)
        };

        if (!string.IsNullOrWhiteSpace(folderId))
        {
            fileMetadata.Parents = new List<string> { folderId };
        }

        FilesResource.CreateMediaUpload request;
        await using (var stream = new FileStream(filePath, FileMode.Open))
        {
            request = service.Files.Create(fileMetadata, stream, "application/zip");
            request.Fields = "id";
            var response = await request.UploadAsync();
            if (response.Status == Google.Apis.Upload.UploadStatus.Failed)
            {
                throw new Exception(response.Exception?.Message ?? "Erro desconhecido no upload.");
            }
        }
    }

    private void CreateZipFromDirectorySafe(string sourceDirectoryName, string destinationArchiveFileName)
    {
        using var archive = ZipFile.Open(destinationArchiveFileName, ZipArchiveMode.Create);
        var dirInfo = new DirectoryInfo(sourceDirectoryName);
        var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            var entryName = Path.GetRelativePath(sourceDirectoryName, file.FullName).Replace('\\', '/');
            
            try
            {
                using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
                entry.LastWriteTime = file.LastWriteTime;
                
                using var es = entry.Open();
                fs.CopyTo(es);
            }
            catch (IOException)
            {
                // Ignorar arquivos que estão bloqueados (ex: logs sendo escritos no momento)
            }
        }
    }
}
