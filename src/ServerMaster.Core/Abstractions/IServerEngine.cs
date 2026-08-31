using ServerMaster.Core.Models;

namespace ServerMaster.Core.Abstractions;

/// <summary>
/// Contract that every game server engine must implement.
/// Follows the Strategy pattern so Minecraft and Hytale are fully interchangeable.
/// </summary>
public interface IServerEngine
{
    /// <summary>The game this engine handles.</summary>
    GameType GameType { get; }

    /// <summary>Current running state of the server process.</summary>
    ServerState State { get; }

    /// <summary>Hot observable stream of console log lines.</summary>
    IObservable<LogEntry> LogStream { get; }

    /// <summary>Hot observable stream of CPU / RAM snapshots (1-second interval).</summary>
    IObservable<ResourceSnapshot> ResourceStream { get; }

    /// <summary>
    /// Injects the profile representing the server state.
    /// Must be called after factory creation if not running PrepareAsync.
    /// </summary>
    void Initialize(ServerProfile profile);

    /// <summary>
    /// Downloads required server files and writes initial configuration.
    Task PrepareAsync(ServerProfile profile, IProgress<string>? progress = null, CancellationToken ct = default);

    /// <summary>Starts the server process.</summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>Sends a graceful stop command to the server process.</summary>
    Task StopAsync(CancellationToken ct = default);

    /// <summary>Sends a raw command string to the server's stdin.</summary>
    Task SendCommandAsync(string command, CancellationToken ct = default);
}
