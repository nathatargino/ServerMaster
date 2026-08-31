namespace ServerMaster.Core.Models;

/// <summary>
/// Mutable data object describing a server. Passed through the wizard and stored to disk as JSON.
/// </summary>
public sealed record ServerProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();

    // ── Identity ────────────────────────────────────────────────────────────
    public string Name { get; set; } = "Meu Servidor";
    public string Description { get; set; } = string.Empty;
    public GameType Game { get; init; }
    public string GameMode { get; set; } = "survival";

    // ── Game-specific ────────────────────────────────────────────────────────
    /// <summary>Minecraft server variant. Null for Hytale.</summary>
    public MinecraftVariant? MinecraftVariant { get; set; }

    /// <summary>Game version string (e.g. "1.21.4" for Minecraft).</summary>
    public string GameVersion { get; set; } = string.Empty;

    // ── Resources ────────────────────────────────────────────────────────────
    public ResourceLimits Resources { get; set; } = new();

    // ── Network ──────────────────────────────────────────────────────────────
    public NetworkMode NetworkMode { get; set; } = NetworkMode.LanOnly;
    public int Port { get; set; } = 25565;

    // ── Options ──────────────────────────────────────────────────────────────
    public bool AllowPiratePlayers { get; set; }
    public int MaxPlayers { get; set; } = 20;

    // ── Modules ──────────────────────────────────────────────────────────────
    public IReadOnlyList<string> Modules { get; set; } = [];

    // ── Filesystem ───────────────────────────────────────────────────────────
    /// <summary>Absolute path to the server root directory.</summary>
    public string ServerDirectory { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;
}

/// <summary>Hardware resource limits applied at server startup.</summary>
public sealed record ResourceLimits
{
    public int RamMb { get; init; } = 2048;

    /// <summary>Minimum JVM heap in megabytes.</summary>
    public int RamMinMb { get; init; } = 512;

    /// <summary>Maximum CPU usage percentage hint (informational; not enforced by JVM).</summary>
    public int MaxCpuPercent { get; init; } = 80;
}

public enum NetworkMode
{
    LanOnly,
    PortForwarded,
    PlayitTunnel
}
