using ServerMaster.Core.Models;

namespace ServerMaster.Core.Abstractions;

/// <summary>
/// Abstraction over a network tunneling service (e.g. Playit.gg).
/// Allows servers behind CGNAT to be publicly reachable.
/// </summary>
public interface INetworkTunnel
{
    /// <summary>Current tunnel status stream.</summary>
    IObservable<TunnelStatus> StatusStream { get; }

    /// <summary>Injects the active profile data into this tunnel explicitly.</summary>
    void Initialize(ServerProfile profile);

    /// <summary>
    /// Starts the tunnel process pointing to <paramref name="localPort"/>.
    /// Returns a <see cref="TunnelInfo"/> with the public address once established.
    /// </summary>
    Task<TunnelInfo> StartAsync(int localPort, CancellationToken ct = default);

    /// <summary>Stops the tunnel process gracefully.</summary>
    Task StopAsync(CancellationToken ct = default);
}
