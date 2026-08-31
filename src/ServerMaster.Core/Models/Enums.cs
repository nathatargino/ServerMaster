namespace ServerMaster.Core.Models;

/// <summary>Supported game types.</summary>
public enum GameType
{
    Minecraft,
    Hytale
}

/// <summary>Variants / loaders for Minecraft servers.</summary>
public enum MinecraftVariant
{
    Vanilla,
    Paper,
    Purpur,
    Forge,
    Fabric
}

/// <summary>Lifecycle states of an <see cref="Abstractions.IServerEngine"/>.</summary>
public enum ServerState
{
    Idle,
    Preparing,
    Starting,
    Running,
    Stopping,
    Crashed,
    Stopped
}

/// <summary>States of the network tunnel.</summary>
public enum TunnelState
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

/// <summary>Log severity levels reflected in the log terminal UI.</summary>
public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error,
    Chat
}
