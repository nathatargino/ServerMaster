using ServerMaster.Core.Models;

namespace ServerMaster.Core.Abstractions;

public interface IBackupService
{
    Task<bool> RunBackupAsync(ServerProfile profile, Action<string> onProgress);
    Task<bool> AuthenticateGoogleDriveAsync();
    Task<bool> IsGoogleDriveAuthenticatedAsync();
    Task LogoutGoogleDriveAsync();
}
