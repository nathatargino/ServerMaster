using ServerMaster.Core.Models;

namespace ServerMaster.Core.Abstractions;

/// <summary>
/// Abstract Factory that produces the correct <see cref="IServerEngine"/>
/// based on a <see cref="ServerProfile"/>.
/// </summary>
public interface IServerFactory
{
    /// <summary>Creates and returns the appropriate engine for the given profile.</summary>
    IServerEngine Create(ServerProfile profile);
}
