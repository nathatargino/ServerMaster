namespace ServerMaster.Core.Models;

/// <summary>
/// Immutable snapshot of hardware usage by the server process.
/// Emitted every second while the server is running.
/// </summary>
public sealed record ResourceSnapshot(
    DateTimeOffset Timestamp,
    double CpuPercent,
    long RamBytes
)
{
    public long RamMb => RamBytes / 1_048_576;
}

/// <summary>
/// A single log line emitted by the game server process.
/// </summary>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    LogLevel Level,
    string Message
);

/// <summary>
/// Current state report from the network tunnel service.
/// </summary>
public sealed record TunnelStatus(
    TunnelState State,
    string? PublicAddress = null,
    string? ErrorMessage = null
);

/// <summary>
/// Resolved information about an established tunnel connection.
/// </summary>
public sealed record TunnelInfo(
    string PublicAddress,
    int PublicPort,
    string Protocol   // "tcp" or "udp"
);
